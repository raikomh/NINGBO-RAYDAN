using BusinessSearcher.Application.Commons;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.TenantManagement;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Application.Features.TenantManagement.Commands.RegisterTenant
{
    // ── Command ───────────────────────────────────────────────────────────────────
    public record RegisterTenantCommand(
        string BusinessName,
        string Email,
        string Password,
        string TenantType,
        string? ReferralCode = null) : IRequest<TenantDto>;

    // ── Validator ─────────────────────────────────────────────────────────────────
    public class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
    {
        public RegisterTenantCommandValidator()
        {
            RuleFor(x => x.BusinessName)
                .NotEmpty().WithMessage("El nombre del negocio es requerido.")
                .MinimumLength(3).WithMessage("El nombre debe tener al menos 3 caracteres.")
                .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("El email es requerido.")
                .EmailAddress().WithMessage("El email no tiene un formato válido.")
                .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("El email no tiene un formato válido.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("La contraseña es requerida.")
                .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
                .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una minúscula.")
                .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una mayúscula.")
                .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un número.")
                .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe contener al menos un carácter especial.");

            RuleFor(x => x.TenantType)
                .NotEmpty().WithMessage("Debes indicar si eres Mayorista o Minorista.")
                .Must(v => Enum.TryParse<TenantType>(v, true, out _))
                .WithMessage("El tipo de negocio debe ser 'Wholesale' (Mayorista) o 'Retail' (Minorista).");
        }
    }

    // ── Handler ───────────────────────────────────────────────────────────────────
    // El registro NO entrega JWT ni refresh token: el tenant queda pendiente de
    // aprobación por un admin y solo obtiene sesión iniciando sesión (Login) una vez aprobado.
    public class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, TenantDto>
    {
        private readonly ITenantRepository     _tenantRepo;
        private readonly IUnitOfWork           _unitOfWork;
        private readonly IPasswordHasher       _passwordHasher;
        private readonly ISecureTokenGenerator _tokenGenerator;
        private readonly ITenantSchemaManager  _schemaManager;
        private readonly IEmailService         _emailService;
        private readonly IConfiguration        _config;
        private readonly ILogger<RegisterTenantCommandHandler> _logger;
        // Programa de referidos cliente-refiere-MiPyme: vive en el bounded context Radar
        // (mismo patrón de acoplamiento suelto ya usado por Client.ReferredByClientId).
        private readonly Domain.BoundedContext.Radar.Repositories.IClientRepository _radarClients;
        private readonly Domain.BoundedContext.Radar.Repositories.IMipymeReferralRepository _mipymeReferrals;
        private readonly IRadarUnitOfWork _radarUow;

        public RegisterTenantCommandHandler(
            ITenantRepository     tenantRepo,
            IUnitOfWork           unitOfWork,
            IPasswordHasher       passwordHasher,
            ISecureTokenGenerator tokenGenerator,
            ITenantSchemaManager  schemaManager,
            IEmailService         emailService,
            IConfiguration        config,
            ILogger<RegisterTenantCommandHandler> logger,
            Domain.BoundedContext.Radar.Repositories.IClientRepository radarClients,
            Domain.BoundedContext.Radar.Repositories.IMipymeReferralRepository mipymeReferrals,
            IRadarUnitOfWork radarUow)
        {
            _tenantRepo     = tenantRepo;
            _unitOfWork     = unitOfWork;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
            _schemaManager  = schemaManager;
            _emailService   = emailService;
            _config         = config;
            _logger         = logger;
            _radarClients    = radarClients;
            _mipymeReferrals = mipymeReferrals;
            _radarUow        = radarUow;
        }

        public async Task<TenantDto> Handle(
            RegisterTenantCommand request, CancellationToken cancellationToken)
        {
            // 0. En modo Local la instalación admite un único negocio (lo crea el
            // dueño del sistema, no autorregistro múltiple como en el SaaS online).
            var isLocalDeployment = DeploymentMode.IsLocal(_config);
            if (isLocalDeployment && await _tenantRepo.CountAsync(cancellationToken) > 0)
                throw new ConflictException(
                    "Esta instalación local ya tiene un negocio registrado. Cada instalación admite un único negocio; agrega más usuarios del TPV desde la gestión de trabajadores.");

            // 1. Verificar email único
            if (await _tenantRepo.ExistsEmailAsync(request.Email, cancellationToken))
                throw new DomainException($"Ya existe una cuenta con el email '{request.Email}'.");

            // 2. Hashear contraseña
            var passwordHash = _passwordHasher.Hash(request.Password);

            // 2.1 Si vino con un código de invitación de un Client, resuelve al invitador
            // (mismo lookup case-insensitive que usa el registro de clientes). No falla el
            // registro si el código es inválido/no existe — simplemente se ignora.
            Domain.BoundedContext.Radar.Aggregates.Client? inviter = null;
            if (!string.IsNullOrWhiteSpace(request.ReferralCode))
                inviter = await _radarClients.GetByReferralCodeAsync(request.ReferralCode, cancellationToken);

            // 3. Crear Tenant (factory method del Aggregate) — queda con IsApproved = false
            var tenantType = Enum.Parse<TenantType>(request.TenantType, true);
            var tenant = Tenant.Create(request.BusinessName, request.Email, passwordHash, tenantType,
                referredByClientId: inviter?.Id);

            // El email configurado como admin (Chat:AdminEmail) se auto-aprueba: de lo
            // contrario nadie podría aprobar la primera cuenta admin (todavía no existe
            // un admin aprobado que lo haga). En modo Local no hay admin SaaS que apruebe
            // manualmente, así que el único negocio de la instalación también se autoaprueba.
            var adminEmail = _config["Chat:AdminEmail"] ?? "admin@businesssearcher.dev";
            var autoApproved = isLocalDeployment || tenant.Email.Value.Equals(adminEmail, StringComparison.OrdinalIgnoreCase);
            if (autoApproved)
                tenant.Approve();

            // 4. Persistir
            await _tenantRepo.AddAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 4.1 Registra el referido de MiPyme (pendiente) si se resolvió un invitador.
            if (inviter is not null)
            {
                await _mipymeReferrals.AddAsync(
                    Domain.BoundedContext.Radar.Aggregates.MipymeReferral.Create(inviter.Id, tenant.Id),
                    cancellationToken);
                await _radarUow.SaveChangesAsync(cancellationToken);
            }

            // 4.2 Si el tenant se auto-aprobó arriba, valida el referido de MiPyme de una vez
            // (mismo disparador dual que usa el programa de referidos de clientes: aprobación
            // de admin O auto-aprobación en el registro).
            if (autoApproved && inviter is not null)
            {
                await BusinessSearcher.Application.Features.Radar.Commands.MipymeReferralReward.ValidateAndRewardAsync(
                    tenant.Id, _mipymeReferrals, _radarClients, DateTime.UtcNow, cancellationToken);
                await _radarUow.SaveChangesAsync(cancellationToken);
            }

            // 5. Crear schema PostgreSQL para el tenant
            await _schemaManager.CreateSchemaAsync(tenant.Id, cancellationToken);

            // 6. Emitir token de verificación de email
            var rawVerifyToken  = _tokenGenerator.Generate(32);
            var verificationToken = tenant.RequestEmailVerification(rawVerifyToken, TimeSpan.FromHours(24));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var baseUrl = _config["Frontend:DashboardUrl"] ?? "https://app.businesssearcher.com";
            var verifyLink = $"{baseUrl}/verify-email?token={verificationToken.Token}";

            try
            {
                await _emailService.SendEmailVerificationAsync(
                    tenant.Email.Value, tenant.BusinessName, verifyLink, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el email de verificación a {Email}", tenant.Email.Value);
            }

            return MapToDto(tenant);
        }

        private static TenantDto MapToDto(Tenant t) => new(
            t.Id, t.BusinessName, t.Email.Value,
            t.Status.ToString(),
            t.IsSubscriptionActive(),
            t.CreatedAt, t.NextPaymentDate, t.GetTotalPaid(),
            t.Plan, t.Type.ToString());
    }
}

namespace BusinessSearcher.Application.Features.TenantManagement.Commands.LoginTenant
{
    // ── Command ───────────────────────────────────────────────────────────────────
    public record LoginTenantCommand(
        string Email,
        string Password,
        string? IpAddress = null,
        Domain.Identity.ClientPlatform Platform = Domain.Identity.ClientPlatform.Web) : IRequest<AuthResultDto>;

    // ── Validator ─────────────────────────────────────────────────────────────────
    public class LoginTenantCommandValidator : AbstractValidator<LoginTenantCommand>
    {
        public LoginTenantCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    // ── Handler ───────────────────────────────────────────────────────────────────
    public class LoginTenantCommandHandler : IRequestHandler<LoginTenantCommand, AuthResultDto>
    {
        private readonly ITenantRepository     _tenantRepo;
        private readonly IUnitOfWork           _unitOfWork;
        private readonly IPasswordHasher       _passwordHasher;
        private readonly IJwtTokenService      _jwtService;
        private readonly ISecureTokenGenerator _tokenGenerator;
        private readonly IConfiguration        _config;

        public LoginTenantCommandHandler(
            ITenantRepository     tenantRepo,
            IUnitOfWork           unitOfWork,
            IPasswordHasher       passwordHasher,
            IJwtTokenService      jwtService,
            ISecureTokenGenerator tokenGenerator,
            IConfiguration        config)
        {
            _tenantRepo     = tenantRepo;
            _unitOfWork     = unitOfWork;
            _passwordHasher = passwordHasher;
            _jwtService     = jwtService;
            _tokenGenerator = tokenGenerator;
            _config         = config;
        }

        public async Task<AuthResultDto> Handle(
            LoginTenantCommand request, CancellationToken cancellationToken)
        {
            // Mensaje genérico para no revelar si el email existe o no
            const string invalidCredentials = "Credenciales inválidas.";

            var tenant = await _tenantRepo.GetByEmailAsync(request.Email, cancellationToken)
                ?? throw new UnauthorizedAccessException(invalidCredentials);

            if (!_passwordHasher.Verify(request.Password, tenant.PasswordHash))
                throw new UnauthorizedAccessException(invalidCredentials);

            // Restricción por plataforma: una Tienda solo inicia sesión desde la web; el
            // Admin (Chat:AdminEmail) puede hacerlo desde la web y desde la app.
            var adminEmail = _config["Chat:AdminEmail"] ?? "admin@businesssearcher.dev";
            var role = tenant.Email.Value.Equals(adminEmail, StringComparison.OrdinalIgnoreCase)
                ? Domain.Identity.AccountRole.Admin
                : Domain.Identity.AccountRole.Store;
            if (!Domain.Identity.AccountRoleExtensions.CanLoginFrom(role, request.Platform))
                throw new DomainException("Las cuentas de tienda solo pueden iniciar sesión desde la web.");

            if (tenant.Status == TenantStatus.Suspended)
                throw new DomainException("Tu cuenta está suspendida. Contacta a soporte.");

            if (tenant.Status == TenantStatus.Deleted)
                throw new UnauthorizedAccessException(invalidCredentials);

            // Verificación de email deshabilitada temporalmente: el admin aprueba
            // manualmente cada cuenta (ver IsApproved) en vez de exigir el enlace de email.
            if (!tenant.IsApproved)
                throw new DomainException("Tu cuenta está pendiente de aprobación por un administrador. Te notificaremos por email cuando puedas iniciar sesión.");

            var refreshToken = _tokenGenerator.Generate();
            tenant.IssueRefreshToken(refreshToken, TimeSpan.FromDays(30), request.IpAddress);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var jwt = _jwtService.GenerateToken(
                tenant.Id, tenant.Email.Value, tenant.BusinessName, tenant.Plan, tenant.Type);

            return new AuthResultDto(jwt, new TenantDto(
                tenant.Id, tenant.BusinessName, tenant.Email.Value,
                tenant.Status.ToString(),
                tenant.IsSubscriptionActive(),
                tenant.CreatedAt, tenant.NextPaymentDate, tenant.GetTotalPaid(),
                tenant.Plan, tenant.Type.ToString()))
            { RefreshToken = refreshToken };
        }
    }
}

namespace BusinessSearcher.Application.Features.TenantManagement.Commands.UpdateProfile
{
    // ── Command ───────────────────────────────────────────────────────────────────
    public record UpdateTenantProfileCommand(string BusinessName) : IRequest;

    // ── Validator ─────────────────────────────────────────────────────────────────
    public class UpdateTenantProfileCommandValidator : AbstractValidator<UpdateTenantProfileCommand>
    {
        public UpdateTenantProfileCommandValidator()
        {
            RuleFor(x => x.BusinessName)
                .NotEmpty().WithMessage("El nombre del negocio es requerido.")
                .MinimumLength(3).WithMessage("El nombre debe tener al menos 3 caracteres.")
                .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");
        }
    }

    // ── Handler ───────────────────────────────────────────────────────────────────
    public class UpdateTenantProfileCommandHandler : IRequestHandler<UpdateTenantProfileCommand>
    {
        private readonly ITenantRepository   _tenantRepo;
        private readonly IUnitOfWork         _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public UpdateTenantProfileCommandHandler(
            ITenantRepository   tenantRepo,
            IUnitOfWork         unitOfWork,
            ICurrentUserService currentUser)
        {
            _tenantRepo  = tenantRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(UpdateTenantProfileCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            tenant.UpdateBusinessName(request.BusinessName);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace BusinessSearcher.Application.Features.TenantManagement.Commands.RegisterFcmToken
{
    // ── Command ───────────────────────────────────────────────────────────────────
    public record RegisterFcmTokenCommand(string Token) : IRequest;

    // ── Validator ─────────────────────────────────────────────────────────────────
    public class RegisterFcmTokenCommandValidator : AbstractValidator<RegisterFcmTokenCommand>
    {
        public RegisterFcmTokenCommandValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("El token FCM es requerido.")
                .MaximumLength(500);
        }
    }

    // ── Handler ───────────────────────────────────────────────────────────────────
    public class RegisterFcmTokenCommandHandler : IRequestHandler<RegisterFcmTokenCommand>
    {
        private readonly ITenantRepository   _tenantRepo;
        private readonly IUnitOfWork         _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public RegisterFcmTokenCommandHandler(
            ITenantRepository   tenantRepo,
            IUnitOfWork         unitOfWork,
            ICurrentUserService currentUser)
        {
            _tenantRepo  = tenantRepo;
            _unitOfWork  = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(RegisterFcmTokenCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepo.GetByIdAsync(_currentUser.TenantId, cancellationToken)
                ?? throw new DomainException("Tenant no encontrado.");

            tenant.RegisterFcmToken(request.Token);

            await _tenantRepo.UpdateAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
