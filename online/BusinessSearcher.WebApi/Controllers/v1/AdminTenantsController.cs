using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Admin;
using BusinessSearcher.Application.Features.TenantManagement.Commands;
using BusinessSearcher.Application.Features.TenantManagement.Queries;
using BusinessSearcher.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Panel de administración de tenants.
    /// Requiere JWT con rol Admin (claim "role", ver AccountRole).
    /// </summary>
    [Authorize]
    [Route("api/v1/admin/tenants")]
    public class AdminTenantsController : BaseApiController
    {
        private readonly ICurrentUserService _currentUser;

        public AdminTenantsController(ICurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }

        private IActionResult? ForbidIfNotAdmin() =>
            _currentUser.Role == AccountRole.Admin ? null : Forbid();

        // ── Estadísticas globales ─────────────────────────────────────────────────

        /// <summary>Estadísticas globales: totales, por plan, por estado, revenue</summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetGlobalStats(CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetAdminGlobalStatsQuery(), ct);
            return Ok(result);
        }

        // ── CRUD de tenants ───────────────────────────────────────────────────────

        /// <summary>Lista todos los tenants con filtros opcionales. Usa isApproved=false para ver pendientes de aprobación.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int     page       = 1,
            [FromQuery] int     pageSize   = 20,
            [FromQuery] string? status     = null,
            [FromQuery] string? search     = null,
            [FromQuery] bool?   isApproved = null,
            CancellationToken ct = default)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(
                new GetAllTenantsAdminQuery(page, pageSize, status, search, isApproved), ct);
            return Ok(result);
        }

        /// <summary>
        /// Tenants Activos/Trial con más de 1 mes calendario sin pagar desde su último
        /// pago (aritmética de meses reales: AddMonths, no 30 días fijos).
        /// </summary>
        [HttpGet("overdue-payments")]
        public async Task<IActionResult> GetOverduePayments(CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetOverduePaymentsQuery(), ct);
            return Ok(result);
        }

        /// <summary>Detalle completo de un tenant (incluye historial de pagos)</summary>
        [HttpGet("{tenantId:guid}")]
        public async Task<IActionResult> GetDetail(Guid tenantId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetTenantDetailAdminQuery(tenantId), ct);
            return Ok(result);
        }

        /// <summary>
        /// Negocios Activos/Trial cuya suscripción vence en los próximos N días (default 3):
        /// aviso previo al corte, calculado al vuelo (sin job periódico).
        /// </summary>
        [HttpGet("expiring-soon")]
        public async Task<IActionResult> GetExpiringSoon([FromQuery] int days, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetExpiringSoonQuery(days <= 0 ? 3 : days), ct);
            return Ok(result);
        }

        // ── Reportes de pago (autoservicio del tenant) ───────────────────────────────

        /// <summary>Reportes de pago enviados por los tenants (teléfono + monto + comprobante), para revisión manual. Usa status=Pending para ver solo los pendientes.</summary>
        [HttpGet("payment-claims")]
        public async Task<IActionResult> GetPaymentClaims([FromQuery] string? status, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            var result = await Mediator.Send(new GetPaymentClaimsAdminQuery(status), ct);
            return Ok(result);
        }

        /// <summary>Aprueba un reporte de pago: registra el pago real y extiende la suscripción 30 días.</summary>
        [HttpPost("{tenantId:guid}/payment-claims/{claimId:guid}/approve")]
        public async Task<IActionResult> ApprovePaymentClaim(
            Guid tenantId, Guid claimId, [FromBody] AdminReviewPaymentClaimDto dto, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminApprovePaymentClaimCommand(tenantId, claimId, dto.Note), ct);
            return Ok<object?>(null, "Pago aprobado. Suscripción renovada por 30 días.");
        }

        /// <summary>Rechaza un reporte de pago (comprobante no válido/no recibido)</summary>
        [HttpPost("{tenantId:guid}/payment-claims/{claimId:guid}/reject")]
        public async Task<IActionResult> RejectPaymentClaim(
            Guid tenantId, Guid claimId, [FromBody] AdminReviewPaymentClaimDto dto, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminRejectPaymentClaimCommand(tenantId, claimId, dto.Note), ct);
            return Ok<object?>(null, "Reporte de pago rechazado.");
        }

        // ── Acciones sobre tenants ────────────────────────────────────────────────

        /// <summary>Aprueba un tenant recién registrado (habilita su login)</summary>
        [HttpPost("{tenantId:guid}/approve")]
        public async Task<IActionResult> Approve(Guid tenantId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminApproveTenantCommand(tenantId), ct);
            return Ok<object?>(null, "Tenant aprobado. Ya puede iniciar sesión.");
        }

        /// <summary>Suspende un tenant (bloquea su acceso)</summary>
        [HttpPost("{tenantId:guid}/suspend")]
        public async Task<IActionResult> Suspend(
            Guid tenantId, [FromBody] AdminSuspendDto dto, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminSuspendTenantCommand(tenantId, dto.Reason), ct);
            return Ok<object?>(null, "Tenant suspendido.");
        }

        /// <summary>Reactiva un tenant suspendido o inactivo</summary>
        [HttpPost("{tenantId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid tenantId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminActivateTenantCommand(tenantId), ct);
            return Ok<object?>(null, "Tenant activado.");
        }

        /// <summary>Registra un pago manual y extiende la suscripción 30 días (reactiva si estaba suspendido/inactivo)</summary>
        [HttpPost("{tenantId:guid}/renew")]
        public async Task<IActionResult> Renew(Guid tenantId, [FromBody] AdminRenewDto dto, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminRenewTenantCommand(tenantId, dto.Amount, dto.Currency ?? "COP", dto.Reference), ct);
            return Ok<object?>(null, "Pago registrado. Suscripción renovada por 30 días.");
        }

        /// <summary>Desactiva (soft-delete) un tenant y cierra todas sus sesiones</summary>
        [HttpDelete("{tenantId:guid}")]
        public async Task<IActionResult> Delete(Guid tenantId, CancellationToken ct)
        {
            var deny = ForbidIfNotAdmin(); if (deny is not null) return deny;
            await Mediator.Send(new AdminDeleteTenantCommand(tenantId), ct);
            return Ok<object?>(null, "Tenant desactivado.");
        }
    }
}
