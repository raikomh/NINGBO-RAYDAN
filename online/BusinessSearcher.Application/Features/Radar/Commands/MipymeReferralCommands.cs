using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Commands
{
    /// <summary>
    /// Lógica compartida de recompensa de referidos de MiPymes. Mirroring exacto de
    /// ReferralReward.ValidateAndRewardAsync (ClientPlanCommands.cs), pero para el programa
    /// cliente-refiere-MiPyme: se dispara desde los mismos dos puntos que aprueban un Tenant
    /// (AdminApproveTenantCommandHandler y la rama de auto-aprobación de
    /// RegisterTenantCommandHandler) — no existe un patrón de domain-event-dispatch en esta
    /// capa (confirmado por grep), así que se sigue la convención existente de llamada directa.
    /// Es idempotente: MipymeReferral.MarkValid() no hace nada si ya estaba validado.
    /// Al validarse: sube el contador de MiPymes del que invitó y le acredita 7000 CUP al
    /// saldo unificado (mismo saldo/balance que los referidos de clientes,
    /// Client.ReferralCupBalance — no se crea ningún saldo nuevo).
    /// </summary>
    public static class MipymeReferralReward
    {
        public static async Task ValidateAndRewardAsync(
            Guid tenantId,
            IMipymeReferralRepository mipymeReferrals,
            IClientRepository clients,
            DateTime now,
            CancellationToken ct)
        {
            var referral = await mipymeReferrals.GetPendingByReferredTenantAsync(tenantId, ct);
            if (referral is null || !referral.MarkValid())
                return;

            await mipymeReferrals.UpdateAsync(referral, ct);

            var inviter = await clients.GetByIdAsync(referral.ReferrerClientId, ct);
            if (inviter is null) return;

            inviter.RecordValidMipymeReferral(now);

            await clients.UpdateAsync(inviter, ct);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CLOSE MONTH (MiPymes) — cierra la competencia mensual paralela: el referidor con más
    // MiPymes válidas ese mes (R_ganador) gana el premio P = R_ganador × 7000 × 0.30.
    // Igual que en la competencia de clientes, a propósito NO se usa el total de MiPymes
    // referidas de toda la plataforma: eso pagaría al ganador por el trabajo de otros
    // referidores, que ya cobran su propio bono individual aparte. Guard de idempotencia
    // independiente (MipymeReferralMonthlySettlement) del de la competencia de clientes.
    // ═══════════════════════════════════════════════════════════════
    public record AdminCloseMonthlyMipymeReferralCompetitionCommand(int? Year = null, int? Month = null)
        : IRequest<MipymeMonthlySettlementReceiptDto>;

    public class AdminCloseMonthlyMipymeReferralCompetitionCommandHandler
        : IRequestHandler<AdminCloseMonthlyMipymeReferralCompetitionCommand, MipymeMonthlySettlementReceiptDto>
    {
        // 7000 CUP = bono fijo por MiPyme referida válida (MipymeReferralReward). El premio
        // mensual del top 1 es el 30% de esa base, aplicada solo a las MiPymes del ganador.
        public const decimal AssumedRevenuePerMipymeReferral = 7000m;
        public const decimal PrizePoolRate = 0.30m;

        private readonly IClientRepository                            _clients;
        private readonly IMipymeReferralRepository                    _mipymeReferrals;
        private readonly IMipymeReferralMonthlySettlementRepository   _settlements;
        private readonly IRadarUnitOfWork                             _uow;

        public AdminCloseMonthlyMipymeReferralCompetitionCommandHandler(
            IClientRepository clients, IMipymeReferralRepository mipymeReferrals,
            IMipymeReferralMonthlySettlementRepository settlements, IRadarUnitOfWork uow)
        {
            _clients         = clients;
            _mipymeReferrals = mipymeReferrals;
            _settlements     = settlements;
            _uow             = uow;
        }

        public async Task<MipymeMonthlySettlementReceiptDto> Handle(
            AdminCloseMonthlyMipymeReferralCompetitionCommand request, CancellationToken ct)
        {
            var now   = DateTime.UtcNow;
            var year  = request.Year  ?? now.Year;
            var month = request.Month ?? now.Month;

            var existing = await _settlements.GetByPeriodAsync(year, month, ct);
            if (existing is not null)
                throw new DomainException($"La competencia de MiPymes referidas de {month:00}/{year} ya fue cerrada.");

            var counts = await _mipymeReferrals.GetValidCountsByReferrerForMonthAsync(year, month, ct);
            var totalValidMipymeReferrals = counts.Sum(c => c.Count);

            if (totalValidMipymeReferrals == 0)
            {
                var empty = MipymeReferralMonthlySettlement.Create(year, month, null, 0m, 0);
                await _settlements.AddAsync(empty, ct);
                await _uow.SaveChangesAsync(ct);
                return new MipymeMonthlySettlementReceiptDto(year, month, null, null, 0m, 0, AlreadySettled: false);
            }

            var winnerCount = counts
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.LastValidatedAt)
                .First();

            // Premio calculado SOLO con las MiPymes referidas del ganador (no
            // totalValidMipymeReferrals, que es de toda la plataforma) — ver comentario de la clase.
            var prize = winnerCount.Count * AssumedRevenuePerMipymeReferral * PrizePoolRate;

            var winner = await _clients.GetByIdAsync(winnerCount.ReferrerClientId, ct)
                ?? throw new DomainException("El cliente ganador ya no existe.");

            winner.CreditReferralCup(prize, Domain.BoundedContext.Radar.Enums.ReferralCupTransactionType.MipymeMonthlyPrize,
                note: $"Top 1 de la competencia mensual de MiPymes referidas {month:00}/{year}");
            await _clients.UpdateAsync(winner, ct);

            var settlement = MipymeReferralMonthlySettlement.Create(year, month, winner.Id, prize, winnerCount.Count);
            await _settlements.AddAsync(settlement, ct);

            await _uow.SaveChangesAsync(ct);

            return new MipymeMonthlySettlementReceiptDto(
                year, month, winner.Id, winner.FullName, prize, winnerCount.Count, AlreadySettled: false);
        }
    }
}
