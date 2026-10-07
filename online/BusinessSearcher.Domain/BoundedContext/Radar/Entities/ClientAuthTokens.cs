using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Entities
{
    /// <summary>Refresh token de larga duración de un Cliente (consumidor de la app).</summary>
    public class ClientRefreshToken : Entity
    {
        public Guid     ClientId        { get; private set; }
        public string   Token           { get; private set; } = default!;
        public DateTime ExpiresAt       { get; private set; }
        public DateTime? RevokedAt      { get; private set; }
        public string?  ReplacedByToken { get; private set; }
        public string?  CreatedByIp     { get; private set; }

        private ClientRefreshToken() { }

        internal ClientRefreshToken(Guid clientId, string token, DateTime expiresAt, string? createdByIp = null)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new DomainException("El refresh token no puede estar vacío.");

            ClientId    = clientId;
            Token       = token;
            ExpiresAt   = expiresAt;
            CreatedByIp = createdByIp;
        }

        public bool IsActive  => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        internal void Revoke(string? replacedByToken = null)
        {
            RevokedAt       = DateTime.UtcNow;
            ReplacedByToken = replacedByToken;
            SetUpdated();
        }
    }

    /// <summary>Token de un solo uso para recuperación de contraseña de un Cliente.</summary>
    public class ClientPasswordResetToken : Entity
    {
        public Guid     ClientId  { get; private set; }
        public string   Token     { get; private set; } = default!;
        public DateTime ExpiresAt { get; private set; }
        public bool     IsUsed    { get; private set; }

        private ClientPasswordResetToken() { }

        internal ClientPasswordResetToken(Guid clientId, string token, DateTime expiresAt)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new DomainException("El token de recuperación no puede estar vacío.");

            ClientId  = clientId;
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

    /// <summary>Token de un solo uso para verificar el email de un Cliente al registrarse.</summary>
    public class ClientEmailVerificationToken : Entity
    {
        public Guid     ClientId  { get; private set; }
        public string   Token     { get; private set; } = default!;
        public DateTime ExpiresAt { get; private set; }
        public bool     IsUsed    { get; private set; }

        private ClientEmailVerificationToken() { }

        internal ClientEmailVerificationToken(Guid clientId, string token, DateTime expiresAt)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new DomainException("El token de verificación no puede estar vacío.");

            ClientId  = clientId;
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
