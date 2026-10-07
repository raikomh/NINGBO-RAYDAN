using BusinessSearcher.Domain.BoundedContext.StoreManagement.Entities;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;
namespace BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates
{
    public class Store : Entity, IAggregateRoot
    {
        // CORRECCIÓN: TenantId faltaba completamente en tu código original
        public Guid         TenantId    { get; private set; }
        public string       Name        { get; private set; } = default!;
        public string?      Description { get; private set; }
        public Address      Address     { get; private set; } = default!;
        public PhoneNumber  Phone       { get; private set; } = default!;
        public string?      LogoUrl     { get; private set; }

        // CORRECCIÓN: "isActive" → "IsActive" (PascalCase)
        public bool IsActive { get; private set; } = true;

        private readonly List<StoreSchedule>      _schedules     = new();

        public IReadOnlyCollection<StoreSchedule>      Schedules     => _schedules.AsReadOnly();

        private Store() { }

        // CORRECCIÓN: Constructor privado + factory method estático (DDD)
        // La StoreFactory de Application fue eliminada: viola encapsulamiento del Aggregate
        public static Store Create(Guid tenantId, string name, Address address,
            PhoneNumber phone, string? description = null, string? logoUrl = null)
        {
            if (tenantId == Guid.Empty)
                throw new DomainException("El TenantId es requerido.");
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("El nombre de la tienda es requerido.");
            if (name.Length > 150)
                throw new DomainException("El nombre no puede exceder 150 caracteres.");

            var store = new Store
            {
                TenantId    = tenantId,
                Name        = name.Trim(),
                Description = description?.Trim(),
                Address     = address,
                Phone       = phone,
                LogoUrl     = logoUrl?.Trim()
            };

            store.AddDomainEvent(new StoreCreatedEvent(store.Id, tenantId, store.Name));
            return store;
        }

        public void Update(string name, Address address, PhoneNumber phone,
            string? description, string? logoUrl)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("El nombre de la tienda es requerido.");

            Name        = name.Trim();
            Address     = address;
            Phone       = phone;
            Description = description?.Trim();
            LogoUrl     = logoUrl?.Trim();
            SetUpdated();
        }

        public void Activate()
        {
            if (IsActive) throw new DomainException("La tienda ya está activa.");
            IsActive = true;
            SetUpdated();
        }

        public void Deactivate()
        {
            if (!IsActive) throw new DomainException("La tienda ya está inactiva.");
            IsActive = false;
            SetUpdated();
            AddDomainEvent(new StoreDeactivatedEvent(Id, TenantId));
        }

        // ── SCHEDULES ────────────────────────────────────────────────────────────

        public StoreSchedule AddOrUpdateSchedule(DayOfWeek dayOfWeek, ScheduleTime time, bool isClosed = false)
        {
            // Valida solapamientos con otros días no aplica (cada día es único)
            var existing = _schedules.FirstOrDefault(s => s.DayOfWeek == dayOfWeek);
            if (existing is not null)
            {
                existing.Update(time, isClosed);
                SetUpdated();
                return existing;
            }

            var schedule = StoreSchedule.Create(Id, dayOfWeek, time, isClosed);
            _schedules.Add(schedule);
            SetUpdated();
            return schedule;
        }

        public bool IsOpenNow()
        {
            var today    = DateTime.Now.DayOfWeek;
            var schedule = _schedules.FirstOrDefault(s => s.DayOfWeek == today);
            return schedule?.IsOpenNow() ?? false;
        }

    }

    // ── Domain Events ─────────────────────────────────────────────────────────────

    public record StoreCreatedEvent(Guid StoreId, Guid TenantId, string StoreName) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record StoreDeactivatedEvent(Guid StoreId, Guid TenantId) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

}
