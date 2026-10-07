using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>
    /// Sub-usuario de un negocio (TPV: usuario con rol operativo). Se autentica con email y
    /// contraseña propios; el JWT lleva el <c>TenantId</c> del negocio dueño y el claim <c>opsRole</c>.
    /// El dueño del negocio (Tenant) es implícitamente Administrador y no necesita OperationsUser.
    /// </summary>
    public class OperationsUser : Entity, IAggregateRoot
    {
        public Guid           TenantId            { get; private set; }
        public string         Name                { get; private set; } = default!;
        public string         Email               { get; private set; } = default!;
        public string         PasswordHash        { get; private set; } = default!;
        public OperationsRole Role                { get; private set; }
        public string?        AvatarUrl           { get; private set; }
        public Guid?          AssignedRegisterId  { get; private set; }
        public Guid?          AssignedWarehouseId { get; private set; }
        public bool           IsActive            { get; private set; } = true;

        private OperationsUser() { }
        private OperationsUser(Guid id) : base(id) { }

        public static OperationsUser Create(Guid tenantId, string name, string email, string passwordHash,
            OperationsRole role, string? avatarUrl = null, Guid? assignedRegisterId = null, Guid? assignedWarehouseId = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("El usuario debe pertenecer a un negocio.");
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre es requerido.");
            if (string.IsNullOrWhiteSpace(email)) throw new DomainException("El email es requerido.");
            if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("La contraseña es requerida.");
            return new OperationsUser
            {
                TenantId = tenantId, Name = name.Trim(), Email = email.Trim().ToLowerInvariant(),
                PasswordHash = passwordHash, Role = role, AvatarUrl = avatarUrl?.Trim(),
                AssignedRegisterId = assignedRegisterId, AssignedWarehouseId = assignedWarehouseId
            };
        }

        public void Update(string name, OperationsRole role, string? avatarUrl,
            Guid? assignedRegisterId, Guid? assignedWarehouseId, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre es requerido.");
            Name = name.Trim(); Role = role; AvatarUrl = avatarUrl?.Trim();
            AssignedRegisterId = assignedRegisterId; AssignedWarehouseId = assignedWarehouseId;
            IsActive = isActive; SetUpdated();
        }

        public void SetPassword(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("La contraseña es requerida.");
            PasswordHash = passwordHash; SetUpdated();
        }

        public static OperationsUser Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            string name, string email, string passwordHash, OperationsRole role, string? avatarUrl,
            Guid? assignedRegisterId, Guid? assignedWarehouseId, bool isActive)
        {
            var user = new OperationsUser(id)
            {
                TenantId = tenantId, Name = name, Email = email, PasswordHash = passwordHash, Role = role,
                AvatarUrl = avatarUrl, AssignedRegisterId = assignedRegisterId,
                AssignedWarehouseId = assignedWarehouseId, IsActive = isActive
            };
            user.CreatedAt = createdAt;
            user.UpdatedAt = updatedAt;
            return user;
        }
    }
}
