namespace BusinessSearcher.Application.DTOs.Sync
{
    // ── Envelope ──────────────────────────────────────────────────────────────
    // A diferencia de los DTOs de UI en OperationsDtos.cs/SalesCashDtos.cs/etc., estos DTOs de
    // sincronización llevan Id/CreatedAt/UpdatedAt/TenantId de cada registro: se usan para
    // reconstruir entidades completas del lado "online" a partir de lo recolectado en "local".

    public record SyncPushEnvelopeDto(
        Guid LocalTenantId,
        DateTime CollectedAt,
        IReadOnlyList<SyncBusinessInfoDto> BusinessInfos,
        IReadOnlyList<SyncSupplierDto> Suppliers,
        IReadOnlyList<SyncWarehouseDto> Warehouses,
        IReadOnlyList<SyncCategoryDto> Categories,
        IReadOnlyList<SyncProductDto> Products,
        IReadOnlyList<SyncSaleDto> Sales,
        IReadOnlyList<SyncPurchaseDto> Purchases,
        IReadOnlyList<SyncInventoryMovementDto> InventoryMovements,
        IReadOnlyList<SyncCashRegisterDto> CashRegisters,
        IReadOnlyList<SyncOperationsUserDto> OperationsUsers,
        IReadOnlyList<SyncExpenseDto> Expenses)
    {
        public int TotalItems =>
            BusinessInfos.Count + Suppliers.Count + Warehouses.Count + Categories.Count + Products.Count +
            Sales.Count + Purchases.Count + InventoryMovements.Count + CashRegisters.Count +
            OperationsUsers.Count + Expenses.Count;
    }

    // ── Resultados ────────────────────────────────────────────────────────────

    /// <summary>Respuesta del endpoint de ingesta; su forma debe coincidir con SyncPushResultDto (local).</summary>
    public record SyncIngestResultDto(int ItemsSent, int ItemsAccepted, IReadOnlyList<string> Conflicts);

    public record SyncHistoryEntryDto(
        Guid Id, string Direction, string Status, DateTime StartedAt, DateTime? CompletedAt,
        int ItemsSent, int ItemsAccepted, string? ErrorMessage, string? CounterpartyUrl);

    /// <summary>
    /// La API key en claro se devuelve una única vez, al generarla: solo se persiste su hash
    /// (<c>Tenant.SyncApiKeyHash</c>), así que si se pierde hay que generar una nueva.
    /// </summary>
    public record SyncApiKeyResultDto(string ApiKey);

    /// <summary>
    /// Snapshot de suscripción del tenant online, servido a la instalación Local emparejada vía
    /// <c>GetTenantSyncStatusQuery</c> (autenticado por API key, igual que ingest/export — no
    /// requiere una sesión online iniciada).
    /// </summary>
    public record SyncStatusDto(
        bool IsApproved, string Status, DateTime? LastPaymentDate, DateTime? NextPaymentDate, bool IsSubscriptionActive);

    // ── Administración ────────────────────────────────────────────────────────

