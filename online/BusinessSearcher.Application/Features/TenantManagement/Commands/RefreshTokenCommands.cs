using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.TenantManagement;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.TenantManagement.Commands.RefreshToken
{
    // ═══════════════════════════════════════════════════════════════
    // REFRESH TOKEN — rota el refresh token y emite un nuevo JWT
    // ═══════════════════════════════════════════════════════════════
    public record RefreshTokenCommand(string RefreshToken, string? IpAddress = null) : IRequest<AuthResultDto>;

    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        public RefreshTokenCommandValidator()
        {
            RuleFor(x => x.RefreshToken).NotEmpty();
        }
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResultDto>
    {
        private readonly ITenantRepository      _tenantRepo;
        private readonly IUnitOfWork            _unitOfWork;
        private readonly IJwtTokenService       _jwtService;
        private readonly ISecureTokenGenerator  _tokenGenerator;

        public RefreshTokenCommandHandler(
            ITenantRepository     tenantRepo,
            IUnitOfWork           unitOfWork,
            IJwtTokenService      jwtService,
            ISecureTokenGenerator tokenGenerator)
        {
            _tenantRepo     = tenantRepo;
            _unitOfWork     = unitOfWork;
            _jwtService     = jwtService;
            _tokenGenerator = tokenGenerator;
        }

        public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken)
                ?? throw new DomainException("Refresh token inválido.");

            var newRefreshToken = _tokenGenerator.Generate();

            // Rota: revoca el actual (o invalida todo si hay reutilización) y emite uno nuevo
            tenant.RotateRefreshToken(
                request.RefreshToken, newRefreshToken,
                TimeSpan.FromDays(30), request.IpAddress);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var jwt = _jwtService.GenerateToken(
                tenant.Id, tenant.Email.Value, tenant.BusinessName, tenant.Plan, tenant.Type);

            var dto = new TenantDto(
                tenant.Id, tenant.BusinessName, tenant.Email.Value,
                tenant.Status.ToString(),
                tenant.IsSubscriptionActive(), tenant.CreatedAt,
                tenant.NextPaymentDate, tenant.GetTotalPaid(),
                tenant.Plan, tenant.Type.ToString());

            return new AuthResultDto(jwt, dto) { RefreshToken = newRefreshToken };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // LOGOUT — revoca un refresh token específico (cierra una sesión)
    // ═══════════════════════════════════════════════════════════════
    public record LogoutCommand(Guid TenantId, string RefreshToken) : IRequest;

    public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;

        public LogoutCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            // Logout idempotente: sin cookie de refresh token no hay nada que revocar,
            // y no debe ser un error (cerrar sesión siempre "tiene éxito").
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return;

            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken);
            if (tenant is null) return;

            // Si el token ya no existe o estaba revocado, tampoco es un error.
            try { tenant.RevokeRefreshToken(request.RefreshToken); }
            catch (DomainException) { return; }

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // LOGOUT ALL — cierra todas las sesiones (todos los dispositivos)
    // ═══════════════════════════════════════════════════════════════
    public record LogoutAllSessionsCommand(Guid TenantId) : IRequest;

    public class LogoutAllSessionsCommandHandler : IRequestHandler<LogoutAllSessionsCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;

        public LogoutAllSessionsCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(LogoutAllSessionsCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            tenant.RevokeAllRefreshTokens();

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
