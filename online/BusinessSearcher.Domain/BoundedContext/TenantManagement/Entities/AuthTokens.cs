using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities
{
    /// <summary>
    /// Refresh token de larga duración para renovar el JWT sin pedir login de nuevo.
    /// Se invalida (Revoke) al usarlo, al hacer logout, o al detectar reutilización (rotación).
    /// </summary>
    public class RefreshToken : Entity
    {
        public Guid     TenantId   { get; private set; }
        public string   Token      { get; private set; } = default!;
        public DateTime ExpiresAt  { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public string?  ReplacedByToken { get; private set; }
        public string?  CreatedByIp     { get; private set; }

        private RefreshToken() { }

        internal RefreshToken(Guid tenantId, string token, DateTime expiresAt, string? createdByIp = null)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new DomainException("El refresh token no puede estar vacío.");

            TenantId    = tenantId;
            Token       = token;
            ExpiresAt   = expiresAt;
            CreatedByIp = createdByIp;
        }

        public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        internal void Revoke(string? replacedByToken = null)
        {
            RevokedAt       = DateTime.UtcNow;
            ReplacedByToken = replacedByToken;
            SetUpdated();
        }
    }

    /// <summary>
    /// Token de un solo uso para recuperación de contraseña. Expira a los 60 minutos.
    /// </summary>
    public class PasswordResetToken : Entity
    {
        public Guid     TenantId  { get; private set; }
        public string   Token     { get; private set; } = default!;
        public DateTime ExpiresAt { get; private set; }
        public bool     IsUsed    { get; private set; }

        private PasswordResetToken() { }

        internal PasswordResetToken(Guid tenantId, string token, DateTime expiresAt)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new DomainException("El token de recuperación no puede estar vacío.");

            TenantId  = tenantId;
            Token     = token;
            ExpiresAt = expiresAt;
        }

        public bool IsValid => !IsUsed && DateTime.UtcNow < ExpiresAt;

        internal void MarkAsUsed()
        {
            if (IsUsed)
                throw new DomainException("Este token ya fue utilizado.");
            if (DateTime.UtcNow >= ExpiresAt)
                throw new DomainException("Este token ha expirado. Solicita uno nuevo.");

            IsUsed = true;
            SetUpdated();
        }
    }

    /// <summary>
    /// Token de un solo uso para verificar el email al registrarse. Expira a las 24 horas.
    /// </summary>
    public class EmailVerificationToken : Entity
    {
        public Guid     TenantId  { get; private set; }
        public string   Token     { get; private set; } = default!;
        public DateTime ExpiresAt { get; private set; }
        public bool     IsUsed    { get; private set; }

        private EmailVerificationToken() { }

        internal EmailVerificationToken(Guid tenantId, string token, DateTime expiresAt)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new DomainException("El token de verificación no puede estar vacío.");

            TenantId  = tenantId;
            Token     = token;
            ExpiresAt = expiresAt;
        }

        public bool IsValid => !IsUsed && DateTime.UtcNow < ExpiresAt;

        internal void MarkAsUsed()
        {
            if (IsUsed)
                throw new DomainException("Este enlace de verificación ya fue utilizado.");
            if (DateTime.UtcNow >= ExpiresAt)
                throw new DomainException("El enlace de verificación ha expirado. Solicita uno nuevo.");

            IsUsed = true;
            SetUpdated();
        }
    }
}
