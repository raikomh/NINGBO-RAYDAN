using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Application.Features.TenantManagement.Commands.ForgotPassword
{
    // ═══════════════════════════════════════════════════════════════
    // FORGOT PASSWORD — genera un token y envía el email de recuperación
    // ═══════════════════════════════════════════════════════════════
    public record ForgotPasswordCommand(string Email) : IRequest;

    public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
    {
        public ForgotPasswordCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
    {
        private readonly ITenantRepository     _tenantRepo;
        private readonly IUnitOfWork           _unitOfWork;
        private readonly ISecureTokenGenerator _tokenGenerator;
        private readonly IEmailService         _emailService;
        private readonly IConfiguration        _config;
        private readonly ILogger<ForgotPasswordCommandHandler> _logger;

        public ForgotPasswordCommandHandler(
            ITenantRepository     tenantRepo,
            IUnitOfWork           unitOfWork,
            ISecureTokenGenerator tokenGenerator,
            IEmailService         emailService,
            IConfiguration        config,
            ILogger<ForgotPasswordCommandHandler> logger)
        {
            _tenantRepo     = tenantRepo;
            _unitOfWork     = unitOfWork;
            _tokenGenerator = tokenGenerator;
            _emailService   = emailService;
            _config         = config;
            _logger         = logger;
        }

        public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByEmailAsync(request.Email, cancellationToken);

            // CORRECCIÓN DE SEGURIDAD: nunca revelar si el email existe o no.
            // Si no existe, simplemente no se hace nada pero se retorna éxito igual.
            if (tenant is null)
            {
                _logger.LogInformation("Solicitud de reset de password para email no registrado: {Email}", request.Email);
                return;
            }

            var resetToken = _tokenGenerator.Generate();
            tenant.RequestPasswordReset(resetToken, TimeSpan.FromMinutes(60));

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var frontendUrl = _config["Frontend:ResetPasswordUrl"] ?? "https://app.businesssearcher.com/reset-password";
            var resetLink   = $"{frontendUrl}?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(tenant.Email.Value)}";

            await _emailService.SendPasswordResetEmailAsync(
                tenant.Email.Value, tenant.BusinessName, resetLink, cancellationToken);
        }
    }
}

namespace BusinessSearcher.Application.Features.TenantManagement.Commands.ResetPassword
{
    // ═══════════════════════════════════════════════════════════════
    // RESET PASSWORD — usa el token recibido por email para cambiar la contraseña
    // ═══════════════════════════════════════════════════════════════
    public record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest;

    public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
    {
        public ResetPasswordCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.NewPassword)
                .NotEmpty().MinimumLength(8)
                .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una minúscula.")
                .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una mayúscula.")
                .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un número.")
                .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe contener al menos un carácter especial.");
        }
    }

    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
    {
        private readonly ITenantRepository _tenantRepo;
        private readonly IUnitOfWork       _unitOfWork;
        private readonly IPasswordHasher   _passwordHasher;
        private readonly IEmailService     _emailService;

        public ResetPasswordCommandHandler(
            ITenantRepository tenantRepo, IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher, IEmailService emailService)
        {
            _tenantRepo     = tenantRepo;
            _unitOfWork     = unitOfWork;
            _passwordHasher = passwordHasher;
            _emailService   = emailService;
        }

        public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByPasswordResetTokenAsync(request.Token, cancellationToken)
                ?? throw new DomainException("Token de recuperación inválido o expirado.");

            if (!tenant.Email.Value.Equals(request.Email.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                throw new DomainException("Token de recuperación inválido.");

            var newHash = _passwordHasher.Hash(request.NewPassword);
            tenant.ResetPassword(request.Token, newHash);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _emailService.SendPasswordChangedNotificationAsync(
                tenant.Email.Value, tenant.BusinessName, cancellationToken);
        }
    }
}

namespace BusinessSearcher.Application.Features.TenantManagement.Commands.ChangePassword
{
    // ═══════════════════════════════════════════════════════════════
    // CHANGE PASSWORD — usuario autenticado cambia su contraseña conociendo la actual
    // ═══════════════════════════════════════════════════════════════
    public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest;

    public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
    {
        public ChangePasswordCommandValidator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty();
            RuleFor(x => x.NewPassword)
                .NotEmpty().MinimumLength(8)
                .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una minúscula.")
                .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una mayúscula.")
                .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un número.")
                .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe contener al menos un carácter especial.")
                .NotEqual(x => x.CurrentPassword).WithMessage("La nueva contraseña debe ser diferente a la actual.");
        }
    }

    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
    {
        private readonly ITenantRepository    _tenantRepo;
        private readonly IUnitOfWork          _unitOfWork;
        private readonly IPasswordHasher      _passwordHasher;
        private readonly ICurrentUserService  _currentUser;
        private readonly IEmailService        _emailService;

        public ChangePasswordCommandHandler(
            ITenantRepository tenantRepo, IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher, ICurrentUserService currentUser,
            IEmailService emailService)
        {
            _tenantRepo     = tenantRepo;
            _unitOfWork     = unitOfWork;
            _passwordHasher = passwordHasher;
            _currentUser    = currentUser;
            _emailService   = emailService;
        }

        public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            var newHash = _passwordHasher.Hash(request.NewPassword);
            tenant.ChangePassword(request.CurrentPassword, newHash, _passwordHasher.Verify);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _emailService.SendPasswordChangedNotificationAsync(
                tenant.Email.Value, tenant.BusinessName, cancellationToken);
        }
    }
}
