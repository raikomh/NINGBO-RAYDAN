using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>Categoría de productos del negocio (TPV: Category).</summary>
    public class Category : Entity, IAggregateRoot
    {
        public Guid    TenantId    { get; private set; }
        public string  Name        { get; private set; } = default!;
        public string? Description { get; private set; }
        public string? Code        { get; private set; }
        public bool    IsActive    { get; private set; } = true;

        private Category() { }
        private Category(Guid id) : base(id) { }

        public static Category Create(Guid tenantId, string name, string? description = null, string? code = null)
        {
            if (tenantId == Guid.Empty) throw new DomainException("La categoría debe pertenecer a un negocio.");
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre de la categoría es requerido.");
            return new Category { TenantId = tenantId, Name = name.Trim(), Description = description?.Trim(), Code = code?.Trim() };
        }

        public static Category Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            string name, string? description, string? code, bool isActive)
        {
            var category = new Category(id) { TenantId = tenantId, Name = name, Description = description, Code = code, IsActive = isActive };
            category.CreatedAt = createdAt;
            category.UpdatedAt = updatedAt;
            return category;
        }

        public void Update(string name, string? description, string? code)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre de la categoría es requerido.");
            Name = name.Trim(); Description = description?.Trim(); Code = code?.Trim(); SetUpdated();
        }

        public void Deactivate() { IsActive = false; SetUpdated(); }
    }

    /// <summary>
    /// Producto del catálogo del negocio (TPV: Product). Precios en doble moneda CUP/USD.
    /// El stock vive por-almacén en <see cref="ProductStock"/>.
    /// </summary>
    public class Product : Entity, IAggregateRoot
    {
        public Guid      TenantId       { get; private set; }
        public string?   Barcode        { get; private set; }
        public string    Name           { get; private set; } = default!;
        public string?   Description    { get; private set; }
        public string    Unit           { get; private set; } = "unidad";
        public Guid?     CategoryId     { get; private set; }
        public decimal   CostPrice      { get; private set; }
        public decimal   SellPrice      { get; private set; }
        public decimal?  CostPriceUSD   { get; private set; }
        public decimal?  SellPriceUSD   { get; private set; }
        public int       MinStock       { get; private set; }
        public decimal?  TaxRate        { get; private set; }
        public string?   BatchNumber    { get; private set; }
        public DateTime? ExpirationDate { get; private set; }
        public bool      ForSale        { get; private set; } = true;
        public bool      IsActive       { get; private set; } = true;

        /// <summary>URL de la foto del producto (Cloudinary).</summary>
        public string?   ImageUrl          { get; private set; }
        /// <summary>Si es visible en la búsqueda pública de la app (catálogo del mercado).</summary>
        public bool      IsPubliclyVisible { get; private set; }
        /// <summary>Cantidad mínima de unidades por pedido (catálogos mayoristas; 1 = sin mínimo real).</summary>
        public int       MinOrderQuantity  { get; private set; } = 1;

        private readonly List<ProductStock> _stocks = new();
        public IReadOnlyCollection<ProductStock> Stocks => _stocks.AsReadOnly();

        private Product() { }
        private Product(Guid id) : base(id) { }

        public static Product Create(Guid tenantId, string name, decimal costPrice, decimal sellPrice,
            string? barcode = null, string? description = null, string unit = "unidad", Guid? categoryId = null,
            decimal? costPriceUsd = null, decimal? sellPriceUsd = null, int minStock = 0, decimal? taxRate = null,
            string? batchNumber = null, DateTime? expirationDate = null, bool forSale = true,
            string? imageUrl = null, bool isPubliclyVisible = false, int minOrderQuantity = 1)
        {
            if (tenantId == Guid.Empty) throw new DomainException("El producto debe pertenecer a un negocio.");
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del producto es requerido.");
            if (costPrice < 0 || sellPrice < 0) throw new DomainException("Los precios no pueden ser negativos.");
            return new Product
            {
                TenantId = tenantId, Name = name.Trim(), Barcode = barcode?.Trim(), Description = description?.Trim(),
                Unit = string.IsNullOrWhiteSpace(unit) ? "unidad" : unit.Trim(), CategoryId = categoryId,
                CostPrice = costPrice, SellPrice = sellPrice, CostPriceUSD = costPriceUsd, SellPriceUSD = sellPriceUsd,
                MinStock = minStock < 0 ? 0 : minStock, TaxRate = taxRate, BatchNumber = batchNumber?.Trim(),
                ExpirationDate = expirationDate, ForSale = forSale,
                ImageUrl = imageUrl?.Trim(), IsPubliclyVisible = isPubliclyVisible,
                MinOrderQuantity = minOrderQuantity < 1 ? 1 : minOrderQuantity
            };
        }

        public void Update(string name, decimal costPrice, decimal sellPrice, string? barcode, string? description,
            string unit, Guid? categoryId, decimal? costPriceUsd, decimal? sellPriceUsd, int minStock,
            decimal? taxRate, string? batchNumber, DateTime? expirationDate, bool forSale,
            string? imageUrl = null, int minOrderQuantity = 1)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("El nombre del producto es requerido.");
            if (costPrice < 0 || sellPrice < 0) throw new DomainException("Los precios no pueden ser negativos.");
            Name = name.Trim(); Barcode = barcode?.Trim(); Description = description?.Trim();
            Unit = string.IsNullOrWhiteSpace(unit) ? "unidad" : unit.Trim(); CategoryId = categoryId;
            CostPrice = costPrice; SellPrice = sellPrice; CostPriceUSD = costPriceUsd; SellPriceUSD = sellPriceUsd;
            MinStock = minStock < 0 ? 0 : minStock; TaxRate = taxRate; BatchNumber = batchNumber?.Trim();
            ExpirationDate = expirationDate; ForSale = forSale; ImageUrl = imageUrl?.Trim();
            MinOrderQuantity = minOrderQuantity < 1 ? 1 : minOrderQuantity; SetUpdated();
        }

        public void Deactivate() { IsActive = false; SetUpdated(); }

        /// <summary>Reactiva un producto desactivado (p. ej. al volver a importar su código).</summary>
        public void Activate() { IsActive = true; SetUpdated(); }

        public void SetPublicVisibility(bool isPubliclyVisible)
        {
            IsPubliclyVisible = isPubliclyVisible;
            SetUpdated();
        }

        /// <summary>Stock total sumando todos los almacenes.</summary>
        public int TotalStock => _stocks.Sum(s => s.Quantity);

        public ProductStock StockFor(Guid warehouseId)
        {
            var s = _stocks.FirstOrDefault(x => x.WarehouseId == warehouseId);
            if (s is null)
            {
                s = ProductStock.Create(Id, warehouseId, 0);
                _stocks.Add(s);
            }
            return s;
        }

        /// <summary>Precio de venta que aplica en un almacén: el propio de ese almacén si lo tiene, o el general del producto.</summary>
        public (decimal SellPrice, decimal? SellPriceUSD) PriceFor(Guid warehouseId)
        {
            var s = _stocks.FirstOrDefault(x => x.WarehouseId == warehouseId);
            if (s is null || (s.SellPrice is null && s.SellPriceUSD is null))
                return (SellPrice, SellPriceUSD);
            return (s.SellPrice ?? SellPrice, s.SellPriceUSD ?? SellPriceUSD);
        }

        /// <summary>Fija el precio propio de un almacén (punto de venta). No cambia el precio general del producto.</summary>
        public void SetPriceForWarehouse(Guid warehouseId, decimal? sellPrice, decimal? sellPriceUsd)
        {
            StockFor(warehouseId).SetPriceOverride(sellPrice, sellPriceUsd);
            SetUpdated();
        }

        /// <summary>Ajusta (suma o resta) el stock en un almacén. Lanza si el resultado sería negativo.</summary>
        public void AdjustStock(Guid warehouseId, int delta)
        {
            var s = StockFor(warehouseId);
            if (s.Quantity + delta < 0)
                throw new DomainException($"Stock insuficiente de '{Name}' en el almacén (disponible {s.Quantity}, requerido {-delta}).");
            s.SetQuantity(s.Quantity + delta);
            SetUpdated();
        }

        public static Product Restore(Guid id, DateTime createdAt, DateTime? updatedAt, Guid tenantId,
            string? barcode, string name, string? description, string unit, Guid? categoryId,
            decimal costPrice, decimal sellPrice, decimal? costPriceUsd, decimal? sellPriceUsd,
            int minStock, decimal? taxRate, string? batchNumber, DateTime? expirationDate,
            bool forSale, bool isActive, string? imageUrl, bool isPubliclyVisible, int minOrderQuantity,
            IEnumerable<ProductStock> stocks)
        {
            var product = new Product(id)
            {
                TenantId = tenantId, Barcode = barcode, Name = name, Description = description, Unit = unit,
                CategoryId = categoryId, CostPrice = costPrice, SellPrice = sellPrice,
                CostPriceUSD = costPriceUsd, SellPriceUSD = sellPriceUsd, MinStock = minStock, TaxRate = taxRate,
                BatchNumber = batchNumber, ExpirationDate = expirationDate, ForSale = forSale, IsActive = isActive,
                ImageUrl = imageUrl, IsPubliclyVisible = isPubliclyVisible, MinOrderQuantity = minOrderQuantity
            };
            foreach (var s in stocks) product._stocks.Add(s);
            product.CreatedAt = createdAt;
            product.UpdatedAt = updatedAt;
            return product;
        }
    }

    /// <summary>Existencia de un producto en un almacén concreto (TPV: warehouseStocks).</summary>
    public class ProductStock : Entity
    {
        public Guid     ProductId      { get; private set; }
        public Guid     WarehouseId    { get; private set; }
        public int      Quantity       { get; private set; }
        public decimal? AverageCost    { get; private set; }
        public decimal? AverageCostUSD { get; private set; }
        /// <summary>Precio de venta propio de este almacén (punto de venta). Null = usa el precio general del producto.</summary>
        public decimal? SellPrice      { get; private set; }
        public decimal? SellPriceUSD   { get; private set; }

        private ProductStock() { }
        private ProductStock(Guid id) : base(id) { }

        public static ProductStock Create(Guid productId, Guid warehouseId, int quantity,
            decimal? averageCost = null, decimal? averageCostUsd = null)
            => new() { ProductId = productId, WarehouseId = warehouseId, Quantity = quantity,
                       AverageCost = averageCost, AverageCostUSD = averageCostUsd };

        public void SetQuantity(int quantity) { Quantity = quantity; SetUpdated(); }

        public void SetAverageCost(decimal? cost, decimal? costUsd)
        { AverageCost = cost; AverageCostUSD = costUsd; SetUpdated(); }

        /// <summary>Fija el precio de venta solo para este almacén. Los valores nulos quitan el precio propio.</summary>
        public void SetPriceOverride(decimal? sellPrice, decimal? sellPriceUsd)
        {
            if (sellPrice < 0 || sellPriceUsd < 0)
                throw new DomainException("Los precios no pueden ser negativos.");
            SellPrice = sellPrice; SellPriceUSD = sellPriceUsd; SetUpdated();
        }

        public static ProductStock Restore(Guid id, DateTime createdAt, DateTime? updatedAt,
            Guid productId, Guid warehouseId, int quantity, decimal? averageCost, decimal? averageCostUsd)
        {
            var stock = new ProductStock(id)
            {
                ProductId = productId, WarehouseId = warehouseId, Quantity = quantity,
                AverageCost = averageCost, AverageCostUSD = averageCostUsd
            };
            stock.CreatedAt = createdAt;
            stock.UpdatedAt = updatedAt;
            return stock;
        }
    }

    /// <summary>Historial de cambios de precio de un producto (TPV: ProductPriceHistory).</summary>
    public class ProductPriceHistory : Entity, IAggregateRoot
    {
        public Guid     TenantId        { get; private set; }
        public Guid     ProductId       { get; private set; }
        public decimal  OldCostPrice    { get; private set; }
        public decimal  NewCostPrice    { get; private set; }
        public decimal  OldSellPrice    { get; private set; }
        public decimal  NewSellPrice    { get; private set; }
        public decimal? OldCostPriceUSD { get; private set; }
        public decimal? NewCostPriceUSD { get; private set; }
        public decimal? OldSellPriceUSD { get; private set; }
        public decimal? NewSellPriceUSD { get; private set; }
        public DateTime ChangeDate      { get; private set; }
        public Guid?    UserId          { get; private set; }
        public string?  Reason          { get; private set; }

        private ProductPriceHistory() { }

        public static ProductPriceHistory Create(Guid tenantId, Guid productId,
            decimal oldCost, decimal newCost, decimal oldSell, decimal newSell,
            decimal? oldCostUsd = null, decimal? newCostUsd = null, decimal? oldSellUsd = null, decimal? newSellUsd = null,
            Guid? userId = null, string? reason = null)
            => new()
            {
                TenantId = tenantId, ProductId = productId,
                OldCostPrice = oldCost, NewCostPrice = newCost, OldSellPrice = oldSell, NewSellPrice = newSell,
                OldCostPriceUSD = oldCostUsd, NewCostPriceUSD = newCostUsd, OldSellPriceUSD = oldSellUsd, NewSellPriceUSD = newSellUsd,
                ChangeDate = DateTime.UtcNow, UserId = userId, Reason = reason?.Trim()
            };
    }
}