    public record SyncBusinessInfoDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        string Name, string? Address, string? Phone, string? Email, string? TaxId, string? LogoUrl);

    // ── Compras/almacén ───────────────────────────────────────────────────────

    public record SyncSupplierDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        string Name, string? Contact, string Phone, string? Email, string? Address,
        double? Latitude, double? Longitude, string? Category, bool IsActive);

    public record SyncWarehouseDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        string Name, string? Location, string? Description, bool IsActive);

    // ── Catálogo ──────────────────────────────────────────────────────────────

    public record SyncCategoryDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        string Name, string? Description, string? Code, bool IsActive);

    public record SyncProductStockDto(
        Guid Id, DateTime CreatedAt, DateTime? UpdatedAt,
        Guid ProductId, Guid WarehouseId, int Quantity, decimal? AverageCost, decimal? AverageCostUSD);

    public record SyncProductDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        string? Barcode, string Name, string? Description, string Unit, Guid? CategoryId,
        decimal CostPrice, decimal SellPrice, decimal? CostPriceUSD, decimal? SellPriceUSD,
        int MinStock, decimal? TaxRate, string? BatchNumber, DateTime? ExpirationDate,
        bool ForSale, bool IsActive, string? ImageUrl, bool IsPubliclyVisible, int MinOrderQuantity,
        IReadOnlyList<SyncProductStockDto> Stocks);

    // ── Ventas ────────────────────────────────────────────────────────────────

    public record SyncSaleItemDto(
        Guid Id, DateTime CreatedAt, DateTime? UpdatedAt,
        Guid ProductId, string ProductName, Guid WarehouseId, int Quantity, decimal UnitPrice,
        string DiscountType, decimal DiscountValue);

    public record SyncSalePaymentDto(
        Guid Id, DateTime CreatedAt, DateTime? UpdatedAt,
        string Method, decimal Amount, string Currency, decimal? AmountUSD, string? TransactionId,
        decimal? CashTendered, decimal? Change, string? ChangeCurrency);

    public record SyncSaleDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        DateTime Date, decimal Subtotal, decimal Discount, decimal Total, decimal? SubtotalUSD, decimal? TotalUSD,
        decimal? TaxAmount, string PaymentMethod, Guid CashierId, Guid RegisterId, string? WarehouseName,
        string PaymentCurrency, decimal? ExchangeRate, string Status,
        IReadOnlyList<SyncSaleItemDto> Items, IReadOnlyList<SyncSalePaymentDto> Payments, string? ManagerCode = null);

    // ── Compras ───────────────────────────────────────────────────────────────

    public record SyncPurchaseItemDto(
        Guid Id, DateTime CreatedAt, DateTime? UpdatedAt,
        Guid ProductId, string ProductName, int Quantity, decimal CostPrice,
        string? BatchNumber, DateTime? ExpirationDate);

    public record SyncPurchaseDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        Guid? SupplierId, Guid WarehouseId, Guid? UserId, decimal Total, decimal? TotalUSD,
        decimal AssociatedExpenses, decimal? AssociatedExpensesUSD, string Currency, decimal? ExchangeRate,
        string Status, string? InvoiceUrl, DateTime Date,
        IReadOnlyList<SyncPurchaseItemDto> Items, IReadOnlyList<Guid> PurchaseRequestIds);

    // ── Inventario ────────────────────────────────────────────────────────────

    public record SyncInventoryMovementDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        Guid ProductId, string ProductName, Guid? FromWarehouseId, Guid? ToWarehouseId,
        int Quantity, string Type, string? Reason, Guid? UserId, DateTime Date);

    // ── Caja ──────────────────────────────────────────────────────────────────

    public record SyncCashMovementDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        Guid RegisterId, DateTime Date, string Type, decimal Amount, decimal? AmountUSD,
        string Currency, string? Description, Guid? UserId);

    public record SyncCashRegisterDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        Guid? WarehouseId, DateTime OpenDate, DateTime? CloseDate,
        decimal InitialAmount, decimal? ExpectedAmount, decimal? ActualAmount, decimal? Difference,
        decimal? InitialAmountUSD, decimal? ExpectedAmountUSD, decimal? ActualAmountUSD, decimal? DifferenceUSD,
        string Status, Guid OpenedBy, Guid? ClosedBy, int SalesCount, decimal TotalSales, decimal TotalExpenses,
        decimal TotalCashIn, decimal TotalCashOut, bool InventoryCountCompleted,
        IReadOnlyList<SyncCashMovementDto> Movements);

    // ── Sub-usuarios operativos ───────────────────────────────────────────────

    public record SyncOperationsUserDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        string Name, string Email, string PasswordHash, string Role, string? AvatarUrl,
        Guid? AssignedRegisterId, Guid? AssignedWarehouseId, bool IsActive);

    // ── Gastos ────────────────────────────────────────────────────────────────

    public record SyncExpenseDto(
        Guid Id, Guid TenantId, DateTime CreatedAt, DateTime? UpdatedAt,
        string Type, decimal Amount, decimal? AmountUSD, string Description,
        Guid? UserId, Guid? RegisterId, DateTime Date);
}
