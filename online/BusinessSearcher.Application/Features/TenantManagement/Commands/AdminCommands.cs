using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using BusinessSearcher.Application.Commons.Interfaces;
using MediatR;

namespace BusinessSearcher.Application.Features.TenantManagement.Commands
{
    // ═══════════════════════════════════════════════════════════════
    // APPROVE TENANT — habilita el login de un tenant recién registrado
    // ═══════════════════════════════════════════════════════════════
    public record AdminApproveTenantCommand(Guid TenantId) : IRequest;

    public class AdminApproveTenantCommandHandler : IRequestHandler<AdminApproveTenantCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;
        // Programa de referidos cliente-refiere-MiPyme: vive en el bounded context Radar
        // (mismo patrón de acoplamiento suelto ya usado por Tenant.ReferredByClientId).
        private readonly Domain.BoundedContext.Radar.Repositories.IClientRepository _radarClients;
        private readonly Domain.BoundedContext.Radar.Repositories.IMipymeReferralRepository _mipymeReferrals;
        private readonly IRadarUnitOfWork _radarUow;

        public AdminApproveTenantCommandHandler(
            ITenantRepository tenantRepo, IUnitOfWork unitOfWork,
            Domain.BoundedContext.Radar.Repositories.IClientRepository radarClients,
            Domain.BoundedContext.Radar.Repositories.IMipymeReferralRepository mipymeReferrals,
            IRadarUnitOfWork radarUow)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
            _radarClients    = radarClients;
            _mipymeReferrals = mipymeReferrals;
            _radarUow        = radarUow;
        }

        public async Task Handle(AdminApproveTenantCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            tenant.Approve();
            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // La aprobación es el disparador principal de validación del referido de MiPyme
            // (mismo patrón que AdminApproveClientCommandHandler para referidos de clientes):
            // si este tenant se registró con un código de invitación de un cliente, esto lo
            // valida y acredita 7000 CUP al saldo unificado de quien lo invitó.
            await BusinessSearcher.Application.Features.Radar.Commands.MipymeReferralReward.ValidateAndRewardAsync(
                tenant.Id, _mipymeReferrals, _radarClients, DateTime.UtcNow, cancellationToken);
            await _radarUow.SaveChangesAsync(cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SUSPEND TENANT
    // ═══════════════════════════════════════════════════════════════
    public record AdminSuspendTenantCommand(Guid TenantId, string? Reason = null) : IRequest;

    public class AdminSuspendTenantCommandHandler : IRequestHandler<AdminSuspendTenantCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;

        public AdminSuspendTenantCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(AdminSuspendTenantCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            tenant.Suspend();
            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ACTIVATE TENANT
    // ═══════════════════════════════════════════════════════════════
    public record AdminActivateTenantCommand(Guid TenantId) : IRequest;

    public class AdminActivateTenantCommandHandler : IRequestHandler<AdminActivateTenantCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;

        public AdminActivateTenantCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(AdminActivateTenantCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            if (tenant.Status == Domain.BoundedContext.TenantManagement.Enums.TenantStatus.Active)
                throw new DomainException("El tenant ya está activo.");

            tenant.Activate();
            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // RENEW — registra un pago manual y extiende la suscripción 30 días (reactiva si estaba
    // Suspended/Inactive). Reusa Tenant.RegisterPayment tal cual, sin mecanismo paralelo.
    // ═══════════════════════════════════════════════════════════════
    public record AdminRenewTenantCommand(Guid TenantId, decimal Amount, string Currency = "COP", string? Reference = null) : IRequest;

    public class AdminRenewTenantCommandHandler : IRequestHandler<AdminRenewTenantCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;

        public AdminRenewTenantCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(AdminRenewTenantCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            tenant.RegisterPayment(request.Amount, request.Currency, request.Reference);
            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // DELETE (soft delete → Inactive)
    // ═══════════════════════════════════════════════════════════════
    public record AdminDeleteTenantCommand(Guid TenantId) : IRequest;

    public class AdminDeleteTenantCommandHandler : IRequestHandler<AdminDeleteTenantCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;

        public AdminDeleteTenantCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(AdminDeleteTenantCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            // Revoca todas las sesiones activas antes de desactivar
            tenant.RevokeAllRefreshTokens();
            tenant.Deactivate();
            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
