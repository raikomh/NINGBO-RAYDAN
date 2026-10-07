using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Features.TenantManagement.Commands.Marketing;
using BusinessSearcher.Application.Features.TenantManagement.Queries.Marketing;
using BusinessSearcher.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>Tracking simple de campañas de marketing (ej. links compartidos en grupos).</summary>
    [Route("api/v1")]
    public class MarketingController : BaseApiController
    {
        private readonly ICurrentUserService _currentUser;
        public MarketingController(ICurrentUserService currentUser) => _currentUser = currentUser;

        /// <summary>Registra una visita con su origen (?src=...). Anónimo: se llama al cargar la app.</summary>
        [HttpPost("track/visit")]
        [AllowAnonymous]
        public async Task<IActionResult> RecordVisit([FromBody] RecordVisitDto dto, CancellationToken ct)
        {
            await Mediator.Send(new RecordMarketingVisitCommand(dto.Source), ct);
            return Ok<object?>(null);
        }

        /// <summary>Resumen de visitas por origen (solo Admin).</summary>
        [Authorize]
        [HttpGet("admin/marketing/visits")]
        public async Task<IActionResult> GetVisitsSummary(CancellationToken ct)
        {
            if (_currentUser.Role != AccountRole.Admin) return Forbid();
            return Ok(await Mediator.Send(new GetMarketingVisitsSummaryQuery(), ct));
        }
    }

    public record RecordVisitDto(string Source);
}
