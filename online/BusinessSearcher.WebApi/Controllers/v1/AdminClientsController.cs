using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Admin;
using BusinessSearcher.Application.Features.Radar.Commands;
using BusinessSearcher.Application.Features.Radar.Commands.SetClientPlan;
using BusinessSearcher.Application.Features.Radar.Queries;
using BusinessSearcher.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Panel de administración de clientes del radar (app móvil).
    /// Requiere JWT con rol Admin (claim "role", ver AccountRole).
    /// </summary>
    [Authorize]
    [Route("api/v1/admin/clients")]
    public class AdminClientsController : BaseApiController
    {
        private readonly ICurrentUserService _currentUser;

        public AdminClientsController(ICurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }

        private IActionResult? ForbidIfNotAdmin() =>
            _currentUser.Role == AccountRole.Admin ? null : Forbid();

        /// <summary>Lista todos los clientes con filtros opcionales. Usa isApproved=false para ver pendientes de aprobación.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int     page       = 1,
            [FromQuery] int     pageSize   = 20,
            [FromQuery] string? search     = null,
            [FromQuery] bool?   isApproved = null,
            CancellationToken ct = default)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetAllClientsAdminQuery(page, pageSize, search, isApproved), ct);
            return Ok(result);
        }

        /// <summary>Aprueba un cliente recién registrado (habilita su login).</summary>
        [HttpPost("{clientId:guid}/approve")]
        public async Task<IActionResult> Approve(Guid clientId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminApproveClientCommand(clientId), ct);
            return Ok<object?>(null, "Cliente aprobado. Ya puede iniciar sesión.");
        }

        /// <summary>Rechaza/revoca la aprobación de un cliente (bloquea el login y cierra sus sesiones activas).</summary>
        [HttpDelete("{clientId:guid}/approve")]
        public async Task<IActionResult> Reject(Guid clientId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminRejectClientCommand(clientId), ct);
            return Ok<object?>(null, "Aprobación revocada. El cliente ya no puede iniciar sesión.");
        }

        /// <summary>Concede el plan Premium a un cliente (habilita las alertas de radar).</summary>
        [HttpPost("{clientId:guid}/premium")]
        public async Task<IActionResult> GrantPremium(Guid clientId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new SetClientPlanCommand(clientId, Premium: true), ct);
            return Ok(result, "Cliente actualizado a Premium.");
        }

        /// <summary>Revoca el plan Premium de un cliente (vuelve a Normal).</summary>
        [HttpDelete("{clientId:guid}/premium")]
        public async Task<IActionResult> RevokePremium(Guid clientId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new SetClientPlanCommand(clientId, Premium: false), ct);
            return Ok(result, "Plan Premium revocado. El cliente vuelve a Normal.");
        }

        /// <summary>Rechaza la solicitud de Premium de un cliente (se queda en Normal, sin concederlo).</summary>
        [HttpDelete("{clientId:guid}/premium-request")]
        public async Task<IActionResult> RejectPremiumRequest(Guid clientId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminRejectPremiumRequestCommand(clientId), ct);
            return Ok<object?>(null, "Solicitud de Premium rechazada.");
        }

        /// <summary>
        /// Clientes Premium cuyo período vence en los próximos N días (default 3): aviso
        /// previo al corte, calculado al vuelo (sin job periódico).
        /// </summary>
        [HttpGet("expiring-soon")]
        public async Task<IActionResult> GetExpiringSoon([FromQuery] int days, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetExpiringClientsQuery(days <= 0 ? 3 : days), ct);
            return Ok(result);
        }

        // ── Reportes de pago de Premium (autoservicio del cliente) ───────────────────

        /// <summary>Reportes de pago enviados por los clientes (teléfono + monto + comprobante), para revisión manual. Usa status=Pending para ver solo los pendientes.</summary>
        [HttpGet("premium-claims")]
        public async Task<IActionResult> GetPremiumClaims([FromQuery] string? status, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetPremiumClaimsAdminQuery(status), ct);
            return Ok(result);
        }

        /// <summary>Aprueba un reporte de pago: extiende el Premium 30 días.</summary>
        [HttpPost("{clientId:guid}/premium-claims/{claimId:guid}/approve")]
        public async Task<IActionResult> ApprovePremiumClaim(
            Guid clientId, Guid claimId, [FromBody] AdminReviewPremiumClaimDto dto, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminApprovePremiumClaimCommand(clientId, claimId, dto.Note), ct);
            return Ok<object?>(null, "Pago aprobado. Premium extendido 30 días.");
        }

        /// <summary>Rechaza un reporte de pago (comprobante no válido/no recibido)</summary>
        [HttpPost("{clientId:guid}/premium-claims/{claimId:guid}/reject")]
        public async Task<IActionResult> RejectPremiumClaim(
            Guid clientId, Guid claimId, [FromBody] AdminReviewPremiumClaimDto dto, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminRejectPremiumClaimCommand(clientId, claimId, dto.Note), ct);
            return Ok<object?>(null, "Reporte de pago rechazado.");
        }
    }
}
