using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>Datos fiscales/identidad del negocio (TPV: BusinessInfo). Uno por Tenant.</summary>
    public class BusinessInfo : Entity, IAggregateRoot
    {
        public Guid    TenantId { get; private set; }
        public string  Name     { get; private set; } = default!;
        public string? Address  { get; private set; }
        public string? Phone    { get; private set; }
        public string? Email    { get; private set; }
        public string? TaxId    { get; private set; }
        public string? LogoUrl  { get; private set; }

        private BusinessInfo() { }
        private BusinessInfo(Guid id) : base(id) { }

        public static BusinessInfo Create(Guid tenantId, string name, string? address = null, string? phone = null,
            string? email = null, string? taxId = null, string? logoUrl = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("Los datos del negocio requieren un negocio.");
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del negocio es requerido.");
            return new BusinessInfo
            {
                TenantId = tenantId, Name = name.Trim(), Address = address?.Trim(), Phone = phone?.Trim(),
                Email = email?.Trim(), TaxId = taxId?.Trim(), LogoUrl = logoUrl?.Trim()
            };
        }

        /// <summary>Reconstruye desde un paquete de sincronización, preservando identidad/timestamps originales.</summary>
        public static BusinessInfo Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            string name, string? address, string? phone, string? email, string? taxId, string? logoUrl)
        {
            var info = new BusinessInfo(id)
            {
                TenantId = tenantId, Name = name, Address = address, Phone = phone,
                Email = email, TaxId = taxId, LogoUrl = logoUrl
            };
            info.CreatedAt = createdAt;
            info.UpdatedAt = updatedAt;
            return info;
        }

        public void Update(string name, string? address, string? phone, string? email, string? taxId, string? logoUrl)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del negocio es requerido.");
            Name = name.Trim(); Address = address?.Trim(); Phone = phone?.Trim();
            Email = email?.Trim(); TaxId = taxId?.Trim(); LogoUrl = logoUrl?.Trim(); SetUpdated();
        }
    }

    /// <summary>
    /// Registro de auditoría (TPV: AuditLog). Lo escribe el middleware para cada acción crítica
    /// (mutaciones y accesos denegados) del módulo de operaciones.
    /// </summary>
    public class AuditLog : Entity, IAggregateRoot
    {
        public Guid     TenantId     { get; private set; }
        public Guid?    UserId       { get; private set; }
        public string?  UserName     { get; private set; }
        public string?  UserRole     { get; private set; }
        public string   Action       { get; private set; } = default!;
        public string?  TargetEntity { get; private set; }
        public string?  Details      { get; private set; }
        public DateTime Timestamp    { get; private set; }
        public string?  IpAddress    { get; private set; }
        public string   Status       { get; private set; } = default!;
        public string?  Method       { get; private set; }
        public string?  Path         { get; private set; }

        private AuditLog() { }

        public static AuditLog Create(Guid tenantId, string action, string status, Guid? userId = null,
            string? userName = null, string? userRole = null, string? targetEntity = null, string? details = null,
            string? ipAddress = null, string? method = null, string? path = null)
            => new()
            {
                TenantId = tenantId, Action = action, Status = status, UserId = userId, UserName = userName,
                UserRole = userRole, TargetEntity = targetEntity, Details = details, IpAddress = ipAddress,
                Method = method, Path = path, Timestamp = DateTime.UtcNow
            };
    }

    /// <summary>
    /// Configuración genérica clave-valor por negocio (TPV: settings). Para flags/parámetros
    /// puntuales que no ameritan una entidad propia (p.ej. "receipt_footer_text", "auto_print").
    /// </summary>
    public class Setting : Entity, IAggregateRoot
    {
        public Guid   TenantId { get; private set; }
        public string Key      { get; private set; } = default!;
        public string Value    { get; private set; } = default!;

        private Setting() { }

        public static Setting Create(Guid tenantId, string key, string value)
        {
            if (tenantId == Guid.Empty) throw new DomainException("La configuración debe pertenecer a un negocio.");
            if (string.IsNullOrWhiteSpace(key)) throw new DomainException("La clave de configuración es requerida.");
            return new Setting { TenantId = tenantId, Key = key.Trim(), Value = value ?? string.Empty };
        }

        public void UpdateValue(string value)
        {
            Value = value ?? string.Empty;
            SetUpdated();
        }
    }
}
