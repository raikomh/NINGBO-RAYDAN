using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Application.Features.TenantManagement.Commands
{
    // ═══════════════════════════════════════════════════════════════
    // VERIFY EMAIL  (link del email → activa la cuenta)
    // ═══════════════════════════════════════════════════════════════
    public record VerifyEmailCommand(string Token) : IRequest;

    public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
    {
        public VerifyEmailCommandValidator()
        {
            RuleFor(x => x.Token).NotEmpty().WithMessage("El token de verificación es requerido.");
        }
    }

    public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;

        public VerifyEmailCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
        {
            _tenantRepo = tenantRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByEmailVerificationTokenAsync(request.Token, cancellationToken)
                ?? throw new DomainException("El enlace de verificación es inválido o ya fue utilizado.");

            tenant.VerifyEmail(request.Token);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // RESEND VERIFICATION  (reenvía el email si expiró o no llegó)
    // Anónimo por email: el tenant aún no tiene sesión (no está verificado
    // ni aprobado), así que no puede autenticarse para pedir el reenvío.
    // ═══════════════════════════════════════════════════════════════
    public record ResendVerificationEmailCommand(string Email) : IRequest;

    public class ResendVerificationEmailCommandValidator : AbstractValidator<ResendVerificationEmailCommand>
    {
        public ResendVerificationEmailCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public class ResendVerificationEmailCommandHandler : IRequestHandler<ResendVerificationEmailCommand>
    {
        private readonly ITenantRepository     _tenantRepo;
        private readonly IUnitOfWork           _unitOfWork;
        private readonly IEmailService         _emailService;
        private readonly ISecureTokenGenerator _tokenGenerator;
        private readonly IConfiguration        _config;
        private readonly ILogger<ResendVerificationEmailCommandHandler> _logger;

        public ResendVerificationEmailCommandHandler(
            ITenantRepository     tenantRepo,
            IUnitOfWork           unitOfWork,
            IEmailService         emailService,
            ISecureTokenGenerator tokenGenerator,
            IConfiguration        config,
            ILogger<ResendVerificationEmailCommandHandler> logger)
        {
            _tenantRepo     = tenantRepo;
            _unitOfWork     = unitOfWork;
            _emailService   = emailService;
            _tokenGenerator = tokenGenerator;
            _config         = config;
            _logger         = logger;
        }

        public async Task Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
        {
            // Nunca revela si el email existe o ya está verificado (mismo patrón que ForgotPassword)
            var tenant = await _tenantRepo.GetByEmailAsync(request.Email, cancellationToken);
            if (tenant is null || tenant.IsEmailVerified)
                return;

            var rawToken = _tokenGenerator.Generate(32);
            var verifyToken = tenant.RequestEmailVerification(rawToken, TimeSpan.FromHours(24));

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var baseUrl = _config["Frontend:DashboardUrl"] ?? "https://app.businesssearcher.com";
            var link    = $"{baseUrl}/verify-email?token={verifyToken.Token}";

            try
            {
                await _emailService.SendEmailVerificationAsync(
                    tenant.Email.Value, tenant.BusinessName, link, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo reenviar el email de verificación a {Email}", tenant.Email.Value);
            }
        }
    }
}
