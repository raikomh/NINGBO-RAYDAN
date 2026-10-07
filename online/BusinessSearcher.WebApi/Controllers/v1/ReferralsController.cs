using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.Features.Radar.Queries.Referrals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Programa de referidos del cliente: código propio, progreso mensual y niveles.
    /// La recompensa (días de premium) se aplica automáticamente cuando un invitado
    /// pasa a premium.
    /// </summary>
    [Route("api/v1/client/referrals")]
    [Authorize(Roles = "Client")]
    public class ReferralsController : BaseApiController
    {
        /// <summary>Estado de mis referidos: código, total, progreso del mes y niveles.</summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var result = await Mediator.Send(new GetMyReferralsQuery(), ct);
            return Ok(result);
        }

        /// <summary>Ranking de referidos válidos del mes (competencia mensual) y mi posición.</summary>
        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard(
            [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        {
            var result = await Mediator.Send(new GetReferralLeaderboardQuery(year, month), ct);
            return Ok(result);
        }
    }
}
