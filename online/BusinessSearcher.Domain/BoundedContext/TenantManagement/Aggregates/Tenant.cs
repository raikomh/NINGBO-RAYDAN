using BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.ValueObjects;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates
{
    public class Tenant : Entity, IAggregateRoot
    {
        public string BusinessName { get; private set; } = default!;
        public Email Email { get; private set; } = default!;

        public string PasswordHash { get; private set; } = default!;

        public string Plan { get; private set; } = "Standard";

        public TenantStatus Status { get; private set; }

        public TenantType Type { get; private set; }

        // CORRECCIÓN: FcmToken para notificaciones push
        public string? FcmToken { get; private set; }

        // Número de contacto del negocio (WhatsApp): se captura la primera vez que el tenant
        // reporta un pago (ver SubmitPaymentClaim) y se reutiliza como valor por defecto luego.
        public string? PhoneNumber { get; private set; }

        public bool IsEmailVerified { get; private set; }

        // Un tenant recién registrado no puede iniciar sesión hasta que un admin lo apruebe
        public bool IsApproved { get; private set; }

        public DateTime? LastPaymentDate { get; private set; }
        public DateTime? NextPaymentDate { get; private set; }

        // Cursor de sincronización (solo aplica a instalaciones en modo Local): marca
        // hasta qué momento ya se subieron los datos operativos al backend online.
        public DateTime? LastSyncedAt { get; private set; }

        // Hash (SHA-256, sin sal — ver SyncApiKeyHasher) de la API key de sincronización de este
        // tenant online. Se genera/reemplaza desde su propia cuenta (autoservicio, JWT normal);
        // el valor en claro nunca se persiste, solo se devuelve una vez al generarlo. La
        // instalación Local correspondiente la usa en el header X-Sync-Api-Key para ingest/export.
        public string? SyncApiKeyHash { get; private set; }

        /// <summary>
        /// Id del Client (bounded context Radar) cuyo código de referido usó esta MiPyme al
        /// registrarse, si lo hizo. Guid suelto sin navegación cruzada — mismo patrón de
        /// desacoplamiento entre bounded contexts que ya usa el resto del código (p. ej.
        /// AvailabilityReport.StoreId). Null si no se registró con ningún código.
        /// </summary>
        public Guid? ReferredByClientId { get; private set; }

        private readonly List<TenantPayment> _payments = new();
        public IReadOnlyCollection<TenantPayment> Payments => _payments.AsReadOnly();

        private readonly List<PaymentClaim> _paymentClaims = new();
        public IReadOnlyCollection<PaymentClaim> PaymentClaims => _paymentClaims.AsReadOnly();

        private readonly List<RefreshToken> _refreshTokens = new();
        public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

        private readonly List<PasswordResetToken> _passwordResetTokens = new();
        public IReadOnlyCollection<PasswordResetToken> PasswordResetTokens => _passwordResetTokens.AsReadOnly();

        private readonly List<EmailVerificationToken> _emailVerificationTokens = new();
        public IReadOnlyCollection<EmailVerificationToken> EmailVerificationTokens => _emailVerificationTokens.AsReadOnly();

        private Tenant() { }

        public static Tenant Create(
            string businessName, string email, string passwordHash, TenantType type,
            Guid? referredByClientId = null)
        {
            if (string.IsNullOrWhiteSpace(businessName))
                throw new DomainException("El nombre del negocio no puede estar vacío.");
            if (businessName.Length < 3)
                throw new DomainException("El nombre del negocio debe tener al menos 3 caracteres.");
            if (businessName.Length > 150)
                throw new DomainException("El nombre del negocio no puede exceder 150 caracteres.");
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new DomainException("El hash de contraseña es requerido.");

            var tenant = new Tenant
            {
                BusinessName       = businessName.Trim(),
                Email              = Email.Create(email),
                PasswordHash       = passwordHash,
                Status             = TenantStatus.Trial,
                Type               = type,
                NextPaymentDate    = DateTime.UtcNow.AddDays(30),
                IsApproved         = false,
                ReferredByClientId = referredByClientId
            };

            tenant.AddDomainEvent(new TenantRegisteredEvent(tenant.Id, tenant.BusinessName, tenant.Email.Value));
            return tenant;
        }

        public void RegisterPayment(decimal amount, string currency = "COP", string? reference = null)
        {
            var payment = new TenantPayment(Id, amount, currency, reference);
            _payments.Add(payment);
            LastPaymentDate = payment.PaymentDate;
            NextPaymentDate = payment.PaymentDate.AddDays(30);

            if (Status == TenantStatus.Suspended || Status == TenantStatus.Inactive)
                Status = TenantStatus.Active;

            SetUpdated();
            AddDomainEvent(new PaymentRecordedEvent(Id, amount, payment.PaymentDate));
        }

        public void Activate()
        {
            if (Status == TenantStatus.Active)
                throw new DomainException("El tenant ya está activo.");
            Status = TenantStatus.Active;
            SetUpdated();
        }

        // CORRECCIÓN: "Desactive" → "Deactivate" (convención inglés)
        public void Deactivate()
        {
            if (Status == TenantStatus.Inactive)
                throw new DomainException("El tenant ya está inactivo.");
            Status = TenantStatus.Inactive;
            SetUpdated();
            AddDomainEvent(new TenantDeactivatedEvent(Id));
        }

        public void Suspend()
        {
            Status = TenantStatus.Suspended;
            SetUpdated();
        }

        public void Approve()
        {
            if (IsApproved)
                throw new DomainException("El tenant ya está aprobado.");
            IsApproved = true;
            SetUpdated();
            AddDomainEvent(new TenantApprovedEvent(Id, Email.Value));
        }

        public void UpdateBusinessName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new DomainException("El nombre del negocio no puede estar vacío.");
            var old = BusinessName;
            BusinessName = newName.Trim();
            SetUpdated();
            AddDomainEvent(new TenantUpdatedEvent(Id, old, BusinessName));
        }

        /// <summary>Registra el momento hasta el cual ya se sincronizaron los datos con el backend online.</summary>
        public void MarkSynced(DateTime syncedAt)
        {
            LastSyncedAt = syncedAt;
            SetUpdated();
        }

        public void RegisterFcmToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new DomainException("El token FCM no puede estar vacío.");
            FcmToken = token;
            SetUpdated();
        }

        public void SetSyncApiKeyHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                throw new DomainException("El hash de la API key de sincronización no puede estar vacío.");
            SyncApiKeyHash = hash;
            SetUpdated();
        }

        public void RevokeSyncApiKey()
        {
            SyncApiKeyHash = null;
            SetUpdated();
        }

        public bool IsSubscriptionActive()
        {
            if (Status != TenantStatus.Active && Status != TenantStatus.Trial)
                return false;
            if (!NextPaymentDate.HasValue)
                return true;
            return NextPaymentDate.Value > DateTime.UtcNow;
        }

        public decimal GetTotalPaid() => _payments.Sum(p => p.Amount);

        /// <summary>
        /// True si ya pasó 1 mes calendario completo desde el último pago registrado.
        /// Usa aritmética de meses reales (AddMonths), no 30 días fijos: respeta que un
        /// mes tenga 28, 29, 30 o 31 días (incl. años bisiestos). Tenants que nunca
        /// pagaron (solo Trial) no cuentan como "atrasados" por esta regla.
        /// </summary>
        public bool IsPaymentOverdue(DateTime now)
            => LastPaymentDate.HasValue && now >= LastPaymentDate.Value.AddMonths(1);

        // ── TELÉFONO Y REPORTES DE PAGO (autoservicio) ──────────────────────────────

        public void SetPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new DomainException("El número de teléfono no puede estar vacío.");
            PhoneNumber = phoneNumber.Trim();
            SetUpdated();
        }

        /// <summary>
        /// El propio tenant reporta un pago manual (teléfono + monto + comprobante) para que un
        /// admin lo revise. Queda "Pendiente" hasta que se apruebe o rechace — no extiende la
        /// suscripción por sí sola (eso solo pasa al aprobar, ver ApprovePaymentClaim).
        /// </summary>
        public PaymentClaim SubmitPaymentClaim(string phoneNumber, decimal amount, string currency, string proofReference)
        {
            if (_paymentClaims.Any(c => c.Status == PaymentClaimStatus.Pending))
                throw new DomainException("Ya tienes un reporte de pago pendiente de revisión.");

            SetPhoneNumber(phoneNumber);

            var claim = new PaymentClaim(Id, phoneNumber, amount, currency, proofReference);
            _paymentClaims.Add(claim);
            SetUpdated();
            return claim;
        }

        /// <summary>Aprueba un reporte de pago: registra el pago real (reusa RegisterPayment, sin mecanismo paralelo) y extiende la suscripción 30 días.</summary>
        public PaymentClaim ApprovePaymentClaim(Guid claimId, string? note = null)
        {
            var claim = _paymentClaims.FirstOrDefault(c => c.Id == claimId)
                ?? throw new DomainException("Reporte de pago no encontrado.");

            claim.Approve(note);
            RegisterPayment(claim.Amount, claim.Currency, claim.ProofReference);
            return claim;
        }

        public PaymentClaim RejectPaymentClaim(Guid claimId, string? note = null)
        {
            var claim = _paymentClaims.FirstOrDefault(c => c.Id == claimId)
                ?? throw new DomainException("Reporte de pago no encontrado.");

            claim.Reject(note);
            SetUpdated();
            return claim;
        }

        // ── REFRESH TOKENS ───────────────────────────────────────────────────────

        public RefreshToken IssueRefreshToken(string token, TimeSpan lifetime, string? createdByIp = null)
        {
            var refreshToken = new RefreshToken(Id, token, DateTime.UtcNow.Add(lifetime), createdByIp);
            _refreshTokens.Add(refreshToken);
            SetUpdated();
            return refreshToken;
        }

        /// <summary>
        /// Rota un refresh token: revoca el actual y emite uno nuevo.
        /// Si el token presentado ya estaba revocado, asume robo/reutilización y revoca TODOS los tokens activos.
        /// </summary>
        public RefreshToken RotateRefreshToken(string currentToken, string newToken, TimeSpan lifetime, string? createdByIp = null)
        {
            var existing = _refreshTokens.FirstOrDefault(t => t.Token == currentToken)
                ?? throw new DomainException("Refresh token inválido.");

            if (!existing.IsActive)
            {
                // Posible robo de token: invalidar toda la sesión por seguridad
                foreach (var rt in _refreshTokens.Where(t => t.IsActive))
                    rt.Revoke();
                SetUpdated();
                throw new DomainException("Refresh token inválido o reutilizado (posible reutilización detectada). Por seguridad, todas tus sesiones fueron cerradas. Vuelve a iniciar sesión.");
            }

            existing.Revoke(newToken);
            var rotated = IssueRefreshToken(newToken, lifetime, createdByIp);
            return rotated;
        }

        public void RevokeRefreshToken(string token)
        {
            var existing = _refreshTokens.FirstOrDefault(t => t.Token == token)
                ?? throw new DomainException("Refresh token no encontrado.");
            if (existing.IsActive)
                existing.Revoke();
            SetUpdated();
        }

        public void RevokeAllRefreshTokens()
        {
            foreach (var rt in _refreshTokens.Where(t => t.IsActive))
                rt.Revoke();
            SetUpdated();
        }

        // ── PASSWORD RESET ───────────────────────────────────────────────────────

        public PasswordResetToken RequestPasswordReset(string token, TimeSpan lifetime)
        {
            // Invalida solicitudes anteriores no usadas
            foreach (var old in _passwordResetTokens.Where(t => t.IsValid))
                old.MarkAsUsed();

            var resetToken = new PasswordResetToken(Id, token, DateTime.UtcNow.Add(lifetime));
            _passwordResetTokens.Add(resetToken);
            SetUpdated();
            AddDomainEvent(new PasswordResetRequestedEvent(Id, Email.Value, token));
            return resetToken;
        }

        public void ResetPassword(string token, string newPasswordHash)
        {
            var resetToken = _passwordResetTokens.FirstOrDefault(t => t.Token == token)
                ?? throw new DomainException("Token de recuperación inválido.");

            resetToken.MarkAsUsed();
            PasswordHash = newPasswordHash;

            // Por seguridad: cerrar todas las sesiones activas al cambiar la contraseña
            RevokeAllRefreshTokens();

            SetUpdated();
            AddDomainEvent(new PasswordChangedEvent(Id));
        }

        public void ChangePassword(string currentPassword, string newPasswordHash, Func<string, string, bool> verifyPasswordFn)
        {
            if (!verifyPasswordFn(currentPassword, PasswordHash))
                throw new DomainException("La contraseña actual no es correcta.");

            PasswordHash = newPasswordHash;

            // Por seguridad: cerrar todas las sesiones activas al cambiar la contraseña
            RevokeAllRefreshTokens();

            SetUpdated();
            AddDomainEvent(new PasswordChangedEvent(Id));
        }

        // ── EMAIL VERIFICATION ────────────────────────────────────────────────────

        public EmailVerificationToken RequestEmailVerification(string token, TimeSpan lifetime)
        {
            foreach (var old in _emailVerificationTokens.Where(t => t.IsValid))
                old.MarkAsUsed();

            var verifyToken = new EmailVerificationToken(Id, token, DateTime.UtcNow.Add(lifetime));
            _emailVerificationTokens.Add(verifyToken);
            SetUpdated();
            return verifyToken;
        }

        public void VerifyEmail(string token)
        {
            var verifyToken = _emailVerificationTokens.FirstOrDefault(t => t.Token == token)
                ?? throw new DomainException("El enlace de verificación es inválido.");

            verifyToken.MarkAsUsed();
            IsEmailVerified = true;
            SetUpdated();
            AddDomainEvent(new EmailVerifiedEvent(Id, Email.Value));
        }
    }

    // ── Domain Events (solo en el Aggregate, no duplicar en Domain/Events/) ──
    public record TenantRegisteredEvent(Guid TenantId, string BusinessName, string Email) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record TenantUpdatedEvent(Guid TenantId, string OldBusinessName, string NewBusinessName) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record PaymentRecordedEvent(Guid TenantId, decimal Amount, DateTime PaymentDate) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record TenantDeactivatedEvent(Guid TenantId) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record TenantApprovedEvent(Guid TenantId, string Email) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record PasswordResetRequestedEvent(Guid TenantId, string Email, string ResetToken) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record PasswordChangedEvent(Guid TenantId) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record EmailVerifiedEvent(Guid TenantId, string Email) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}
