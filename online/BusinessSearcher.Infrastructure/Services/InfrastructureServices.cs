using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Identity;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BusinessSearcher.Infrastructure.Services
{
    // ── JWT Service ───────────────────────────────────────────────────────────────
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _config;

        public JwtTokenService(IConfiguration config) => _config = config;

        public string GenerateToken(Guid tenantId, string email, string businessName, string plan, TenantType tenantType)
        {
            // Un Tenant es Store (mayorista/minorista → login web), salvo la cuenta admin
            // (email configurado en Chat:AdminEmail), que obtiene rol Admin (app y web).
            var adminEmail = _config["Chat:AdminEmail"] ?? "admin@businesssearcher.dev";
            var role = email.Equals(adminEmail, StringComparison.OrdinalIgnoreCase)
                ? AccountRole.Admin
                : AccountRole.Store;

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   tenantId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
                new Claim("role",         role.ToString()),
                new Claim("businessName", businessName),
                new Claim("plan",         plan),
                new Claim("tenantType",   tenantType.ToString())
            };
            return BuildToken(claims);
        }

        public string GenerateClientToken(Guid clientId, string email, string fullName, string plan)
        {
            // El rol de un Cliente consumidor es siempre Client (login solo desde la app).
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   clientId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
                new Claim("role",     AccountRole.Client.ToString()),
                new Claim("fullName", fullName),
                new Claim("plan",     plan)
            };
            return BuildToken(claims);
        }

        public string GenerateOperationsUserToken(Guid userId, Guid tenantId, string email, string name, OperationsRole opsRole)
        {
            // Sub-usuario del TPV: rol de cuenta Store (mismo pipeline que el dueño), pero el
            // TenantId real viaja en el claim "tenantId" (sub = id del propio usuario) y su rol
            // operativo en "opsRole".
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
                new Claim("role",         AccountRole.Store.ToString()),
                new Claim("tenantId",     tenantId.ToString()),
                new Claim("opsRole",      opsRole.ToString()),
                new Claim("businessName", name),
                new Claim("plan",         string.Empty),
                new Claim("tenantType",   TenantType.Retail.ToString())
            };
            return BuildToken(claims);
        }

        private string BuildToken(IEnumerable<Claim> claims)
        {
            var secret  = _config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret no configurado.");
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:ExpirationDays"] ?? "30"));

            var token = new JwtSecurityToken(
                issuer:             _config["Jwt:Issuer"],
                audience:           _config["Jwt:Audience"],
                claims:             claims,
                expires:            expires,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public bool ValidateToken(string token)
        {
            try
            {
                var secret = _config["Jwt:Secret"]!;
                var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
                new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = key,
                    ValidateIssuer           = true,
                    ValidIssuer              = _config["Jwt:Issuer"],
                    ValidateAudience         = true,
                    ValidAudience            = _config["Jwt:Audience"],
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero
                }, out _);
                return true;
            }
            catch { return false; }
        }
    }

    // ── Password Hasher (BCrypt) ──────────────────────────────────────────────────
    public class PasswordHasher : IPasswordHasher
    {
        public string Hash(string password)   => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        public bool   Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
    }

    // ── CurrentUserService (extrae claims del JWT) ────────────────────────────────
    public class CurrentUserService : ICurrentUserService
    {
        public Guid   TenantId        { get; }
        public string Email           { get; }
        public string BusinessName    { get; }
        public string Plan            { get; }
        public TenantType TenantType  { get; }
        public bool   IsAuthenticated { get; }
        public Guid        AccountId  { get; }
        public AccountRole Role       { get; }
        public OperationsRole? OpsRole { get; }

        public CurrentUserService(IHttpContextAccessor accessor)
        {
            var user = accessor.HttpContext?.User;
            IsAuthenticated = user?.Identity?.IsAuthenticated ?? false;

            if (IsAuthenticated)
            {
                var sub = user!.FindFirstValue(JwtRegisteredClaimNames.Sub);
                AccountId    = sub is not null && Guid.TryParse(sub, out var id) ? id : Guid.Empty;
                Role         = Enum.TryParse<AccountRole>(user.FindFirstValue("role"), true, out var r)
                    ? r : AccountRole.Store;
                // Un sub-usuario del TPV lleva el TenantId real en el claim "tenantId" (su sub es
                // su propio id). El dueño del negocio no lleva ese claim → TenantId = sub.
                var tenantClaim = user.FindFirstValue("tenantId");
                TenantId     = tenantClaim is not null && Guid.TryParse(tenantClaim, out var tid)
                    ? tid
                    : (Role == AccountRole.Client ? Guid.Empty : AccountId);
                Email        = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty;
                BusinessName = user.FindFirstValue("businessName")                ?? string.Empty;
                Plan         = user.FindFirstValue("plan")                        ?? string.Empty;
                TenantType   = Enum.TryParse<TenantType>(user.FindFirstValue("tenantType"), true, out var t)
                    ? t : TenantType.Retail;
                // Rol operativo: claim explícito (sub-usuario) o Administrador si es el dueño del negocio.
                if (Enum.TryParse<OperationsRole>(user.FindFirstValue("opsRole"), true, out var ops))
                    OpsRole = ops;
                else if (Role is AccountRole.Store or AccountRole.Admin)
                    OpsRole = OperationsRole.Administrador;
                else
                    OpsRole = null;
            }
            else
            {
                AccountId    = Guid.Empty;
                Role         = AccountRole.Client;
                TenantId     = Guid.Empty;
                Email        = string.Empty;
                BusinessName = string.Empty;
                Plan         = string.Empty;
                TenantType   = TenantType.Retail;
                OpsRole      = null;
            }
        }
    }

    // ── DateTime Service ──────────────────────────────────────────────────────────
    public class DateTimeService : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime Now    => DateTime.Now;
    }

    // ── SignalR Hub ───────────────────────────────────────────────────────────────
    public interface IAvailabilityClient
    {
        Task ProductAvailabilityChanged(AvailabilityNotification notification);
    }

    public record AvailabilityNotification(
        Guid     ProductId,
        Guid     StoreId,
        string   StoreName,
        string   ProductName,
        bool     IsAvailable,
        DateTime ChangedAt);

    public class AvailabilityHub : Hub<IAvailabilityClient>
    {
        public async Task SubscribeToCity(string city)
            => await Groups.AddToGroupAsync(Context.ConnectionId, $"city:{city.ToLowerInvariant()}");

        public async Task SubscribeToStore(string storeId)
            => await Groups.AddToGroupAsync(Context.ConnectionId, $"store:{storeId}");

        public async Task UnsubscribeFromCity(string city)
            => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"city:{city.ToLowerInvariant()}");
    }

    // ── Availability Notifier (SignalR) ───────────────────────────────────────────
    public class SignalRAvailabilityNotifier : IAvailabilityNotifier
    {
        private readonly IHubContext<AvailabilityHub, IAvailabilityClient> _hub;
        private readonly ILogger<SignalRAvailabilityNotifier> _logger;

        public SignalRAvailabilityNotifier(
            IHubContext<AvailabilityHub, IAvailabilityClient> hub,
            ILogger<SignalRAvailabilityNotifier> logger)
        {
            _hub    = hub;
            _logger = logger;
        }

        public async Task NotifyProductAvailabilityChangedAsync(
            Guid productId, Guid storeId, string storeName,
            string productName, bool isAvailable,
            CancellationToken cancellationToken = default)
        {
            var notification = new AvailabilityNotification(
                productId, storeId, storeName, productName, isAvailable, DateTime.UtcNow);

            // Notifica a todos los suscriptores de la tienda
            await _hub.Clients.Group($"store:{storeId}").ProductAvailabilityChanged(notification);

            // Notifica a todos los clientes conectados globalmente
            await _hub.Clients.All.ProductAvailabilityChanged(notification);

            _logger.LogInformation(
                "Disponibilidad cambiada — Producto: {Product}, Disponible: {Available}",
                productName, isAvailable);
        }
    }

    // ── Chat Hub (SignalR) ────────────────────────────────────────────────────────
    public interface IChatClient
    {
        Task ReceiveMessage(ChatMessageNotification notification);
    }

    public record ChatMessageNotification(
        Guid     TenantId,
        string   SenderName,
        string   Text,
        bool     IsFromAdmin,
        DateTime SentAt);

    public class ChatHub : Hub<IChatClient>
    {
        public async Task JoinConversation(string tenantId)
            => await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:tenant:{tenantId}");

        public async Task JoinAdminRoom()
            => await Groups.AddToGroupAsync(Context.ConnectionId, "chat:admin");
    }

    public class SignalRChatNotifier : IChatNotifier
    {
        private readonly IHubContext<ChatHub, IChatClient> _hub;
        private readonly ILogger<SignalRChatNotifier>      _logger;

        public SignalRChatNotifier(
            IHubContext<ChatHub, IChatClient> hub,
            ILogger<SignalRChatNotifier>      logger)
        {
            _hub    = hub;
            _logger = logger;
        }

        public async Task NotifyNewMessageAsync(
            Guid tenantId, string senderName, string text,
            bool isFromAdmin, DateTime sentAt,
            CancellationToken cancellationToken = default)
        {
            var notification = new ChatMessageNotification(tenantId, senderName, text, isFromAdmin, sentAt);

            if (isFromAdmin)
                await _hub.Clients.Group($"chat:tenant:{tenantId}").ReceiveMessage(notification);
            else
                await _hub.Clients.Group("chat:admin").ReceiveMessage(notification);

            _logger.LogInformation("Chat — {Sender} → TenantId:{Tenant}", senderName, tenantId);
        }
    }

    // ── Radar Hub (SignalR) ───────────────────────────────────────────────────────
    public interface IRadarClient
    {
        Task ReportPosted(ReportDto report);
    }

    public class RadarHub : Hub<IRadarClient>
    {
        /// <summary>El cliente se suscribe a los reportes de una ciudad.</summary>
        public Task SubscribeToCity(string city)
            => Groups.AddToGroupAsync(Context.ConnectionId, $"radar:city:{city.ToLowerInvariant()}");

        public Task UnsubscribeFromCity(string city)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"radar:city:{city.ToLowerInvariant()}");
    }

    public class SignalRRadarNotifier : IRadarNotifier
    {
        private readonly IHubContext<RadarHub, IRadarClient> _hub;
        private readonly ILogger<SignalRRadarNotifier>       _logger;

        public SignalRRadarNotifier(
            IHubContext<RadarHub, IRadarClient> hub,
            ILogger<SignalRRadarNotifier>       logger)
        {
            _hub    = hub;
            _logger = logger;
        }

        public async Task BroadcastNewReportAsync(ReportDto report, CancellationToken cancellationToken = default)
        {
            // Suscriptores de la ciudad del reporte…
            await _hub.Clients.Group($"radar:city:{report.City.ToLowerInvariant()}").ReportPosted(report);
            // …y cualquier cliente conectado al radar global.
            await _hub.Clients.All.ReportPosted(report);

            _logger.LogInformation(
                "Radar — nuevo reporte {Product} en {City} ({Place})",
                report.ProductName, report.City, report.PlaceName);
        }
    }

    // ── Firebase FCM ─────────────────────────────────────────────────────────────
    public class FcmNotificationService : IFcmNotificationService
    {
        private readonly ILogger<FcmNotificationService> _logger;

        public FcmNotificationService(IConfiguration config, ILogger<FcmNotificationService> logger)
        {
            _logger = logger;
            if (FirebaseApp.DefaultInstance is null)
            {
                var keyPath = config["Firebase:ServiceAccountKeyPath"];
                if (!string.IsNullOrEmpty(keyPath) && File.Exists(keyPath))
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(keyPath)
                    });
                }
                else
                {
                    _logger.LogWarning("Firebase no configurado. ServiceAccountKeyPath no encontrado.");
                }
            }
        }

        public async Task SendToTokenAsync(string token, string title, string body,
            Dictionary<string, string>? data = null, CancellationToken cancellationToken = default)
        {
            if (FirebaseApp.DefaultInstance is null) return;
            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(new Message
                {
                    Token        = token,
                    Notification = new Notification { Title = title, Body = body },
                    Data         = data
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando notificación FCM a token {Token}", token);
            }
        }

        public async Task SendToTopicAsync(string topic, string title, string body,
            Dictionary<string, string>? data = null, CancellationToken cancellationToken = default)
        {
            if (FirebaseApp.DefaultInstance is null) return;
            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(new Message
                {
                    Topic        = topic,
                    Notification = new Notification { Title = title, Body = body },
                    Data         = data
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando notificación FCM al topic {Topic}", topic);
            }
        }
    }
}
