namespace BusinessSearcher.Application.DTOs.Operations
{
    // ── Categorías ──
    public record CategoryDto(Guid Id, string Name, string? Description, string? Code);
    public record CreateCategoryDto(string Name, string? Description = null, string? Code = null);

    // ── Existencias por almacén ──
    /// <summary>SellPrice/SellPriceUSD: precio propio de este almacén (null = usa el precio general del producto).</summary>
    public record ProductStockDto(Guid WarehouseId, int Quantity, decimal? AverageCost, decimal? AverageCostUSD,
        decimal? SellPrice = null, decimal? SellPriceUSD = null);

    // ── Productos ──
    public record ProductDto(
        Guid Id, string? Barcode, string Name, string? Description, string Unit,
        Guid? CategoryId, decimal CostPrice, decimal SellPrice, decimal? CostPriceUSD, decimal? SellPriceUSD,
        int MinStock, decimal? TaxRate, string? BatchNumber, DateTime? ExpirationDate, bool ForSale,
        int TotalStock, IReadOnlyList<ProductStockDto> Stocks,
        string? ImageUrl, bool IsPubliclyVisible, int MinOrderQuantity);

    public record CreateProductDto(
        string Name, decimal CostPrice, decimal SellPrice, string? Barcode = null, string? Description = null,
        string Unit = "unidad", Guid? CategoryId = null, decimal? CostPriceUSD = null, decimal? SellPriceUSD = null,
        int MinStock = 0, decimal? TaxRate = null, string? BatchNumber = null, DateTime? ExpirationDate = null,
        bool ForSale = true,
        // stock inicial opcional (almacén + cantidad)
        Guid? InitialWarehouseId = null, int InitialStock = 0, string? PriceChangeReason = null,
        string? ImageUrl = null, int MinOrderQuantity = 1,
        // visible en la búsqueda pública de la app desde el momento de creación
        bool IsPubliclyVisible = true);

    public record SetProductPublicVisibilityDto(bool IsPubliclyVisible);

    // ── Importación de catálogo desde Excel ──
    /// <summary>Status: "Creado" (producto nuevo), "Sumado" (se sumó stock al que ya había en el almacén),
    /// "Reactivado" (estaba desactivado: vuelve a estar activo y se suma su stock).
    /// PriceDecision: "accepted" / "kept" cuando hubo conflicto de precio que el usuario resolvió; null si no hubo.</summary>
    public record ImportedProductDto(string Barcode, Guid ProductId, string Status, int PreviousStock, int NewStock,
        string? PriceDecision);

    /// <summary>Producto que ya existe con otro precio en el archivo. Precios en USD.</summary>
    public record PriceConflictDto(Guid ProductId, string Barcode, string Name, decimal SystemPriceUsd, decimal ExcelPriceUsd);

    /// <summary>NeedsDecision = true: el archivo tiene conflictos de precio sin resolver. No se escribió nada;
    /// hay que reenviar el mismo archivo con la decisión de cada conflicto.</summary>
    public record ImportProductsResultDto(
        bool NeedsDecision, IReadOnlyList<PriceConflictDto> PriceConflicts,
        int CreatedCount, int AddedCount, int ReactivatedCount, int PricesUpdatedCount, int CategoriesCreatedCount,
        IReadOnlyList<ImportedProductDto> Products, IReadOnlyList<string> Warnings);

    // ── Feed de notificaciones operativas (stock bajo + cambios de precio recientes) ──
    public record OperationalNotificationDto(string Type, string Title, string Message, DateTime Date, Guid? ProductId);

    // ── Ajuste manual de stock ──
    public record AdjustStockDto(Guid WarehouseId, int Delta);

    // ── Historial de precios ──
    public record ProductPriceHistoryDto(
        Guid Id, Guid ProductId, decimal OldCostPrice, decimal NewCostPrice, decimal OldSellPrice, decimal NewSellPrice,
        decimal? OldCostPriceUSD, decimal? NewCostPriceUSD, decimal? OldSellPriceUSD, decimal? NewSellPriceUSD,
        DateTime ChangeDate, Guid? UserId, string? Reason);
}
