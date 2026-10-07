using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.TenantManagement.Commands
{
    // ═══════════════════════════════════════════════════════════════
    // SUBMIT — el propio tenant reporta un pago (autoservicio: teléfono + monto + comprobante)
    // ═══════════════════════════════════════════════════════════════
    public record SubmitPaymentClaimCommand(
        string PhoneNumber, decimal Amount, string? Currency, string ProofReference) : IRequest<Guid>;

    public class SubmitPaymentClaimCommandValidator : AbstractValidator<SubmitPaymentClaimCommand>
    {
        public SubmitPaymentClaimCommandValidator()
        {
            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("El número de teléfono es requerido.")
                .MaximumLength(30);
            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("El monto debe ser mayor que cero.");
            RuleFor(x => x.ProofReference)
                .NotEmpty().WithMessage("Indica una referencia del comprobante (número de operación, remitente, etc.).")
                .MaximumLength(300);
        }
    }

    public class SubmitPaymentClaimCommandHandler : IRequestHandler<SubmitPaymentClaimCommand, Guid>
    {
        private readonly ITenantRepository   _tenantRepo;
        private readonly IUnitOfWork         _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public SubmitPaymentClaimCommandHandler(
            ITenantRepository tenantRepo, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
        {
            _tenantRepo  = tenantRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(SubmitPaymentClaimCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            var claim = tenant.SubmitPaymentClaim(
                request.PhoneNumber, request.Amount, request.Currency ?? "CUP", request.ProofReference);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return claim.Id;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ADMIN APPROVE — registra el pago real (extiende 30 días) y cierra el reporte
    // ═══════════════════════════════════════════════════════════════
    public record AdminApprovePaymentClaimCommand(Guid TenantId, Guid ClaimId, string? Note) : IRequest;

    public class AdminApprovePaymentClaimCommandHandler : IRequestHandler<AdminApprovePaymentClaimCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;

        public AdminApprovePaymentClaimCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(AdminApprovePaymentClaimCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            tenant.ApprovePaymentClaim(request.ClaimId, request.Note);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ADMIN REJECT — cierra el reporte sin registrar pago
    // ═══════════════════════════════════════════════════════════════
    public record AdminRejectPaymentClaimCommand(Guid TenantId, Guid ClaimId, string? Note) : IRequest;

    public class AdminRejectPaymentClaimCommandHandler : IRequestHandler<AdminRejectPaymentClaimCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;

        public AdminRejectPaymentClaimCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(AdminRejectPaymentClaimCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            tenant.RejectPaymentClaim(request.ClaimId, request.Note);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
