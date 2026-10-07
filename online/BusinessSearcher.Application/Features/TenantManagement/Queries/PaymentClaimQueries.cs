using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.TenantManagement;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.TenantManagement.Queries
{
    // ═══════════════════════════════════════════════════════════════
    // Mis reportes de pago (autoservicio) — historial visible al propio tenant
    // ═══════════════════════════════════════════════════════════════
    public record GetMyPaymentClaimsQuery : IRequest<IReadOnlyList<PaymentClaimDto>>;

    public class GetMyPaymentClaimsQueryHandler
        : IRequestHandler<GetMyPaymentClaimsQuery, IReadOnlyList<PaymentClaimDto>>
    {
        private readonly ITenantRepository   _tenantRepo;
        private readonly ICurrentUserService _currentUser;

        public GetMyPaymentClaimsQueryHandler(ITenantRepository tenantRepo, ICurrentUserService currentUser)
        {
            _tenantRepo  = tenantRepo;
            _currentUser = currentUser;
        }

        public async Task<IReadOnlyList<PaymentClaimDto>> Handle(
            GetMyPaymentClaimsQuery request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            return tenant.PaymentClaims
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new PaymentClaimDto(
                    c.Id, c.Amount, c.Currency, c.ProofReference, c.PhoneNumber,
                    c.Status.ToString(), c.CreatedAt, c.ReviewedAt, c.ReviewNote))
                .ToList();
        }
    }
}
