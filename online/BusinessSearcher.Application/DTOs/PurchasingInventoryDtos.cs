namespace BusinessSearcher.Application.DTOs.Operations
{
    // ── Solicitudes de compra ──
    public record PurchaseRequestDto(
        Guid Id, Guid ProductId, string ProductName, Guid WarehouseId, int CurrentStock, int MinStock,
        int RequestedQuantity, string Status, Guid? UserId, string? Notes, DateTime Date);

    public record CreatePurchaseRequestDto(Guid ProductId, Guid WarehouseId, int RequestedQuantity, string? Notes = null);
    public record UpdatePurchaseRequestQtyDto(int RequestedQuantity);

    // ── Compras ──
    public record PurchaseItemDto(
        Guid ProductId, string ProductName, int Quantity, decimal CostPrice, decimal LineTotal,
        string? BatchNumber, DateTime? ExpirationDate);

    public record PurchaseDto(
        Guid Id, Guid? SupplierId, Guid WarehouseId, decimal Total, decimal? TotalUSD, decimal AssociatedExpenses,
        string Currency, decimal? ExchangeRate, string Status, string? InvoiceUrl, DateTime Date,
        IReadOnlyList<PurchaseItemDto> Items, IReadOnlyList<Guid> PurchaseRequestIds);

    /// <summary>Alta rápida de producto "al vuelo" durante el registro de una compra.</summary>
    public record NewPurchaseProductDto(
        string Name, string Unit = "unidad", Guid? CategoryId = null, string? Barcode = null,
        decimal? SellPriceUSD = null, int MinStock = 0, decimal? TaxRate = null);

    public record CreatePurchaseItemDto(
        // Si ProductId es null, se requiere NewProduct para dar de alta el producto en la misma compra.
        Guid? ProductId, int Quantity, decimal CostPrice, string? BatchNumber = null, DateTime? ExpirationDate = null,
        // si el precio de venta cambia, se actualiza el producto
        decimal? NewSellPrice = null, NewPurchaseProductDto? NewProduct = null);

    public record CreatePurchaseDto(
        Guid WarehouseId, IReadOnlyList<CreatePurchaseItemDto> Items, string Currency = "CUP",
        Guid? SupplierId = null, decimal AssociatedExpenses = 0, decimal? ExchangeRate = null,
        string? InvoiceUrl = null, IReadOnlyList<Guid>? PurchaseRequestIds = null);

    // ── Movimientos de inventario ──
    public record InventoryMovementDto(
        Guid Id, Guid ProductId, string ProductName, string Type, int Quantity,
        Guid? FromWarehouseId, Guid? ToWarehouseId, string? Reason, DateTime Date);

    public record CreateInventoryMovementDto(
        Guid ProductId, string Type, int Quantity, Guid? FromWarehouseId = null, Guid? ToWarehouseId = null, string? Reason = null);

    // ── Conversión/desglose de inventario (mezclar N productos origen en un producto destino con otra unidad) ──
    public record ConvertInventoryItemDto(Guid ProductId, int Quantity);

    public record ConvertInventoryDto(
        Guid WarehouseId, IReadOnlyList<ConvertInventoryItemDto> SourceItems,
        int DestinationQuantity, Guid? DestinationProductId = null,
        NewPurchaseProductDto? NewDestinationProduct = null, string? Reason = null);

    // ── Conteo de inventario ──
    public record InventoryCountItemDto(
        Guid ProductId, string ProductName, int SystemQuantity, int CountedQuantity, int Difference, bool Audited);

    public record InventoryCountDto(
        Guid Id, Guid WarehouseId, string Status, bool Adjusted, DateTime Date,
        IReadOnlyList<InventoryCountItemDto> Items);

    public record CreateInventoryCountItemDto(Guid ProductId, int CountedQuantity);
    public record CreateInventoryCountDto(Guid WarehouseId, IReadOnlyList<CreateInventoryCountItemDto> Items);
    public record CloseInventoryCountDto(bool ApplyAdjustments);
    public record SetInventoryCountItemAuditedDto(bool Audited);
}
