using BusinessSearcher.Domain.BoundedContext.Radar.Entities;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.ValueObjects;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Aggregates
{
    /// <summary>
    /// Cliente consumidor de la app móvil. Distinto del Tenant (negocio):
    /// solo puede iniciar sesión desde la app y participa en el radar colaborativo
    /// reportando y confirmando disponibilidad de productos.
    /// </summary>
    public class Client : Entity, IAggregateRoot
    {
        public string FullName     { get; private set; } = default!;
        public Email  Email        { get; private set; } = default!;
        public string PasswordHash { get; private set; } = default!;

        public ClientPlan Plan { get; private set; } = ClientPlan.Normal;

        public bool IsEmailVerified { get; private set; }

        /// <summary>
        /// Aprobación de admin (mismo mecanismo que Tenant.IsApproved). Un cliente
        /// recién registrado queda en false y no puede iniciar sesión hasta que
        /// admin@businesssearcher.dev lo apruebe.
        /// </summary>
        public bool IsApproved { get; private set; }

        public string? FcmToken { get; private set; }

        // Teléfono de contacto (WhatsApp): se captura al registrarse desde la app, o la
        // primera vez que el cliente reporta un pago de Premium (ver SubmitPremiumClaim).
        public string? PhoneNumber { get; private set; }

        // ── Ubicación (para vender: aparece en el mapa como pin de cliente) ──
        /// <summary>Ubicación del cliente-vendedor, capturada por GPS al publicar su primer producto.</summary>
        public double? Latitude  { get; private set; }
        public double? Longitude { get; private set; }
        public string? City      { get; private set; }

        // ── Dirección (autoservicio: la carga/edita el propio cliente desde su perfil) ──
        public string? Street  { get; private set; }
        public string? State   { get; private set; }
        public string? Country { get; private set; }

        // ── Reputación (feature 14): mejora la confianza de los reportes que emite ──
        public int ReportsSubmitted       { get; private set; }
        public int ConfirmationsReceived  { get; private set; }
        /// <summary>Reputación 0–100. Arranca en 50 (neutral). Sube con reportes acertados.</summary>
        public int ReputationScore        { get; private set; } = 50;

        // ── Programa de referidos ─────────────────────────────────────────────────
        /// <summary>Código único que este cliente comparte para invitar (ej. "RAIKO82").</summary>
        public string? ReferralCode         { get; private set; }
        /// <summary>Quién invitó a este cliente (null si llegó por su cuenta).</summary>
        public Guid?   ReferredByClientId   { get; private set; }
        /// <summary>Hasta cuándo es válido el premium. null = permanente (concesión de admin).</summary>
        public DateTime? PremiumUntil       { get; private set; }
        /// <summary>El cliente pidió pasar a Premium desde la app y espera revisión de un admin.</summary>
        public bool PremiumRequested        { get; private set; }
        public int ValidReferralsTotal      { get; private set; }
        public int ValidReferralsThisMonth  { get; private set; }
        /// <summary>Ancla del mes (año*100+mes) para reiniciar el contador mensual.</summary>
        public int ReferralMonthAnchor      { get; private set; }

        // ── Referidos de MiPymes (Tenants) ──────────────────────────────────────────
        /// <summary>MiPymes referidas y validadas por este cliente (histórico total).</summary>
        public int MipymeReferralsTotal     { get; private set; }
        public int MipymeReferralsThisMonth { get; private set; }
        /// <summary>Ancla del mes (año*100+mes) para reiniciar el contador mensual de MiPymes.</summary>
        public int MipymeReferralMonthAnchor { get; private set; }

        /// <summary>Saldo en CUP acumulado por referidos, pendiente de que el admin lo pague.</summary>
        public decimal ReferralCupBalance   { get; private set; }
        /// <summary>Histórico de CUP ya liquidados (pagados) por el admin.</summary>
        public decimal ReferralCupPaidTotal { get; private set; }

        private readonly List<ReferralCupTransaction> _referralCupTransactions = new();
        public IReadOnlyCollection<ReferralCupTransaction> ReferralCupTransactions => _referralCupTransactions.AsReadOnly();

        private readonly List<ClientPremiumClaim> _premiumClaims = new();
        public IReadOnlyCollection<ClientPremiumClaim> PremiumClaims => _premiumClaims.AsReadOnly();

        private readonly List<ClientRefreshToken> _refreshTokens = new();
        public IReadOnlyCollection<ClientRefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

        private readonly List<ClientPasswordResetToken> _passwordResetTokens = new();
        public IReadOnlyCollection<ClientPasswordResetToken> PasswordResetTokens => _passwordResetTokens.AsReadOnly();

        private readonly List<ClientEmailVerificationToken> _emailVerificationTokens = new();
        public IReadOnlyCollection<ClientEmailVerificationToken> EmailVerificationTokens => _emailVerificationTokens.AsReadOnly();

        private Client() { }

        public static Client Create(string fullName, string email, string passwordHash, ClientPlan plan = ClientPlan.Normal)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new DomainException("El nombre no puede estar vacío.");
            if (fullName.Trim().Length < 3)
                throw new DomainException("El nombre debe tener al menos 3 caracteres.");
            if (fullName.Length > 120)
                throw new DomainException("El nombre no puede exceder 120 caracteres.");
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new DomainException("El hash de contraseña es requerido.");

            var client = new Client
            {
                FullName     = fullName.Trim(),
                Email        = Email.Create(email),
                PasswordHash = passwordHash,
                Plan         = plan,
                IsApproved   = false
            };

            client.AddDomainEvent(new ClientRegisteredEvent(client.Id, client.FullName, client.Email.Value));
            return client;
        }

        /// <summary>Aprueba la cuenta (acción de admin): habilita el login.</summary>
        public void Approve()
        {
            if (IsApproved) return;
            IsApproved = true;
            SetUpdated();
            AddDomainEvent(new ClientApprovedEvent(Id));
        }

        /// <summary>
        /// Rechaza/revoca la aprobación (acción de admin): bloquea el login y cierra
        /// cualquier sesión activa (revoca los refresh tokens vigentes).
        /// </summary>
        public void Reject()
        {
            if (!IsApproved) return;
            IsApproved = false;
            RevokeAllRefreshTokens();
            SetUpdated();
            AddDomainEvent(new ClientRejectedEvent(Id));
        }

        public void UpgradeToPremium()
        {
            // Concesión permanente (admin): premium sin fecha de caducidad.
            PremiumUntil = null;
            PremiumRequested = false;
            if (Plan == ClientPlan.Premium) { SetUpdated(); return; }
            Plan = ClientPlan.Premium;
            SetUpdated();
            AddDomainEvent(new ClientPlanChangedEvent(Id, Plan));
        }

        /// <summary>El cliente pide pasar a Premium desde la app. Queda pendiente de revisión de un admin.</summary>
        public void RequestPremium()
        {
            if (Plan == ClientPlan.Premium)
                throw new DomainException("Ya tienes el plan Premium.");
            if (PremiumRequested)
                throw new DomainException("Ya tienes una solicitud de Premium pendiente de revisión.");
            PremiumRequested = true;
            SetUpdated();
        }

        /// <summary>Un admin rechaza la solicitud de Premium (el cliente se queda en Normal).</summary>
        public void RejectPremiumRequest()
        {
            if (!PremiumRequested) return;
            PremiumRequested = false;
            SetUpdated();
        }

        // ── TELÉFONO Y REPORTES DE PAGO DE PREMIUM (autoservicio) ───────────────────
        // Espejo de Tenant.SubmitPaymentClaim/ApprovePaymentClaim/RejectPaymentClaim.

        public void SetPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new DomainException("El número de teléfono no puede estar vacío.");
            PhoneNumber = phoneNumber.Trim();
            SetUpdated();
        }

        /// <summary>
        /// El propio cliente reporta un pago manual (teléfono + monto + comprobante) para
        /// pasar a Premium. Queda "Pendiente" hasta que un admin lo apruebe o rechace — no
        /// otorga Premium por sí solo (eso solo pasa al aprobar, ver ApprovePremiumClaim).
        /// </summary>
        public ClientPremiumClaim SubmitPremiumClaim(string phoneNumber, decimal amount, string currency, string proofReference)
        {
            if (_premiumClaims.Any(c => c.Status == PremiumClaimStatus.Pending))
                throw new DomainException("Ya tienes un reporte de pago pendiente de revisión.");

            SetPhoneNumber(phoneNumber);

            var claim = new ClientPremiumClaim(Id, phoneNumber, amount, currency, proofReference);
            _premiumClaims.Add(claim);
            SetUpdated();
            return claim;
        }

        /// <summary>Aprueba un reporte de pago: extiende el Premium N días (reusa ExtendPremium, sin mecanismo paralelo).</summary>
        public ClientPremiumClaim ApprovePremiumClaim(Guid claimId, int days, string? note = null)
        {
            var claim = _premiumClaims.FirstOrDefault(c => c.Id == claimId)
                ?? throw new DomainException("Reporte de pago no encontrado.");

            claim.Approve(note);
            PremiumRequested = false;
            ExtendPremium(days);
            return claim;
        }

        public ClientPremiumClaim RejectPremiumClaim(Guid claimId, string? note = null)
        {
            var claim = _premiumClaims.FirstOrDefault(c => c.Id == claimId)
                ?? throw new DomainException("Reporte de pago no encontrado.");

            claim.Reject(note);
            SetUpdated();
            return claim;
        }

        // ── Referidos ─────────────────────────────────────────────────────────────

        /// <summary>Asigna el código de invitación una sola vez (al crear el cliente).</summary>
        public void AssignReferralCode(string code)
        {
            if (!string.IsNullOrWhiteSpace(ReferralCode)) return;
            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException("El código de referido no puede estar vacío.");
            ReferralCode = code.Trim().ToUpperInvariant();
            SetUpdated();
        }

        /// <summary>Registra quién invitó a este cliente (una sola vez, no puede ser él mismo).</summary>
        public void SetReferredBy(Guid inviterClientId)
        {
            if (ReferredByClientId is not null) return;
            if (inviterClientId == Id)
                throw new DomainException("No puedes usar tu propio código de invitación.");
            ReferredByClientId = inviterClientId;
            SetUpdated();
        }

        /// <summary>
        /// Extiende el premium N días (desde hoy o desde la fecha vigente si aún no vence)
        /// y asegura el plan Premium. Se usa para aplicar recompensas de referidos.
        /// </summary>
        public void ExtendPremium(int days)
        {
            if (days <= 0) return;
            var baseDate = (PremiumUntil.HasValue && PremiumUntil.Value > DateTime.UtcNow)
                ? PremiumUntil.Value : DateTime.UtcNow;
            PremiumUntil = baseDate.AddDays(days);
            if (Plan != ClientPlan.Premium)
            {
                Plan = ClientPlan.Premium;
                AddDomainEvent(new ClientPlanChangedEvent(Id, Plan));
            }
            SetUpdated();
        }

        /// <summary>
        /// Suma un referido válido y devuelve el total mensual acumulado
        /// (con reinicio automático al cambiar de mes) para calcular la recompensa.
        /// </summary>
        public (int total, int thisMonth) RecordValidReferral(DateTime now)
        {
            var anchor = now.Year * 100 + now.Month;
            if (ReferralMonthAnchor != anchor)
            {
                ReferralMonthAnchor     = anchor;
                ValidReferralsThisMonth = 0;
            }
            ValidReferralsTotal++;
            ValidReferralsThisMonth++;
            SetUpdated();
            return (ValidReferralsTotal, ValidReferralsThisMonth);
        }

        /// <summary>
        /// Suma una MiPyme referida y validada (equivalente a RecordValidReferral, pero para el
        /// programa de referidos de MiPymes) y acredita 7000 CUP al saldo unificado de ganancias
        /// (mismo saldo que los referidos de clientes: Client.ReferralCupBalance).
        /// </summary>
        public (int total, int thisMonth) RecordValidMipymeReferral(DateTime now)
        {
            var anchor = now.Year * 100 + now.Month;
            if (MipymeReferralMonthAnchor != anchor)
            {
                MipymeReferralMonthAnchor = anchor;
                MipymeReferralsThisMonth  = 0;
            }
            MipymeReferralsTotal++;
            MipymeReferralsThisMonth++;
            CreditReferralCup(7000m, ReferralCupTransactionType.MipymeReferralBonus);
            SetUpdated();
            return (MipymeReferralsTotal, MipymeReferralsThisMonth);
        }

        /// <summary>Suma CUP al saldo pendiente de pago (bono por referido o premio mensual).</summary>
        public void CreditReferralCup(decimal amount, ReferralCupTransactionType type, string? note = null)
        {
            if (amount <= 0)
                throw new DomainException("El monto a acreditar debe ser mayor que cero.");

            ReferralCupBalance += amount;
            _referralCupTransactions.Add(new ReferralCupTransaction(Id, amount, type, note));
            SetUpdated();
        }

        /// <summary>
        /// Acción de admin: liquida (paga) el saldo pendiente, lo mueve al histórico y lo
        /// deja en 0. Devuelve el monto pagado (0 si no había saldo, no-op).
        /// </summary>
        public decimal SettleReferralCupPayout()
        {
            if (ReferralCupBalance <= 0) return 0m;

            var paid = ReferralCupBalance;
            ReferralCupPaidTotal += paid;
            ReferralCupBalance = 0m;
            _referralCupTransactions.Add(new ReferralCupTransaction(Id, -paid, ReferralCupTransactionType.PayoutSettlement));
            SetUpdated();
            return paid;
        }

        /// <summary>Degrada a Normal si el premium con caducidad ya venció. Devuelve true si cambió.</summary>
        public bool DowngradeIfPremiumExpired(DateTime now)
        {
            if (Plan == ClientPlan.Premium && PremiumUntil.HasValue && PremiumUntil.Value <= now)
            {
                Plan = ClientPlan.Normal;
                SetUpdated();
                AddDomainEvent(new ClientPlanChangedEvent(Id, Plan));
                return true;
            }
            return false;
        }

        public void DowngradeToNormal()
        {
            PremiumRequested = false;
            if (Plan == ClientPlan.Normal) { SetUpdated(); return; }
            Plan = ClientPlan.Normal;
            SetUpdated();
            AddDomainEvent(new ClientPlanChangedEvent(Id, Plan));
        }

        public void UpdateFullName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new DomainException("El nombre no puede estar vacío.");
            FullName = newName.Trim();
            SetUpdated();
        }

        public void RegisterFcmToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new DomainException("El token FCM no puede estar vacío.");
            FcmToken = token;
            SetUpdated();
        }

        /// <summary>Fija/actualiza la ubicación del cliente-vendedor (GPS al publicar).</summary>
        public void SetLocation(double latitude, double longitude, string? city = null)
        {
            if (latitude < -90 || latitude > 90)
                throw new DomainException("Latitud fuera de rango.");
            if (longitude < -180 || longitude > 180)
                throw new DomainException("Longitud fuera de rango.");
            Latitude  = latitude;
            Longitude = longitude;
            if (!string.IsNullOrWhiteSpace(city))
                City = city.Trim();
            SetUpdated();
        }

        /// <summary>
        /// Fija/actualiza la dirección de texto del cliente (autoservicio, desde su perfil).
        /// Sin validación estricta de formato — solo recorta espacios. Cualquier parámetro
        /// null se ignora (no borra el valor existente); pasa cadena vacía para limpiarlo.
        /// </summary>
        public void SetAddress(string? street, string? city, string? state, string? country)
        {
            if (street is not null)  Street  = string.IsNullOrWhiteSpace(street)  ? null : street.Trim();
            if (city is not null)    City    = string.IsNullOrWhiteSpace(city)    ? null : city.Trim();
            if (state is not null)   State   = string.IsNullOrWhiteSpace(state)   ? null : state.Trim();
            if (country is not null) Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim();
            SetUpdated();
        }

        // ── Reputación ────────────────────────────────────────────────────────────

        public void RecordReportSubmitted()
        {
            ReportsSubmitted++;
            SetUpdated();
        }

        /// <summary>Aplica el efecto de una confirmación recibida en un reporte propio.</summary>
        public void RecordConfirmationReceived(bool positive)
        {
            ConfirmationsReceived++;
            ReputationScore = Math.Clamp(ReputationScore + (positive ? 2 : -3), 0, 100);
            SetUpdated();
        }

        // ── REFRESH TOKENS ───────────────────────────────────────────────────────

        public ClientRefreshToken IssueRefreshToken(string token, TimeSpan lifetime, string? createdByIp = null)
        {
            var refreshToken = new ClientRefreshToken(Id, token, DateTime.UtcNow.Add(lifetime), createdByIp);
            _refreshTokens.Add(refreshToken);
            SetUpdated();
            return refreshToken;
        }

        public ClientRefreshToken RotateRefreshToken(string currentToken, string newToken, TimeSpan lifetime, string? createdByIp = null)
        {
            var existing = _refreshTokens.FirstOrDefault(t => t.Token == currentToken)
                ?? throw new DomainException("Refresh token inválido.");

            if (!existing.IsActive)
            {
                foreach (var rt in _refreshTokens.Where(t => t.IsActive))
                    rt.Revoke();
                SetUpdated();
                throw new DomainException("Refresh token inválido o reutilizado (posible reutilización detectada). Por seguridad, todas tus sesiones fueron cerradas. Vuelve a iniciar sesión.");
            }

            existing.Revoke(newToken);
            return IssueRefreshToken(newToken, lifetime, createdByIp);
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

        public ClientPasswordResetToken RequestPasswordReset(string token, TimeSpan lifetime)
        {
            foreach (var old in _passwordResetTokens.Where(t => t.IsValid))
                old.MarkAsUsed();

            var resetToken = new ClientPasswordResetToken(Id, token, DateTime.UtcNow.Add(lifetime));
            _passwordResetTokens.Add(resetToken);
            SetUpdated();
            AddDomainEvent(new ClientPasswordResetRequestedEvent(Id, Email.Value, token));
            return resetToken;
        }

        public void ResetPassword(string token, string newPasswordHash)
        {
            var resetToken = _passwordResetTokens.FirstOrDefault(t => t.Token == token)
                ?? throw new DomainException("Token de recuperación inválido.");

            resetToken.MarkAsUsed();
            PasswordHash = newPasswordHash;
            RevokeAllRefreshTokens();
            SetUpdated();
            AddDomainEvent(new ClientPasswordChangedEvent(Id));
        }

        public void ChangePassword(string currentPassword, string newPasswordHash, Func<string, string, bool> verifyPasswordFn)
        {
            if (!verifyPasswordFn(currentPassword, PasswordHash))
                throw new DomainException("La contraseña actual no es correcta.");

            PasswordHash = newPasswordHash;
            RevokeAllRefreshTokens();
            SetUpdated();
            AddDomainEvent(new ClientPasswordChangedEvent(Id));
        }

        // ── EMAIL VERIFICATION ────────────────────────────────────────────────────

        public ClientEmailVerificationToken RequestEmailVerification(string token, TimeSpan lifetime)
        {
            foreach (var old in _emailVerificationTokens.Where(t => t.IsValid))
                old.MarkAsUsed();

            var verifyToken = new ClientEmailVerificationToken(Id, token, DateTime.UtcNow.Add(lifetime));
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
            AddDomainEvent(new ClientEmailVerifiedEvent(Id, Email.Value));
        }
    }

    // ── Domain Events ───────────────────────────────────────────────────────────
    public record ClientRegisteredEvent(Guid ClientId, string FullName, string Email) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ClientPlanChangedEvent(Guid ClientId, ClientPlan Plan) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ClientApprovedEvent(Guid ClientId) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ClientRejectedEvent(Guid ClientId) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ClientPasswordResetRequestedEvent(Guid ClientId, string Email, string ResetToken) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ClientPasswordChangedEvent(Guid ClientId) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ClientEmailVerifiedEvent(Guid ClientId, string Email) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}
