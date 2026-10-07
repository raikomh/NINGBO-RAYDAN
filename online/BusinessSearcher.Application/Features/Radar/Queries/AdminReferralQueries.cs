using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.Radar.Commands.SetClientPlan;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Queries
{
    // ═══════════════════════════════════════════════════════════════
    // Lista de clientes con actividad en el programa de referidos (invitados
    // válidos y/o CUP ganado) — panel de admin. Incluye el desglose de cuánto
    // viene de bonos por invitado vs. premios de la competencia mensual (de
    // clientes y de MiPymes por separado), junto al saldo pendiente de pago
    // (unificado: un solo saldo/botón "pagar" para ambos programas) y el
    // histórico ya liquidado.
    // ═══════════════════════════════════════════════════════════════
    public record GetReferralPayoutsAdminQuery : IRequest<IReadOnlyList<ReferralPayoutAdminDto>>;

    public class GetReferralPayoutsAdminQueryHandler
        : IRequestHandler<GetReferralPayoutsAdminQuery, IReadOnlyList<ReferralPayoutAdminDto>>
    {
        private readonly IClientRepository _clients;

        public GetReferralPayoutsAdminQueryHandler(IClientRepository clients) => _clients = clients;

        public async Task<IReadOnlyList<ReferralPayoutAdminDto>> Handle(
            GetReferralPayoutsAdminQuery request, CancellationToken ct)
        {
            // No paginado: se espera un volumen manejable de clientes con actividad de
            // referidos a la vez.
            var (items, _) = await _clients.GetAllWithFiltersAsync(1, int.MaxValue, null, null, ct);

            return items
                .Where(c => c.ValidReferralsTotal > 0 || c.MipymeReferralsTotal > 0
                    || c.ReferralCupBalance > 0 || c.ReferralCupPaidTotal > 0)
                .OrderByDescending(c => c.ReferralCupBalance)
                .ThenByDescending(c => c.ReferralCupBalance + c.ReferralCupPaidTotal)
                .Select(c =>
                {
                    // Desglose real por tipo de movimiento (no residual): cada bono/premio
                    // queda registrado como su propio ReferralCupTransaction, así que sumamos
                    // directo por Type en vez de inferir por resta — necesario ahora que el
                    // saldo unificado mezcla 4 tipos de movimiento (bono/premio de clientes y
                    // de MiPymes) además de la liquidación (PayoutSettlement, que no se cuenta aquí).
                    var bonusTotal = c.ReferralCupTransactions
                        .Where(t => t.Type == ReferralCupTransactionType.PerReferralBonus).Sum(t => t.Amount);
                    var prizeTotal = c.ReferralCupTransactions
                        .Where(t => t.Type == ReferralCupTransactionType.MonthlyPrize).Sum(t => t.Amount);
                    var mipymeBonusTotal = c.ReferralCupTransactions
                        .Where(t => t.Type == ReferralCupTransactionType.MipymeReferralBonus).Sum(t => t.Amount);
                    var mipymePrizeTotal = c.ReferralCupTransactions
                        .Where(t => t.Type == ReferralCupTransactionType.MipymeMonthlyPrize).Sum(t => t.Amount);

                    return new ReferralPayoutAdminDto(
                        c.Id, c.FullName, c.Email.Value,
                        c.ValidReferralsTotal, bonusTotal, prizeTotal,
                        c.ReferralCupBalance, c.ReferralCupPaidTotal,
                        c.MipymeReferralsTotal, mipymeBonusTotal, mipymePrizeTotal);
                })
                .ToList();
        }
    }
}
