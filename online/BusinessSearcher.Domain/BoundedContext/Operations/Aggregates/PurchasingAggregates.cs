using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>
    /// Solicitud de reabastecimiento (TPV: PurchaseRequest). Se genera cuando un producto cae por
    /// debajo de su stock mínimo; solo el Administrador ajusta cantidades antes de aprobar.
    /// </summary>
    public class PurchaseRequest : Entity, IAggregateRoot
    {
        public Guid                  TenantId          { get; private set; }
        public Guid                  ProductId         { get; private set; }
        public string                ProductName       { get; private set; } = default!;
        public Guid                  WarehouseId       { get; private set; }
        public int                   CurrentStock      { get; private set; }
        public int                   MinStock          { get; private set; }
        public int                   RequestedQuantity { get; private set; }
        public PurchaseRequestStatus Status            { get; private set; } = PurchaseRequestStatus.Pending;
        public Guid?                 UserId            { get; private set; }
        public string?               Notes             { get; private set; }
        public DateTime              Date              { get; private set; }

        private PurchaseRequest() { }

        public static PurchaseRequest Create(Guid tenantId, Guid productId, string productName, Guid warehouseId,
            int currentStock, int minStock, int requestedQuantity, Guid? userId = null, string? notes = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("La solicitud debe pertenecer a un negocio.");
            if (requestedQuantity <= 0) throw new DomainException("La cantidad solicitada debe ser mayor que cero.");
            return new PurchaseRequest
            {
                TenantId = tenantId, ProductId = productId, ProductName = productName, WarehouseId = warehouseId,
                CurrentStock = currentStock, MinStock = minStock, RequestedQuantity = requestedQuantity,
                UserId = userId, Notes = notes?.Trim(), Date = DateTime.UtcNow
            };
        }

        public void UpdateQuantity(int requestedQuantity)
        {
            EnsurePending();
            if (requestedQuantity <= 0) throw new DomainException("La cantidad solicitada debe ser mayor que cero.");
            RequestedQuantity = requestedQuantity; SetUpdated();
        }

        public void Approve() { EnsurePending(); Status = PurchaseRequestStatus.Approved; SetUpdated(); }
        public void Reject()  { EnsurePending(); Status = PurchaseRequestStatus.Rejected; SetUpdated(); }

        /// <summary>Marca la solicitud como finalizada al asociarla a una compra recibida.</summary>
        public void Complete()
        {
            if (Status is PurchaseRequestStatus.Rejected)
                throw new DomainException("Una solicitud rechazada no puede completarse.");
            Status = PurchaseRequestStatus.Completed; SetUpdated();
        }

        private void EnsurePending()
        {
            if (Status != PurchaseRequestStatus.Pending)
                throw new DomainException("Solo se pueden modificar solicitudes pendientes.");
        }
    }

    /// <summary>Renglón de una compra a proveedor (TPV: PurchaseItem).</summary>
    public class PurchaseItem : Entity
    {
        public Guid      PurchaseId     { get; private set; }
        public Guid      ProductId      { get; private set; }
        public string    ProductName    { get; private set; } = default!;
        public int       Quantity       { get; private set; }
        public decimal   CostPrice      { get; private set; }
        public string?   BatchNumber    { get; private set; }
        public DateTime? ExpirationDate { get; private set; }

        private PurchaseItem() { }
        private PurchaseItem(Guid id) : base(id) { }

        public static PurchaseItem Create(Guid productId, string productName, int quantity, decimal costPrice,
            string? batchNumber = null, DateTime? expirationDate = null)
        {
            if (quantity <= 0) throw new DomainException("La cantidad debe ser mayor que cero.");
            if (costPrice < 0) throw new DomainException("El costo no puede ser negativo.");
            return new PurchaseItem
            {
                ProductId = productId, ProductName = productName, Quantity = quantity, CostPrice = costPrice,
                BatchNumber = batchNumber?.Trim(), ExpirationDate = expirationDate
            };
        }

        public decimal LineTotal => CostPrice * Quantity;

        public static PurchaseItem Restore(Guid id, DateTime createdAt, DateTime? updatedAt,
            Guid productId, string productName, int quantity, decimal costPrice,
            string? batchNumber, DateTime? expirationDate)
        {
            var item = new PurchaseItem(id)
            {
                ProductId = productId, ProductName = productName, Quantity = quantity, CostPrice = costPrice,
                BatchNumber = batchNumber, ExpirationDate = expirationDate
            };
            item.CreatedAt = createdAt;
            item.UpdatedAt = updatedAt;
            return item;
        }
    }

    /// <summary>
    /// Compra a proveedor (TPV: Purchase). Al recibirla aumenta el stock del almacén y marca como
    /// finalizadas las solicitudes asociadas.
    /// </summary>
    public class Purchase : Entity, IAggregateRoot
    {
        public Guid           TenantId              { get; private set; }
        public Guid?          SupplierId            { get; private set; }
        public Guid           WarehouseId           { get; private set; }
        public Guid?          UserId                { get; private set; }
        public decimal        Total                 { get; private set; }
        public decimal?       TotalUSD              { get; private set; }
        public decimal        AssociatedExpenses    { get; private set; }
        public decimal?       AssociatedExpensesUSD { get; private set; }
        public Currency       Currency              { get; private set; }
        public decimal?       ExchangeRate          { get; private set; }
        public PurchaseStatus Status                { get; private set; } = PurchaseStatus.Completed;
        public string?        InvoiceUrl            { get; private set; }
        public DateTime       Date                  { get; private set; }

        private readonly List<PurchaseItem> _items = new();
        public IReadOnlyCollection<PurchaseItem> Items => _items.AsReadOnly();

        private readonly List<Guid> _purchaseRequestIds = new();
        public IReadOnlyCollection<Guid> PurchaseRequestIds => _purchaseRequestIds.AsReadOnly();

        private Purchase() { }
        private Purchase(Guid id) : base(id) { }

        public static Purchase Create(Guid tenantId, Guid warehouseId, IEnumerable<PurchaseItem> items,
            Currency currency, Guid? supplierId = null, Guid? userId = null, decimal associatedExpenses = 0,
            decimal? associatedExpensesUsd = null, decimal? exchangeRate = null, string? invoiceUrl = null,
            IEnumerable<Guid>? purchaseRequestIds = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("La compra debe pertenecer a un negocio.");
            var itemList = items?.ToList() ?? new List<PurchaseItem>();
            if (itemList.Count == 0) throw new DomainException("La compra debe tener al menos un producto.");

            var purchase = new Purchase
            {
                TenantId = tenantId, WarehouseId = warehouseId, SupplierId = supplierId, UserId = userId,
                Currency = currency, AssociatedExpenses = associatedExpenses, AssociatedExpensesUSD = associatedExpensesUsd,
                ExchangeRate = exchangeRate, InvoiceUrl = invoiceUrl?.Trim(), Date = DateTime.UtcNow
            };
            foreach (var it in itemList) purchase._items.Add(it);
            if (purchaseRequestIds is not null) purchase._purchaseRequestIds.AddRange(purchaseRequestIds);

            purchase.Total = purchase._items.Sum(i => i.LineTotal) + associatedExpenses;
            if (exchangeRate is > 0)
                purchase.TotalUSD = Math.Round(purchase.Total / exchangeRate.Value, 2);
            return purchase;
        }

        public static Purchase Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            Guid? supplierId, Guid warehouseId, Guid? userId, decimal total, decimal? totalUsd,
            decimal associatedExpenses, decimal? associatedExpensesUsd, Currency currency, decimal? exchangeRate,
            PurchaseStatus status, string? invoiceUrl, DateTime date,
            IEnumerable<PurchaseItem> items, IEnumerable<Guid> purchaseRequestIds)
        {
            var purchase = new Purchase(id)
            {
                TenantId = tenantId, SupplierId = supplierId, WarehouseId = warehouseId, UserId = userId,
                Total = total, TotalUSD = totalUsd, AssociatedExpenses = associatedExpenses,
                AssociatedExpensesUSD = associatedExpensesUsd, Currency = currency, ExchangeRate = exchangeRate,
                Status = status, InvoiceUrl = invoiceUrl, Date = date
            };
            foreach (var it in items) purchase._items.Add(it);
            purchase._purchaseRequestIds.AddRange(purchaseRequestIds);
            purchase.CreatedAt = createdAt;
            purchase.UpdatedAt = updatedAt;
            return purchase;
        }
    }
}
