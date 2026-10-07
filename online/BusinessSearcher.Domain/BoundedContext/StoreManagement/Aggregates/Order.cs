using BusinessSearcher.Domain.BoundedContext.StoreManagement.Entities;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates
{
    public enum OrderStatus { Pending, Completed, Cancelled }

    public class Order : Entity, IAggregateRoot
    {
        public Guid        StoreId   { get; private set; }
        public OrderStatus Status    { get; private set; }
        public decimal     Total     { get; private set; }
        public string?     Notes     { get; private set; }

        private readonly List<OrderItem> _items = new();
        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

        private Order() { }

        public static Order Create(Guid storeId, IEnumerable<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> items, string? notes = null)
        {
            if (storeId == Guid.Empty)
                throw new DomainException("El StoreId es requerido.");

            var itemList = items.ToList();
            if (!itemList.Any())
                throw new DomainException("La venta debe tener al menos un producto.");

            var order = new Order
            {
                StoreId = storeId,
                Status  = OrderStatus.Pending,
                Notes   = notes?.Trim()
            };

            foreach (var (productId, productName, quantity, unitPrice) in itemList)
                order.AddItem(productId, productName, quantity, unitPrice);

            order.RecalculateTotal();
            order.AddDomainEvent(new OrderCreatedEvent(order.Id, storeId, order.Total));
            return order;
        }

        public void Complete()
        {
            if (Status != OrderStatus.Pending)
                throw new DomainException("Solo se pueden completar ventas en estado Pendiente.");
            Status = OrderStatus.Completed;
            SetUpdated();
            AddDomainEvent(new OrderStatusChangedEvent(Id, StoreId, Status));
        }

        public void Cancel()
        {
            if (Status == OrderStatus.Cancelled)
                throw new DomainException("La venta ya está cancelada.");
            if (Status == OrderStatus.Completed)
                throw new DomainException("No se puede cancelar una venta completada.");
            Status = OrderStatus.Cancelled;
            SetUpdated();
            AddDomainEvent(new OrderStatusChangedEvent(Id, StoreId, Status));
        }

        private void AddItem(Guid productId, string productName, int quantity, decimal unitPrice)
        {
            if (quantity <= 0)
                throw new DomainException($"La cantidad para '{productName}' debe ser mayor a 0.");
            if (unitPrice < 0)
                throw new DomainException($"El precio para '{productName}' no puede ser negativo.");

            _items.Add(OrderItem.Create(Id, productId, productName, quantity, unitPrice));
        }

        private void RecalculateTotal()
            => Total = _items.Sum(i => i.Total);
    }

    public record OrderCreatedEvent(Guid OrderId, Guid StoreId, decimal Total) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record OrderStatusChangedEvent(Guid OrderId, Guid StoreId, OrderStatus Status) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}
