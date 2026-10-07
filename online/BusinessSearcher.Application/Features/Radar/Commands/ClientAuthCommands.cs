using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using BusinessSearcher.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Application.Features.Radar.Commands.ClientAuth
{
    // ── Helpers ────────────────────────────────────────────────────────────────────
    internal static class ClientMapper
    {
        public static ClientDto ToDto(Client c) => new(
            c.Id, c.FullName, c.Email.Value, c.Plan.ToString(),
            c.IsEmailVerified, c.ReputationScore, c.ReportsSubmitted, c.CreatedAt, c.PremiumRequested,
            c.ReferralCupBalance, c.PhoneNumber, c.PremiumUntil,
            c.Street, c.City, c.State, c.Country, c.Latitude, c.Longitude);
    }

    // ── Register ─────────────────────────────────────────────────────────────────
    public record RegisterClientCommand(
        string FullName,
        string Email,
        string Password,
        ClientPlatform Platform,
        string? IpAddress = null,
        string? ReferralCode = null,
        string? PhoneNumber = null) : IRequest<ClientAuthResultDto>;

    public class RegisterClientCommandValidator : AbstractValidator<RegisterClientCommand>
    {
        public RegisterClientCommandValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("El nombre es requerido.")
                .MinimumLength(3).WithMessage("El nombre debe tener al menos 3 caracteres.")
                .MaximumLength(120).WithMessage("El nombre no puede exceder 120 caracteres.");

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
        }
    }

    public class RegisterClientCommandHandler : IRequestHandler<RegisterClientCommand, ClientAuthResultDto>
    {
        private readonly IClientRepository     _clients;
        private readonly IReferralRepository   _referrals;
        private readonly IRadarUnitOfWork      _uow;
        private readonly IPasswordHasher       _hasher;
        private readonly IEmailService         _email;
        private readonly ILogger<RegisterClientCommandHandler> _logger;

        public RegisterClientCommandHandler(
            IClientRepository clients, IReferralRepository referrals, IRadarUnitOfWork uow,
            IPasswordHasher hasher, IEmailService email, ILogger<RegisterClientCommandHandler> logger)
        {
            _clients = clients; _referrals = referrals; _uow = uow; _hasher = hasher;
            _email = email; _logger = logger;
        }

        public async Task<ClientAuthResultDto> Handle(RegisterClientCommand request, CancellationToken ct)
        {
            if (!AccountRole.Client.CanLoginFrom(request.Platform))
                throw new DomainException("El registro de clientes solo está disponible desde la app móvil.");

            if (await _clients.ExistsEmailAsync(request.Email, ct))
                throw new DomainException($"Ya existe una cuenta con el email '{request.Email}'.");

            var passwordHash = _hasher.Hash(request.Password);
            var client = Client.Create(request.FullName, request.Email, passwordHash);

            // La app ya captura el teléfono al registrar (para contactar por WhatsApp);
            // antes se enviaba y el servidor lo descartaba en silencio.
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                client.SetPhoneNumber(request.PhoneNumber);

            // Código de invitación propio, único.
            client.AssignReferralCode(await GenerateUniqueReferralCodeAsync(request.FullName, ct));

            // Si vino con un código de invitación válido, registra la relación (pendiente
            // hasta que este cliente pase a premium).
            if (!string.IsNullOrWhiteSpace(request.ReferralCode))
            {
                var inviter = await _clients.GetByReferralCodeAsync(request.ReferralCode, ct);
                if (inviter is not null && inviter.Id != client.Id)
                {
                    client.SetReferredBy(inviter.Id);
                    await _referrals.AddAsync(Referral.Create(inviter.Id, client.Id), ct);
                }
            }

            await _clients.AddAsync(client, ct);
            await _uow.SaveChangesAsync(ct);

            // El registro NO entrega JWT ni refresh token (igual que Tenant): la cuenta
            // queda pendiente de aprobación por un admin y solo obtiene sesión iniciando
            // sesión (Login) una vez aprobada.

            // Email de bienvenida en segundo plano: NO se espera. Enviarlo de forma
            // bloqueante colgaba el registro ~60 s cuando el SMTP no responde (Render
            // free bloquea los puertos SMTP de salida). El email es best-effort.
            var welcomeEmail = client.Email.Value;
            var welcomeName  = client.FullName;
            var emailService = _email;
            var log          = _logger;
            _ = Task.Run(async () =>
            {
                try { await emailService.SendWelcomeEmailAsync(welcomeEmail, welcomeName, CancellationToken.None); }
                catch (Exception ex) { log.LogWarning(ex, "No se pudo enviar el email de bienvenida a {Email}", welcomeEmail); }
            });

            return new ClientAuthResultDto(null, ClientMapper.ToDto(client));
        }

        /// <summary>Genera un código tipo "RAIKO42" a partir del nombre, único en la tabla.</summary>
        private async Task<string> GenerateUniqueReferralCodeAsync(string fullName, CancellationToken ct)
        {
            var letters = new string((fullName ?? "USER")
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .Take(5)
                .ToArray());
            if (letters.Length < 3) letters = "USER";

            var rnd = Random.Shared;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var code = $"{letters}{rnd.Next(10, 99)}";
                if (!await _clients.ExistsReferralCodeAsync(code, ct))
                    return code;
            }
            return $"{letters}{Guid.NewGuid():N}".Substring(0, 12).ToUpperInvariant();
        }
    }

    // ── Update profile (autoservicio: nombre, teléfono y dirección/ubicación) ──────
    public record UpdateClientProfileCommand(
        string? Street    = null,
        string? City      = null,
        string? State     = null,
        string? Country   = null,
        double? Latitude  = null,
        double? Longitude = null,
        string? FullName    = null,
        string? PhoneNumber = null) : IRequest<ClientDto>;

    public class UpdateClientProfileCommandHandler : IRequestHandler<UpdateClientProfileCommand, ClientDto>
    {
        private readonly IClientRepository   _clients;
        private readonly IRadarUnitOfWork    _uow;
        private readonly ICurrentUserService _currentUser;

        public UpdateClientProfileCommandHandler(
            IClientRepository clients, IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _clients = clients; _uow = uow; _currentUser = currentUser;
        }

        public async Task<ClientDto> Handle(UpdateClientProfileCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            client.SetAddress(request.Street, request.City, request.State, request.Country);

            if (request.Latitude.HasValue && request.Longitude.HasValue)
                client.SetLocation(request.Latitude.Value, request.Longitude.Value, request.City);

            // UpdateFullName y SetPhoneNumber lanzan si el valor viene vacío, así que
            // aquí un blanco se trata como "no tocar" en vez de como un borrado. El
            // teléfono es el canal de cobro por WhatsApp: no conviene poder vaciarlo.
            if (!string.IsNullOrWhiteSpace(request.FullName))
                client.UpdateFullName(request.FullName);

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                client.SetPhoneNumber(request.PhoneNumber);

            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);

            return ClientMapper.ToDto(client);
        }
    }

    // ── Login ────────────────────────────────────────────────────────────────────
    public record LoginClientCommand(
        string Email,
        string Password,
        ClientPlatform Platform,
        string? IpAddress = null) : IRequest<ClientAuthResultDto>;

    public class LoginClientCommandValidator : AbstractValidator<LoginClientCommand>
    {
        public LoginClientCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public class LoginClientCommandHandler : IRequestHandler<LoginClientCommand, ClientAuthResultDto>
    {
        private readonly IClientRepository     _clients;
        private readonly IRadarUnitOfWork      _uow;
        private readonly IPasswordHasher       _hasher;
        private readonly IJwtTokenService      _jwt;
        private readonly ISecureTokenGenerator _tokens;

        public LoginClientCommandHandler(
            IClientRepository clients, IRadarUnitOfWork uow, IPasswordHasher hasher,
            IJwtTokenService jwt, ISecureTokenGenerator tokens)
        {
            _clients = clients; _uow = uow; _hasher = hasher; _jwt = jwt; _tokens = tokens;
        }

        public async Task<ClientAuthResultDto> Handle(LoginClientCommand request, CancellationToken ct)
        {
            const string invalidCredentials = "Credenciales inválidas.";

            var client = await _clients.GetByEmailAsync(request.Email, ct)
                ?? throw new UnauthorizedAccessException(invalidCredentials);

            if (!_hasher.Verify(request.Password, client.PasswordHash))
                throw new UnauthorizedAccessException(invalidCredentials);

            // Restricción por plataforma: un Cliente solo puede iniciar sesión desde la app.
            if (!AccountRole.Client.CanLoginFrom(request.Platform))
                throw new DomainException("Esta cuenta de cliente solo puede iniciar sesión desde la app móvil.");

            if (!client.IsApproved)
                throw new DomainException("Tu cuenta está pendiente de aprobación por un administrador. Te notificaremos por email cuando puedas iniciar sesión.");

            var refreshToken = _tokens.Generate();
            client.IssueRefreshToken(refreshToken, TimeSpan.FromDays(30), request.IpAddress);
            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);

            var jwt = _jwt.GenerateClientToken(client.Id, client.Email.Value, client.FullName, client.Plan.ToString());
            return new ClientAuthResultDto(jwt, ClientMapper.ToDto(client)) { RefreshToken = refreshToken };
        }
    }

    // ── Refresh token ──────────────────────────────────────────────────────────────
    public record RefreshClientTokenCommand(string RefreshToken, string? IpAddress = null)
        : IRequest<ClientAuthResultDto>;

    public class RefreshClientTokenCommandHandler : IRequestHandler<RefreshClientTokenCommand, ClientAuthResultDto>
    {
        private readonly IClientRepository     _clients;
        private readonly IRadarUnitOfWork      _uow;
        private readonly IJwtTokenService      _jwt;
        private readonly ISecureTokenGenerator _tokens;

        public RefreshClientTokenCommandHandler(
            IClientRepository clients, IRadarUnitOfWork uow, IJwtTokenService jwt, ISecureTokenGenerator tokens)
        {
            _clients = clients; _uow = uow; _jwt = jwt; _tokens = tokens;
        }

        public async Task<ClientAuthResultDto> Handle(RefreshClientTokenCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByRefreshTokenAsync(request.RefreshToken, ct)
                ?? throw new DomainException("Refresh token inválido.");

            var newToken = _tokens.Generate();
            client.RotateRefreshToken(request.RefreshToken, newToken, TimeSpan.FromDays(30), request.IpAddress);
            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);

            var jwt = _jwt.GenerateClientToken(client.Id, client.Email.Value, client.FullName, client.Plan.ToString());
            return new ClientAuthResultDto(jwt, ClientMapper.ToDto(client)) { RefreshToken = newToken };
        }
    }

    // ── Logout ─────────────────────────────────────────────────────────────────────
    public record LogoutClientCommand(Guid ClientId, string RefreshToken) : IRequest;

    public class LogoutClientCommandHandler : IRequestHandler<LogoutClientCommand>
    {
        private readonly IClientRepository _clients;
        private readonly IRadarUnitOfWork  _uow;

        public LogoutClientCommandHandler(IClientRepository clients, IRadarUnitOfWork uow)
        {
            _clients = clients; _uow = uow;
        }

        public async Task Handle(LogoutClientCommand request, CancellationToken ct)
        {
            // Logout idempotente: sin refresh token (o ya revocado) no es un error.
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return;

            var client = await _clients.GetByIdAsync(request.ClientId, ct);
            if (client is null) return;

            try { client.RevokeRefreshToken(request.RefreshToken); }
            catch (DomainException) { return; }

            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }

    // ── Register FCM token ──────────────────────────────────────────────────────────
    public record RegisterClientFcmTokenCommand(Guid ClientId, string Token) : IRequest;

    public class RegisterClientFcmTokenCommandHandler : IRequestHandler<RegisterClientFcmTokenCommand>
    {
        private readonly IClientRepository _clients;
        private readonly IRadarUnitOfWork  _uow;

        public RegisterClientFcmTokenCommandHandler(IClientRepository clients, IRadarUnitOfWork uow)
        {
            _clients = clients; _uow = uow;
        }

        public async Task Handle(RegisterClientFcmTokenCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            client.RegisterFcmToken(request.Token);
            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.ClientProfile
{
    using BusinessSearcher.Application.Commons.Interfaces;
    using BusinessSearcher.Application.DTOs.Radar;
    using BusinessSearcher.Application.Features.Radar.Commands.ClientAuth;
    using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
    using BusinessSearcher.Domain.Exceptions;

    public record GetClientProfileQuery : IRequest<ClientDto>;

    public class GetClientProfileQueryHandler : IRequestHandler<GetClientProfileQuery, ClientDto>
    {
        private readonly IClientRepository   _clients;
        private readonly ICurrentUserService _currentUser;

        public GetClientProfileQueryHandler(IClientRepository clients, ICurrentUserService currentUser)
        {
            _clients = clients; _currentUser = currentUser;
        }

        public async Task<ClientDto> Handle(GetClientProfileQuery request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");
            return ClientMapper.ToDto(client);
        }
    }

    /// <summary>
    /// Perfil público de un cliente-vendedor (nombre/teléfono/dirección), visible para
    /// cualquier otro cliente autenticado que toque su producto en el buscador. Nunca
    /// expone email/password/plan/reputación.
    /// </summary>
    public record GetClientPublicProfileQuery(Guid ClientId) : IRequest<SellerPublicProfileDto>;

    public class GetClientPublicProfileQueryHandler : IRequestHandler<GetClientPublicProfileQuery, SellerPublicProfileDto>
    {
        private readonly IClientRepository _clients;

        public GetClientPublicProfileQueryHandler(IClientRepository clients) => _clients = clients;

        public async Task<SellerPublicProfileDto> Handle(GetClientPublicProfileQuery request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            return new SellerPublicProfileDto(
                client.FullName, client.PhoneNumber,
                client.Street, client.City, client.State, client.Country,
                client.Latitude, client.Longitude);
        }
    }
}
