namespace BusinessSearcher.Domain.Common
{
    public abstract class Entity
    {
        private List<IDomainEvent> _domainEvents = new();

        protected Entity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        protected Entity(Guid id)
        {
            Id = id;
            CreatedAt = DateTime.UtcNow;
        }

        public Guid Id { get; protected set; }
        public DateTime CreatedAt { get; protected set; }
        public DateTime? UpdatedAt { get; protected set; }

        // CORRECCIÓN: Typo "DomianEvents" → "DomainEvents"
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents ??= new List<IDomainEvent>();
            _domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents() => _domainEvents.Clear();

        protected void SetUpdated() => UpdatedAt = DateTime.UtcNow;

        public override bool Equals(object? obj)
        {
            if (obj is not Entity other || GetType() != other.GetType()) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(Entity? left, Entity? right)
        {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(Entity? left, Entity? right) => !(left == right);
    }

    public interface IDomainEvent
    {
        DateTime OccurredOn { get; }
    }
}
