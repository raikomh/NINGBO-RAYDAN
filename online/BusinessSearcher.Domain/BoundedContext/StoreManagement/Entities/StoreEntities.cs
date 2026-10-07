using BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.StoreManagement.Entities
{
    public class StoreSchedule : Entity
    {
        public Guid         StoreId    { get; private set; }
        public DayOfWeek    DayOfWeek  { get; private set; }
        public ScheduleTime Time       { get; private set; } = default!;
        public bool         IsClosed   { get; private set; }

        private StoreSchedule() { }

        internal static StoreSchedule Create(Guid storeId, DayOfWeek dayOfWeek,
            ScheduleTime time, bool isClosed = false)
        {
            return new StoreSchedule
            {
                StoreId   = storeId,
                DayOfWeek = dayOfWeek,
                Time      = time,
                IsClosed  = isClosed
            };
        }

        internal void Update(ScheduleTime time, bool isClosed)
        {
            Time     = time;
            IsClosed = isClosed;
            SetUpdated();
        }

        internal void MarkAsClosed()  { IsClosed = true;  SetUpdated(); }
        internal void MarkAsOpen()    { IsClosed = false; SetUpdated(); }

        public bool IsOpenNow() => !IsClosed && Time.IsOpenNow();
    }

    public class OrderItem : Entity
    {
        public Guid    OrderId     { get; private set; }
        public Guid    ProductId   { get; private set; }
        public string  ProductName { get; private set; } = default!;
        public int     Quantity    { get; private set; }
        public decimal UnitPrice   { get; private set; }
        public decimal Total       { get; private set; }

        private OrderItem() { }

        internal static OrderItem Create(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice)
            => new()
            {
                OrderId     = orderId,
                ProductId   = productId,
                ProductName = productName.Trim(),
                Quantity    = quantity,
                UnitPrice   = unitPrice,
                Total       = quantity * unitPrice
            };
    }

}
