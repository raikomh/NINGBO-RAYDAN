using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Features.Radar.Commands;
using BusinessSearcher.Application.Features.Radar.Queries.MipymeReferrals;
using BusinessSearcher.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Panel de administración de la competencia mensual "MiPymes referidas": ranking y
    /// cierre de mes (premio al top 1). El pago del saldo unificado de CUP (bonos de
    /// MiPymes incluidos) sigue cubierto por AdminReferralsController — este controller NO
    /// tiene endpoints de payout/settle propios. Requiere JWT con rol Admin.
    /// </summary>
    [Authorize]
    [Route("api/v1/admin/mipyme-referrals")]
    public class AdminMipymeReferralsController : BaseApiController
    {
        private readonly ICurrentUserService _currentUser;

        public AdminMipymeReferralsController(ICurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }

        private IActionResult? ForbidIfNotAdmin() =>
            _currentUser.Role == AccountRole.Admin ? null : Forbid();

        /// <summary>Ranking de MiPymes referidas válidas del mes (sin filtrar por "mi posición").</summary>
        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard(
            [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetMipymeReferralLeaderboardQuery(year, month, Take: 100), ct);
            return Ok(result);
        }

        /// <summary>
        /// Cierra la competencia mensual de MiPymes referidas: calcula el premio y lo acredita
        /// al referidor con más MiPymes válidas del mes. No se puede repetir para el mismo
        /// (año, mes) — falla si ya fue cerrado. Independiente del cierre de la competencia de clientes.
        /// </summary>
        [HttpPost("close-month")]
        public async Task<IActionResult> CloseMonth(
            [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new AdminCloseMonthlyMipymeReferralCompetitionCommand(year, month), ct);
            return Ok(result, result.WinnerClientId.HasValue
                ? $"{result.WinnerName} ganó {result.PrizeAmount} CUP."
                : "Nadie tuvo MiPymes referidas válidas ese mes: nada que premiar.");
        }
    }
}
