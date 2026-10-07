using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>Renglón de una venta (TPV: CartItem dentro de Sale).</summary>
    public class SaleItem : Entity
    {
        public Guid         SaleId        { get; private set; }
        public Guid         ProductId     { get; private set; }
        public string       ProductName   { get; private set; } = default!;
        public Guid         WarehouseId   { get; private set; }
        public int          Quantity      { get; private set; }
        public decimal      UnitPrice     { get; private set; }
        public DiscountType DiscountType  { get; private set; }
        public decimal      DiscountValue { get; private set; }

        private SaleItem() { }
        private SaleItem(Guid id) : base(id) { }

        public static SaleItem Create(Guid productId, string productName, Guid warehouseId, int quantity,
            decimal unitPrice, DiscountType discountType = DiscountType.Amount, decimal discountValue = 0)
        {
            if (quantity <= 0) throw new DomainException("La cantidad debe ser mayor que cero.");
            if (unitPrice < 0) throw new DomainException("El precio no puede ser negativo.");
            return new SaleItem
            {
                ProductId = productId, ProductName = productName, WarehouseId = warehouseId,
                Quantity = quantity, UnitPrice = unitPrice, DiscountType = discountType,
                DiscountValue = discountValue < 0 ? 0 : discountValue
            };
        }

        public static SaleItem Restore(Guid id, DateTime createdAt, DateTime? updatedAt,
            Guid productId, string productName, Guid warehouseId, int quantity, decimal unitPrice,
            DiscountType discountType, decimal discountValue)
        {
            var item = new SaleItem(id)
            {
                ProductId = productId, ProductName = productName, WarehouseId = warehouseId,
                Quantity = quantity, UnitPrice = unitPrice, DiscountType = discountType, DiscountValue = discountValue
            };
            item.CreatedAt = createdAt;
            item.UpdatedAt = updatedAt;
            return item;
        }

        /// <summary>Descuento absoluto del renglón (según tipo % o monto).</summary>
        public decimal LineDiscount =>
            DiscountType == DiscountType.Percentage
                ? Math.Round(UnitPrice * Quantity * (DiscountValue / 100m), 2)
                : DiscountValue;

        public decimal LineTotal => Math.Max(0, (UnitPrice * Quantity) - LineDiscount);
    }

    /// <summary>Pago (o parte de un pago mixto) de una venta (TPV: SalePayment).</summary>
    public class SalePayment : Entity
    {
        public Guid          SaleId        { get; private set; }
        public PaymentMethod Method        { get; private set; }
        public decimal       Amount        { get; private set; }
        public Currency      Currency      { get; private set; }
        public decimal?      AmountUSD     { get; private set; }
        public string?       TransactionId { get; private set; }
        public decimal?      CashTendered  { get; private set; }
        public decimal?      Change        { get; private set; }
        public Currency?     ChangeCurrency{ get; private set; }

        private SalePayment() { }
        private SalePayment(Guid id) : base(id) { }

        public static SalePayment Create(PaymentMethod method, decimal amount, Currency currency,
            decimal? amountUsd = null, string? transactionId = null, decimal? cashTendered = null,
            decimal? change = null, Currency? changeCurrency = null)
            => new()
            {
                Method = method, Amount = amount, Currency = currency, AmountUSD = amountUsd,
                TransactionId = transactionId, CashTendered = cashTendered, Change = change, ChangeCurrency = changeCurrency
            };

        public static SalePayment Restore(Guid id, DateTime createdAt, DateTime? updatedAt,
            PaymentMethod method, decimal amount, Currency currency, decimal? amountUsd, string? transactionId,
            decimal? cashTendered, decimal? change, Currency? changeCurrency)
        {
            var payment = new SalePayment(id)
            {
                Method = method, Amount = amount, Currency = currency, AmountUSD = amountUsd,
                TransactionId = transactionId, CashTendered = cashTendered, Change = change, ChangeCurrency = changeCurrency
            };
            payment.CreatedAt = createdAt;
            payment.UpdatedAt = updatedAt;
            return payment;
        }
    }

    /// <summary>Venta del POS (TPV: Sale). Raíz de agregado con renglones y pagos.</summary>
    public class Sale : Entity, IAggregateRoot
    {
        public Guid          TenantId       { get; private set; }
        public DateTime      Date           { get; private set; }
        public decimal       Subtotal       { get; private set; }
        public decimal       Discount       { get; private set; }
        public decimal       Total          { get; private set; }
        public decimal?      SubtotalUSD    { get; private set; }
        public decimal?      TotalUSD       { get; private set; }
        public decimal?      TaxAmount      { get; private set; }
        public PaymentMethod PaymentMethod  { get; private set; }
        public Guid          CashierId      { get; private set; }
        public Guid          RegisterId     { get; private set; }
        public string?       TerminalName   { get; private set; }
        public Currency      PaymentCurrency{ get; private set; }
        public decimal?      ExchangeRate   { get; private set; }
        public SaleStatus    Status         { get; private set; } = SaleStatus.Completed;

        private readonly List<SaleItem> _items = new();
        public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

        private readonly List<SalePayment> _payments = new();
        public IReadOnlyCollection<SalePayment> Payments => _payments.AsReadOnly();

        private Sale() { }
        private Sale(Guid id) : base(id) { }

        public static Sale Create(Guid tenantId, Guid cashierId, Guid registerId, PaymentMethod paymentMethod,
            Currency paymentCurrency, IEnumerable<SaleItem> items, IEnumerable<SalePayment> payments,
            decimal? exchangeRate = null, decimal? taxAmount = null, string? terminalName = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("La venta debe pertenecer a un negocio.");
            var itemList = items?.ToList() ?? new List<SaleItem>();
            if (itemList.Count == 0) throw new DomainException("La venta debe tener al menos un producto.");

            var sale = new Sale
            {
                TenantId = tenantId, CashierId = cashierId, RegisterId = registerId, Date = DateTime.UtcNow,
                PaymentMethod = paymentMethod, PaymentCurrency = paymentCurrency, ExchangeRate = exchangeRate,
                TaxAmount = taxAmount, TerminalName = terminalName
            };
            foreach (var it in itemList) sale._items.Add(it);
            foreach (var p in payments ?? Enumerable.Empty<SalePayment>()) sale._payments.Add(p);

            sale.Subtotal = sale._items.Sum(i => i.UnitPrice * i.Quantity);
            sale.Discount = sale._items.Sum(i => i.LineDiscount);
            sale.Total    = sale._items.Sum(i => i.LineTotal);
            if (exchangeRate is > 0)
            {
                sale.SubtotalUSD = Math.Round(sale.Subtotal / exchangeRate.Value, 2);
                sale.TotalUSD    = Math.Round(sale.Total    / exchangeRate.Value, 2);
            }
            return sale;
        }

        public void MarkRefunded()
        {
            if (Status == SaleStatus.Refunded) throw new DomainException("La venta ya fue reembolsada.");
            Status = SaleStatus.Refunded; SetUpdated();
        }

        /// <summary>Reemplaza los renglones de una venta ya registrada y recalcula sus totales.
        /// El ajuste de stock (restaurar lo viejo, descontar lo nuevo) lo hace el handler antes de llamar aquí.</summary>
        public void UpdateItems(IEnumerable<SaleItem> newItems)
        {
            if (Status == SaleStatus.Refunded)
                throw new DomainException("No se puede editar una venta reembolsada.");

            var itemList = newItems?.ToList() ?? new List<SaleItem>();
            if (itemList.Count == 0) throw new DomainException("La venta debe tener al menos un producto.");

            _items.Clear();
            foreach (var it in itemList) _items.Add(it);

            Subtotal = _items.Sum(i => i.UnitPrice * i.Quantity);
            Discount = _items.Sum(i => i.LineDiscount);
            Total    = _items.Sum(i => i.LineTotal);
            if (ExchangeRate is > 0)
            {
                SubtotalUSD = Math.Round(Subtotal / ExchangeRate.Value, 2);
                TotalUSD    = Math.Round(Total    / ExchangeRate.Value, 2);
            }
            SetUpdated();
        }

        public static Sale Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            DateTime date, decimal subtotal, decimal discount, decimal total, decimal? subtotalUsd, decimal? totalUsd,
            decimal? taxAmount, PaymentMethod paymentMethod, Guid cashierId, Guid registerId, string? terminalName,
            Currency paymentCurrency, decimal? exchangeRate, SaleStatus status,
            IEnumerable<SaleItem> items, IEnumerable<SalePayment> payments)
        {
            var sale = new Sale(id)
            {
                TenantId = tenantId, Date = date, Subtotal = subtotal, Discount = discount, Total = total,
                SubtotalUSD = subtotalUsd, TotalUSD = totalUsd, TaxAmount = taxAmount, PaymentMethod = paymentMethod,
                CashierId = cashierId, RegisterId = registerId, TerminalName = terminalName,
                PaymentCurrency = paymentCurrency, ExchangeRate = exchangeRate, Status = status
            };
            foreach (var it in items) sale._items.Add(it);
            foreach (var p in payments) sale._payments.Add(p);
            sale.CreatedAt = createdAt;
            sale.UpdatedAt = updatedAt;
            return sale;
        }
    }
}
