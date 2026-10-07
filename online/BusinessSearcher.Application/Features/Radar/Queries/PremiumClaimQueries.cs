using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Admin;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Queries
{
    // ═══════════════════════════════════════════════════════════════
    // Mis reportes de pago de Premium (autoservicio) — historial visible al propio cliente
    // ═══════════════════════════════════════════════════════════════
    public record GetMyPremiumClaimsQuery : IRequest<IReadOnlyList<ClientPremiumClaimDto>>;

    public class GetMyPremiumClaimsQueryHandler
        : IRequestHandler<GetMyPremiumClaimsQuery, IReadOnlyList<ClientPremiumClaimDto>>
    {
        private readonly IClientRepository   _clients;
        private readonly ICurrentUserService _currentUser;

        public GetMyPremiumClaimsQueryHandler(IClientRepository clients, ICurrentUserService currentUser)
        {
            _clients     = clients;
            _currentUser = currentUser;
        }

        public async Task<IReadOnlyList<ClientPremiumClaimDto>> Handle(
            GetMyPremiumClaimsQuery request, CancellationToken cancellationToken)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, cancellationToken)
                ?? throw new DomainException("Cliente no encontrado.");

            return client.PremiumClaims
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new ClientPremiumClaimDto(
                    c.Id, c.Amount, c.Currency, c.ProofReference, c.PhoneNumber,
                    c.Status.ToString(), c.CreatedAt, c.ReviewedAt, c.ReviewNote))
                .ToList();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Reportes de pago de Premium (autoservicio del cliente) — listado para el admin
    // ═══════════════════════════════════════════════════════════════
    public record GetPremiumClaimsAdminQuery(string? Status = null) : IRequest<IReadOnlyList<AdminPremiumClaimDto>>;

    public class GetPremiumClaimsAdminQueryHandler
        : IRequestHandler<GetPremiumClaimsAdminQuery, IReadOnlyList<AdminPremiumClaimDto>>
    {
        private readonly IClientRepository _clients;

        public GetPremiumClaimsAdminQueryHandler(IClientRepository clients)
            => _clients = clients;

        public async Task<IReadOnlyList<AdminPremiumClaimDto>> Handle(
            GetPremiumClaimsAdminQuery request, CancellationToken cancellationToken)
        {
            var (items, _) = await _clients.GetAllWithFiltersAsync(
                page: 1, pageSize: 1000, search: null, isApproved: null, cancellationToken);

            var claims = items.SelectMany(c => c.PremiumClaims.Select(claim => (Client: c, Claim: claim)));

            if (!string.IsNullOrWhiteSpace(request.Status) &&
                Enum.TryParse<PremiumClaimStatus>(request.Status, true, out var statusFilter))
                claims = claims.Where(x => x.Claim.Status == statusFilter);

            return claims
                .OrderByDescending(x => x.Claim.CreatedAt)
                .Select(x => new AdminPremiumClaimDto(
                    x.Claim.Id, x.Client.Id, x.Client.FullName, x.Client.Email.Value,
                    x.Claim.PhoneNumber, x.Claim.Amount, x.Claim.Currency, x.Claim.ProofReference,
                    x.Claim.Status.ToString(), x.Claim.CreatedAt, x.Claim.ReviewedAt,
                    x.Client.Plan.ToString(), x.Client.PremiumUntil))
                .ToList();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Clientes Premium cuyo período vence en los próximos N días
    // (aviso previo al corte — se calcula al vuelo, sin job periódico)
    // ═══════════════════════════════════════════════════════════════
    public record GetExpiringClientsQuery(int Days = 3) : IRequest<IReadOnlyList<ExpiringClientDto>>;

    public class GetExpiringClientsQueryHandler
        : IRequestHandler<GetExpiringClientsQuery, IReadOnlyList<ExpiringClientDto>>
    {
        private readonly IClientRepository _clients;
        private readonly IDateTimeService  _dateTime;

        public GetExpiringClientsQueryHandler(IClientRepository clients, IDateTimeService dateTime)
        {
            _clients   = clients;
            _dateTime  = dateTime;
        }

        public async Task<IReadOnlyList<ExpiringClientDto>> Handle(
            GetExpiringClientsQuery request, CancellationToken cancellationToken)
        {
            var now     = _dateTime.UtcNow;
            var horizon = now.AddDays(Math.Max(request.Days, 0));

            var (items, _) = await _clients.GetAllWithFiltersAsync(
                page: 1, pageSize: 1000, search: null, isApproved: null, cancellationToken);

            return items
                .Where(c => c.Plan == ClientPlan.Premium && c.PremiumUntil.HasValue)
                .Where(c => c.PremiumUntil!.Value <= horizon && c.PremiumUntil.Value > now)
                .Select(c => new ExpiringClientDto(
                    c.Id, c.FullName, c.Email.Value, c.PremiumUntil!.Value,
                    (int)Math.Ceiling((c.PremiumUntil.Value - now).TotalDays)))
                .OrderBy(c => c.DaysUntilExpiry)
                .ToList();
        }
    }
}
