using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>
    /// Movimiento de inventario (TPV: InventoryMovement). Entrada/Salida/Merma afectan un almacén;
    /// Traslado mueve de un almacén origen a uno destino.
    /// </summary>
    public class InventoryMovement : Entity, IAggregateRoot
    {
        public Guid                  TenantId        { get; private set; }
        public Guid                  ProductId       { get; private set; }
        public string                ProductName     { get; private set; } = default!;
        public Guid?                 FromWarehouseId { get; private set; }
        public Guid?                 ToWarehouseId   { get; private set; }
        public int                   Quantity        { get; private set; }
        public InventoryMovementType Type            { get; private set; }
        public string?               Reason          { get; private set; }
        public Guid?                 UserId          { get; private set; }
        public DateTime              Date            { get; private set; }

        private InventoryMovement() { }
        private InventoryMovement(Guid id) : base(id) { }

        public static InventoryMovement Create(Guid tenantId, Guid productId, string productName,
            InventoryMovementType type, int quantity, Guid? fromWarehouseId, Guid? toWarehouseId,
            string? reason = null, Guid? userId = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("El movimiento debe pertenecer a un negocio.");
            if (quantity <= 0) throw new DomainException("La cantidad debe ser mayor que cero.");

            switch (type)
            {
                case InventoryMovementType.Entrada:
                    if (toWarehouseId is null) throw new DomainException("Una entrada requiere almacén destino.");
                    break;
                case InventoryMovementType.Salida:
                case InventoryMovementType.Merma:
                    if (fromWarehouseId is null) throw new DomainException("Una salida/merma requiere almacén origen.");
                    break;
                case InventoryMovementType.Traslado:
                    if (fromWarehouseId is null || toWarehouseId is null)
                        throw new DomainException("Un traslado requiere almacén origen y destino.");
                    if (fromWarehouseId == toWarehouseId)
                        throw new DomainException("El origen y el destino deben ser distintos.");
                    break;
            }

            return new InventoryMovement
            {
                TenantId = tenantId, ProductId = productId, ProductName = productName, Type = type,
                Quantity = quantity, FromWarehouseId = fromWarehouseId, ToWarehouseId = toWarehouseId,
                Reason = reason?.Trim(), UserId = userId, Date = DateTime.UtcNow
            };
        }

        public static InventoryMovement Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            Guid productId, string productName, Guid? fromWarehouseId, Guid? toWarehouseId,
            int quantity, InventoryMovementType type, string? reason, Guid? userId, DateTime date)
        {
            var movement = new InventoryMovement(id)
            {
                TenantId = tenantId, ProductId = productId, ProductName = productName, Type = type,
                Quantity = quantity, FromWarehouseId = fromWarehouseId, ToWarehouseId = toWarehouseId,
                Reason = reason, UserId = userId, Date = date
            };
            movement.CreatedAt = createdAt;
            movement.UpdatedAt = updatedAt;
            return movement;
        }
    }

    /// <summary>Renglón de un conteo físico con su discrepancia (sistema vs contado).</summary>
    public class InventoryCountItem : Entity
    {
        public Guid   InventoryCountId { get; private set; }
        public Guid   ProductId        { get; private set; }
        public string ProductName      { get; private set; } = default!;
        public int    SystemQuantity   { get; private set; }
        public int    CountedQuantity  { get; private set; }

        /// <summary>Confirmación manual de que el producto fue verificado físicamente durante el conteo.</summary>
        public bool   Audited          { get; private set; }

        private InventoryCountItem() { }

        public static InventoryCountItem Create(Guid productId, string productName, int systemQuantity, int countedQuantity)
            => new() { ProductId = productId, ProductName = productName, SystemQuantity = systemQuantity, CountedQuantity = countedQuantity };

        /// <summary>Diferencia (contado − sistema): positiva = sobrante, negativa = faltante.</summary>
        public int Difference => CountedQuantity - SystemQuantity;

        internal void SetAudited(bool audited) => Audited = audited;
    }

    /// <summary>
    /// Conteo físico de inventario de un almacén (TPV: InventoryCount). Al cerrarlo se puede aplicar
    /// el ajuste al stock del sistema para igualarlo a lo contado.
    /// </summary>
    public class InventoryCount : Entity, IAggregateRoot
    {
        public Guid                 TenantId    { get; private set; }
        public Guid                 WarehouseId { get; private set; }
        public InventoryCountStatus Status      { get; private set; } = InventoryCountStatus.Open;
        public Guid?                UserId      { get; private set; }
        public DateTime             Date        { get; private set; }
        public bool                 Adjusted    { get; private set; }

        private readonly List<InventoryCountItem> _items = new();
        public IReadOnlyCollection<InventoryCountItem> Items => _items.AsReadOnly();

        private InventoryCount() { }

        public static InventoryCount Create(Guid tenantId, Guid warehouseId, IEnumerable<InventoryCountItem> items, Guid? userId = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("El conteo debe pertenecer a un negocio.");
            var count = new InventoryCount { TenantId = tenantId, WarehouseId = warehouseId, UserId = userId, Date = DateTime.UtcNow };
            foreach (var it in items ?? Enumerable.Empty<InventoryCountItem>()) count._items.Add(it);
            return count;
        }

        /// <summary>Renglones con discrepancia (contado ≠ sistema).</summary>
        public IEnumerable<InventoryCountItem> Discrepancies => _items.Where(i => i.Difference != 0);

        /// <summary>Marca (o desmarca) un producto del conteo como auditado/verificado físicamente.</summary>
        public void SetItemAudited(Guid productId, bool audited)
        {
            if (Status != InventoryCountStatus.Open) throw new DomainException("El conteo ya está cerrado.");
            var item = _items.FirstOrDefault(i => i.ProductId == productId)
                ?? throw new DomainException("El producto no pertenece a este conteo.");
            item.SetAudited(audited);
            SetUpdated();
        }

        public void Close(bool adjusted)
        {
            if (Status != InventoryCountStatus.Open) throw new DomainException("El conteo ya está cerrado.");
            if (_items.Any(i => !i.Audited))
                throw new DomainException("Todos los productos del conteo deben quedar auditados antes de cerrarlo.");
            Status = InventoryCountStatus.Closed; Adjusted = adjusted; SetUpdated();
        }
    }
}
