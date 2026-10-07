using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.Features.Radar.Queries.MipymeReferrals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Programa de referidos de MiPymes del cliente: cuántas MiPymes ha invitado este mes y
    /// el ranking de la competencia mensual paralela a la de clientes. El bono (7000 CUP por
    /// MiPyme validada) y el premio del top 1 se acreditan al mismo saldo unificado que ya
    /// expone /api/v1/client/referrals/me — este controller solo expone los contadores/ranking.
    /// </summary>
    [Route("api/v1/client/mipyme-referrals")]
    [Authorize(Roles = "Client")]
    public class MipymeReferralsController : BaseApiController
    {
        /// <summary>Cuántas MiPymes he referido y validado (total y en el mes actual).</summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var result = await Mediator.Send(new GetMyMipymeReferralsQuery(), ct);
            return Ok(result);
        }

        /// <summary>Ranking de MiPymes referidas válidas del mes (competencia mensual) y mi posición.</summary>
        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard(
            [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        {
            var result = await Mediator.Send(new GetMipymeReferralLeaderboardQuery(year, month), ct);
            return Ok(result);
        }
    }
}
