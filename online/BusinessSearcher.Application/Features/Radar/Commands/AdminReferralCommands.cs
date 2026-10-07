using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Commands
{
    // ═══════════════════════════════════════════════════════════════
    // SETTLE PAYOUT — el admin paga (fuera del sistema) el saldo de CUP
    // de un cliente y lo resetea a 0.
    // ═══════════════════════════════════════════════════════════════
    public record AdminSettleReferralPayoutCommand(Guid ClientId) : IRequest<decimal>;

    public class AdminSettleReferralPayoutCommandHandler : IRequestHandler<AdminSettleReferralPayoutCommand, decimal>
    {
        private readonly IClientRepository _clients;
        private readonly IRadarUnitOfWork  _uow;

        public AdminSettleReferralPayoutCommandHandler(IClientRepository clients, IRadarUnitOfWork uow)
        {
            _clients = clients;
            _uow     = uow;
        }

        public async Task<decimal> Handle(AdminSettleReferralPayoutCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            var paid = client.SettleReferralCupPayout();
            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);

            return paid;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CLOSE MONTH — cierra la competencia mensual: el invitador con más referidos
    // válidos ese mes (R_ganador) gana el premio P = R_ganador × 40 × 0.20.
    // A propósito NO se usa el total de referidos de toda la plataforma: eso pagaría
    // al ganador por el trabajo de otros invitadores, que ya cobran su propio bono
    // individual aparte y no ven nada de este premio.
    // ═══════════════════════════════════════════════════════════════
    public record AdminCloseMonthlyReferralCompetitionCommand(int? Year = null, int? Month = null)
        : IRequest<MonthlySettlementReceiptDto>;

    public class AdminCloseMonthlyReferralCompetitionCommandHandler
        : IRequestHandler<AdminCloseMonthlyReferralCompetitionCommand, MonthlySettlementReceiptDto>
    {
        // 40 CUP = precio del Plan Normal: cada referido válido del ganador se asume como
        // un alta que paga ese plan, así que R_ganador × 40 son los ingresos asumidos que
        // esos referidos generan. El premio es el 20% de eso, lo que deja margen sobre los
        // 10 CUP/referido que ese mismo referido ya paga como bono individual
        // (40 ≥ 10 + 40×0.20 = 18, ~55% de margen por referido).
        public const decimal AssumedRevenuePerReferral = 40m;
        public const decimal PrizePoolRate = 0.20m;

        private readonly IClientRepository                     _clients;
        private readonly IReferralRepository                   _referrals;
        private readonly IReferralMonthlySettlementRepository  _settlements;
        private readonly IRadarUnitOfWork                      _uow;

        public AdminCloseMonthlyReferralCompetitionCommandHandler(
            IClientRepository clients, IReferralRepository referrals,
            IReferralMonthlySettlementRepository settlements, IRadarUnitOfWork uow)
        {
            _clients     = clients;
            _referrals   = referrals;
            _settlements = settlements;
            _uow         = uow;
        }

        public async Task<MonthlySettlementReceiptDto> Handle(
            AdminCloseMonthlyReferralCompetitionCommand request, CancellationToken ct)
        {
            var now   = DateTime.UtcNow;
            var year  = request.Year  ?? now.Year;
            var month = request.Month ?? now.Month;

            var existing = await _settlements.GetByPeriodAsync(year, month, ct);
            if (existing is not null)
                throw new DomainException($"La competencia de {month:00}/{year} ya fue cerrada.");

            var counts = await _referrals.GetValidCountsByInviterForMonthAsync(year, month, ct);
            var totalValidReferrals = counts.Sum(c => c.Count);

            if (totalValidReferrals == 0)
            {
                var empty = ReferralMonthlySettlement.Create(year, month, null, 0m, 0);
                await _settlements.AddAsync(empty, ct);
                await _uow.SaveChangesAsync(ct);
                return new MonthlySettlementReceiptDto(year, month, null, null, 0m, 0, AlreadySettled: false);
            }

            var winnerCount = counts
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.LastValidatedAt)
                .First();

            // Premio calculado SOLO con los referidos del ganador (no totalValidReferrals,
            // que es de toda la plataforma) — ver comentario de la clase.
            var prize = winnerCount.Count * AssumedRevenuePerReferral * PrizePoolRate;

            var winner = await _clients.GetByIdAsync(winnerCount.InviterClientId, ct)
                ?? throw new DomainException("El cliente ganador ya no existe.");

            winner.CreditReferralCup(prize, Domain.BoundedContext.Radar.Enums.ReferralCupTransactionType.MonthlyPrize,
                note: $"Top 1 de la competencia mensual {month:00}/{year}");
            await _clients.UpdateAsync(winner, ct);

            var settlement = ReferralMonthlySettlement.Create(year, month, winner.Id, prize, winnerCount.Count);
            await _settlements.AddAsync(settlement, ct);

            await _uow.SaveChangesAsync(ct);

            return new MonthlySettlementReceiptDto(
                year, month, winner.Id, winner.FullName, prize, winnerCount.Count, AlreadySettled: false);
        }
    }
}
