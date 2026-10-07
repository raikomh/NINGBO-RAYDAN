using BusinessSearcher.Application.Commons;
using BusinessSearcher.Application.DTOs.Sync;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace BusinessSearcher.Application.Features.TenantManagement.Queries
{
    // ═══════════════════════════════════════════════════════════════
    // ONLINE: estado de suscripción (para que una instalación Local refresque el suyo)
    // Se ejecuta en el backend ONLINE. Misma autenticación que Export/Ingest: por hash de la API
    // key en X-Sync-Api-Key, no requiere una sesión online iniciada (el tenant puede seguir sin
    // aprobar). Ver SyncController.Status, [AllowAnonymous].
    // ═══════════════════════════════════════════════════════════════
    public record GetTenantSyncStatusQuery(string? ApiKey) : IRequest<SyncStatusDto>;

    public class GetTenantSyncStatusQueryHandler : IRequestHandler<GetTenantSyncStatusQuery, SyncStatusDto>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IConfiguration    _config;

        public GetTenantSyncStatusQueryHandler(ITenantRepository tenantRepo, IConfiguration config)
        {
            _tenantRepo = tenantRepo;
            _config     = config;
        }

        public async Task<SyncStatusDto> Handle(GetTenantSyncStatusQuery request, CancellationToken cancellationToken)
        {
            if (DeploymentMode.IsLocal(_config))
                throw new DomainException("Este endpoint solo existe en el backend online.");

            if (string.IsNullOrWhiteSpace(request.ApiKey))
                throw new UnauthorizedAccessException("Falta el header X-Sync-Api-Key.");

            var tenant = await _tenantRepo.GetBySyncApiKeyHashAsync(
                SyncApiKeyHasher.Hash(request.ApiKey), cancellationToken)
                ?? throw new UnauthorizedAccessException("API key de sincronización inválida.");

            return new SyncStatusDto(
                tenant.IsApproved, tenant.Status.ToString(), tenant.LastPaymentDate, tenant.NextPaymentDate,
                tenant.IsSubscriptionActive());
        }
    }
}
