using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Admin;
using BusinessSearcher.Application.DTOs.Common;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using MediatR;

namespace BusinessSearcher.Application.Features.TenantManagement.Queries
{
    // ═══════════════════════════════════════════════════════════════
    // Estadísticas globales de la plataforma
    // ═══════════════════════════════════════════════════════════════
    public record GetAdminGlobalStatsQuery : IRequest<AdminGlobalStatsDto>;

    public class GetAdminGlobalStatsQueryHandler
        : IRequestHandler<GetAdminGlobalStatsQuery, AdminGlobalStatsDto>
    {
        private readonly ITenantRepository _tenantRepo;

        public GetAdminGlobalStatsQueryHandler(ITenantRepository tenantRepo)
            => _tenantRepo = tenantRepo;

        public async Task<AdminGlobalStatsDto> Handle(
            GetAdminGlobalStatsQuery request, CancellationToken cancellationToken)
        {
            var stats = await _tenantRepo.GetGlobalStatsAsync(cancellationToken);
            return new AdminGlobalStatsDto(
                stats.TotalTenants, stats.TrialTenants, stats.ActiveTenants,
                stats.SuspendedTenants, stats.InactiveTenants,
                stats.NewLast30Days, stats.TotalRevenue);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Lista paginada de todos los tenants (con filtros)
    // ═══════════════════════════════════════════════════════════════
    public record GetAllTenantsAdminQuery(
        int     Page       = 1,
        int     PageSize   = 20,
        string? Status     = null,
        string? Search     = null,
        bool?   IsApproved = null) : IRequest<PagedResult<AdminTenantDto>>;

    public class GetAllTenantsAdminQueryHandler
        : IRequestHandler<GetAllTenantsAdminQuery, PagedResult<AdminTenantDto>>
    {
        private readonly ITenantRepository _tenantRepo;

        public GetAllTenantsAdminQueryHandler(ITenantRepository tenantRepo)
            => _tenantRepo = tenantRepo;

        public async Task<PagedResult<AdminTenantDto>> Handle(
            GetAllTenantsAdminQuery request, CancellationToken cancellationToken)
        {
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var page     = Math.Max(request.Page, 1);

            var (items, total) = await _tenantRepo.GetAllWithFiltersAsync(
                page, pageSize, request.Status, request.Search, request.IsApproved, cancellationToken);

            var dtos = items.Select(t => new AdminTenantDto(
                t.Id, t.BusinessName, t.Email.Value,
                t.Status.ToString(),
                t.IsEmailVerified,
                t.IsApproved,
                t.IsSubscriptionActive(),
                t.Payments.Count,
                t.GetTotalPaid(),
                t.CreatedAt,
                t.NextPaymentDate,
                t.PhoneNumber,
                t.LastPaymentDate));

            return new PagedResult<AdminTenantDto>(dtos, total, page, pageSize);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Detalle completo de un tenant (con historial de pagos)
    // ═══════════════════════════════════════════════════════════════
    public record GetTenantDetailAdminQuery(Guid TenantId) : IRequest<AdminTenantDetailDto>;

    public class GetTenantDetailAdminQueryHandler
        : IRequestHandler<GetTenantDetailAdminQuery, AdminTenantDetailDto>
    {
        private readonly ITenantRepository _tenantRepo;

        public GetTenantDetailAdminQueryHandler(ITenantRepository tenantRepo)
            => _tenantRepo = tenantRepo;

        public async Task<AdminTenantDetailDto> Handle(
            GetTenantDetailAdminQuery request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
                ?? throw new Domain.Exceptions.DomainException("Tenant no encontrado.");

            var payments = tenant.Payments
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new PaymentRecordDto(p.Id, p.Amount, p.Currency, p.Reference, p.PaymentDate));

            return new AdminTenantDetailDto(
                tenant.Id, tenant.BusinessName, tenant.Email.Value,
                tenant.Status.ToString(),
                tenant.IsEmailVerified,
                tenant.IsApproved,
                tenant.IsSubscriptionActive(),
                tenant.GetTotalPaid(),
                tenant.CreatedAt,
                tenant.LastPaymentDate,
                tenant.NextPaymentDate,
                payments,
                tenant.PhoneNumber);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Tenants con más de 1 mes calendario sin pagar (para notificar al admin)
    // ═══════════════════════════════════════════════════════════════
    public record GetOverduePaymentsQuery : IRequest<IReadOnlyList<OverdueTenantDto>>;

    public class GetOverduePaymentsQueryHandler
        : IRequestHandler<GetOverduePaymentsQuery, IReadOnlyList<OverdueTenantDto>>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IDateTimeService  _dateTime;

        public GetOverduePaymentsQueryHandler(ITenantRepository tenantRepo, IDateTimeService dateTime)
        {
            _tenantRepo = tenantRepo;
            _dateTime   = dateTime;
        }

        public async Task<IReadOnlyList<OverdueTenantDto>> Handle(
            GetOverduePaymentsQuery request, CancellationToken cancellationToken)
        {
            var now = _dateTime.UtcNow;

            // Suspended/Inactive ya están "atendidos" por el admin; no hace falta re-notificar.
            var (items, _) = await _tenantRepo.GetAllWithFiltersAsync(
                page: 1, pageSize: 1000, status: null, search: null, isApproved: null, cancellationToken);

            return items
                .Where(t => t.Status is TenantStatus.Active or TenantStatus.Trial)
                .Where(t => t.IsPaymentOverdue(now))
                .Select(t => new OverdueTenantDto(
                    t.Id, t.BusinessName, t.Email.Value, t.Status.ToString(),
                    t.LastPaymentDate!.Value,
                    (int)(now - t.LastPaymentDate!.Value).TotalDays))
                .OrderByDescending(t => t.DaysOverdue)
                .ToList();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Reportes de pago (autoservicio del tenant) — listado para el admin
    // ═══════════════════════════════════════════════════════════════
    public record GetPaymentClaimsAdminQuery(string? Status = null) : IRequest<IReadOnlyList<AdminPaymentClaimDto>>;

    public class GetPaymentClaimsAdminQueryHandler
        : IRequestHandler<GetPaymentClaimsAdminQuery, IReadOnlyList<AdminPaymentClaimDto>>
    {
        private readonly ITenantRepository _tenantRepo;

        public GetPaymentClaimsAdminQueryHandler(ITenantRepository tenantRepo)
            => _tenantRepo = tenantRepo;

        public async Task<IReadOnlyList<AdminPaymentClaimDto>> Handle(
            GetPaymentClaimsAdminQuery request, CancellationToken cancellationToken)
        {
            var (items, _) = await _tenantRepo.GetAllWithFiltersAsync(
                page: 1, pageSize: 1000, status: null, search: null, isApproved: null, cancellationToken);

            var claims = items.SelectMany(t => t.PaymentClaims.Select(c => (Tenant: t, Claim: c)));

            if (!string.IsNullOrWhiteSpace(request.Status) &&
                Enum.TryParse<Domain.BoundedContext.TenantManagement.Enums.PaymentClaimStatus>(request.Status, true, out var statusFilter))
                claims = claims.Where(x => x.Claim.Status == statusFilter);

            return claims
                .OrderByDescending(x => x.Claim.CreatedAt)
                .Select(x => new AdminPaymentClaimDto(
                    x.Claim.Id, x.Tenant.Id, x.Tenant.BusinessName, x.Tenant.Email.Value,
                    x.Claim.PhoneNumber, x.Claim.Amount, x.Claim.Currency, x.Claim.ProofReference,
                    x.Claim.Status.ToString(), x.Claim.CreatedAt, x.Claim.ReviewedAt,
                    x.Tenant.Status.ToString(), x.Tenant.IsSubscriptionActive(),
                    x.Tenant.LastPaymentDate, x.Tenant.NextPaymentDate))
                .ToList();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Negocios Activos/Trial cuya suscripción vence en los próximos N días
    // (aviso previo al corte — se calcula al vuelo, sin job periódico)
    // ═══════════════════════════════════════════════════════════════
    public record GetExpiringSoonQuery(int Days = 3) : IRequest<IReadOnlyList<ExpiringTenantDto>>;

    public class GetExpiringSoonQueryHandler
        : IRequestHandler<GetExpiringSoonQuery, IReadOnlyList<ExpiringTenantDto>>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IDateTimeService  _dateTime;

        public GetExpiringSoonQueryHandler(ITenantRepository tenantRepo, IDateTimeService dateTime)
        {
            _tenantRepo = tenantRepo;
            _dateTime   = dateTime;
        }

        public async Task<IReadOnlyList<ExpiringTenantDto>> Handle(
            GetExpiringSoonQuery request, CancellationToken cancellationToken)
        {
            var now     = _dateTime.UtcNow;
            var horizon = now.AddDays(Math.Max(request.Days, 0));

            var (items, _) = await _tenantRepo.GetAllWithFiltersAsync(
                page: 1, pageSize: 1000, status: null, search: null, isApproved: null, cancellationToken);

            return items
                .Where(t => (t.Status == TenantStatus.Active || t.Status == TenantStatus.Trial) && t.NextPaymentDate.HasValue)
                .Where(t => t.NextPaymentDate!.Value <= horizon && t.NextPaymentDate.Value > now)
                .Select(t => new ExpiringTenantDto(
                    t.Id, t.BusinessName, t.Email.Value, t.NextPaymentDate!.Value,
                    (int)Math.Ceiling((t.NextPaymentDate.Value - now).TotalDays)))
                .OrderBy(t => t.DaysUntilExpiry)
                .ToList();
        }
    }
}
