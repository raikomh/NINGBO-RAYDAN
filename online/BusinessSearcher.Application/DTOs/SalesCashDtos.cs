namespace BusinessSearcher.Application.DTOs.Operations
{
    // ── Ventas / POS ──
    public record SaleItemDto(
        Guid ProductId, string ProductName, Guid WarehouseId, int Quantity, decimal UnitPrice,
        string DiscountType, decimal DiscountValue, decimal LineDiscount, decimal LineTotal);

    public record SalePaymentDto(
        string Method, decimal Amount, string Currency, decimal? AmountUSD, string? TransactionId,
        decimal? CashTendered, decimal? Change, string? ChangeCurrency);

    public record SaleDto(
        Guid Id, DateTime Date, decimal Subtotal, decimal Discount, decimal Total,
        decimal? SubtotalUSD, decimal? TotalUSD, decimal? TaxAmount, string PaymentMethod, string PaymentCurrency,
        decimal? ExchangeRate, string Status, Guid CashierId, Guid RegisterId, string? WarehouseName,
        IReadOnlyList<SaleItemDto> Items, IReadOnlyList<SalePaymentDto> Payments, string? CashierName = null,
        string? ManagerCode = null);

    // ── Gestores ──
    public record ManagerDto(
        Guid Id, string Code, string Name, string IdNumber, string Municipality, string Province, string Phone, bool IsActive);
    public record CreateManagerDto(string Code, string Name, string IdNumber, string Municipality, string Province, string Phone);
    public record UpdateManagerDto(
        string Code, string Name, string IdNumber, string Municipality, string Province, string Phone, bool IsActive = true);

    // Renglón entrante del carrito
    public record CreateSaleItemDto(
        Guid ProductId, Guid WarehouseId, int Quantity, decimal? UnitPriceOverride = null,
        string DiscountType = "Amount", decimal DiscountValue = 0);

    public record CreateSalePaymentDto(
        string Method, decimal Amount, string Currency = "CUP", decimal? AmountUSD = null,
        string? TransactionId = null, decimal? CashTendered = null, decimal? Change = null, string? ChangeCurrency = null);

    public record CreateSaleDto(
        Guid RegisterId, string PaymentMethod, string PaymentCurrency,
        IReadOnlyList<CreateSaleItemDto> Items, IReadOnlyList<CreateSalePaymentDto> Payments,
        decimal? ExchangeRate = null, decimal? TaxAmount = null,
        string? ManagerCode = null);

    /// <summary>Edita los renglones de una venta ya registrada (reajusta stock viejo vs nuevo). No modifica pagos.</summary>
    public record UpdateSaleDto(IReadOnlyList<CreateSaleItemDto> Items);

    // ── Importación de ventas (Excel: Código, Producto, Cantidad, Precio) ──
    /// <summary>Fila cuyo precio en el Excel difiere del precio actual del producto en el sistema.</summary>
    public record SalesImportPriceDiffDto(
        int RowNumber, string ProductName, string? Barcode, int Quantity, decimal SystemPrice, decimal ExcelPrice);

    /// <summary>
    /// Resultado de importar ventas. Si <c>NeedsPriceConfirmation</c> es true, no se registró nada todavía:
    /// el cliente debe reenviar la misma importación indicando <c>AcceptNewPrices</c> (true = actualiza los
    /// precios del catálogo con los del Excel; false = conserva los precios del sistema).
    /// </summary>
    public record ImportSalesResultDto(
        bool Success, bool NeedsPriceConfirmation, int ImportedCount, int PricesUpdatedCount,
        IReadOnlyList<SalesImportPriceDiffDto> PriceDifferences, IReadOnlyList<string> Errors);

    // ── Caja / arqueo ──
    public record CashMovementDto(
        Guid Id, Guid RegisterId, DateTime Date, string Type, decimal Amount, decimal? AmountUSD,
        string Currency, string? Description, Guid? UserId);

    public record CashRegisterDto(
        Guid Id, Guid? WarehouseId, DateTime OpenDate, DateTime? CloseDate,
        decimal InitialAmount, decimal? ExpectedAmount, decimal? ActualAmount, decimal? Difference,
        decimal? InitialAmountUSD, decimal? ActualAmountUSD, decimal? DifferenceUSD, string Status,
        Guid OpenedBy, Guid? ClosedBy, int SalesCount, decimal TotalSales, decimal TotalExpenses,
        decimal TotalCashIn, decimal TotalCashOut, bool InventoryCountCompleted);

    public record OpenCashRegisterDto(decimal InitialAmount, Guid? WarehouseId = null, decimal? InitialAmountUSD = null);
    public record CloseCashRegisterDto(decimal ActualAmount, decimal? ActualAmountUSD = null);
    public record CreateCashMovementDto(Guid RegisterId, string Type, decimal Amount, string Currency = "CUP", decimal? AmountUSD = null, string? Description = null);
}
