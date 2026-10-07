using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Features.Radar.Commands;
using BusinessSearcher.Application.Features.Radar.Queries;
using BusinessSearcher.Application.Features.Radar.Queries.Referrals;
using BusinessSearcher.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Panel de administración del programa de referidos con recompensa en CUP:
    /// saldos pendientes de pago, liquidación individual y cierre de la
    /// competencia mensual (premio al top 1). Requiere JWT con rol Admin.
    /// </summary>
    [Authorize]
    [Route("api/v1/admin/referrals")]
    public class AdminReferralsController : BaseApiController
    {
        private readonly ICurrentUserService _currentUser;

        public AdminReferralsController(ICurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }

        private IActionResult? ForbidIfNotAdmin() =>
            _currentUser.Role == AccountRole.Admin ? null : Forbid();

        /// <summary>Clientes con saldo de CUP pendiente de pago.</summary>
        [HttpGet("payouts")]
        public async Task<IActionResult> GetPayouts(CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetReferralPayoutsAdminQuery(), ct);
            return Ok(result);
        }

        /// <summary>Marca como pagado el saldo de CUP de un cliente (lo mueve al histórico y lo resetea a 0).</summary>
        [HttpPost("{clientId:guid}/settle-payout")]
        public async Task<IActionResult> SettlePayout(Guid clientId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var paid = await Mediator.Send(new AdminSettleReferralPayoutCommand(clientId), ct);
            return Ok(paid, paid > 0 ? $"Se liquidaron {paid} CUP." : "Este cliente no tenía saldo pendiente.");
        }

        /// <summary>Ranking de referidos válidos del mes (sin filtrar por "mi posición").</summary>
        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard(
            [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetReferralLeaderboardQuery(year, month, Take: 100), ct);
            return Ok(result);
        }

        /// <summary>
        /// Cierra la competencia mensual de referidos: calcula el premio y lo acredita
        /// al invitador con más referidos válidos del mes. No se puede repetir para el
        /// mismo (año, mes) — falla si ya fue cerrado.
        /// </summary>
        [HttpPost("close-month")]
        public async Task<IActionResult> CloseMonth(
            [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new AdminCloseMonthlyReferralCompetitionCommand(year, month), ct);
            return Ok(result, result.WinnerClientId.HasValue
                ? $"{result.WinnerName} ganó {result.PrizeAmount} CUP."
                : "Nadie tuvo referidos válidos ese mes: nada que premiar.");
        }
    }
}
