namespace BusinessSearcher.Application.DTOs.Operations
{
    // ── Reporte de ventas ──
    public record SalesReportRowDto(DateTime Date, Guid RegisterId, string PaymentMethod, string Currency, decimal Total, string Status);
    public record SalesReportDto(int Count, decimal TotalSales, decimal TotalRefunded, IReadOnlyList<SalesReportRowDto> Rows);

    // ── Reporte de inventario ──
    public record InventoryReportRowDto(string ProductName, string? Barcode, int TotalStock, int MinStock, decimal SellPrice, bool LowStock);
    public record InventoryReportDto(int ProductCount, int LowStockCount, decimal InventoryValue, IReadOnlyList<InventoryReportRowDto> Rows);

    // ── Reporte de gastos ──
    public record ExpensesReportRowDto(DateTime Date, string Type, decimal Amount, string Description);
    public record ExpensesReportDto(int Count, decimal Total, IReadOnlyList<ExpensesReportRowDto> Rows);

    public record ReportRangeDto(DateTime? From, DateTime? To);

    // ── Dashboard mensual (ventas, costo de venta, compras, mermas, gastos, ganancia) ──
    // Ganancia = Ventas − Costo de venta − Gastos − Merma. Las compras se informan pero no la afectan.
    public record MonthlyDashboardRowDto(
        int Month, string MonthLabel,
        decimal SalesTotal, decimal RefundsTotal, int SalesCount,
        decimal CostOfGoodsSold,
        decimal PurchasesTotal, int PurchasesCount,
        int MermaCount, decimal MermaValue,
        decimal ExpensesTotal,
        decimal Profit);

    public record MonthlyDashboardDto(
        int Year,
        IReadOnlyList<MonthlyDashboardRowDto> Months,
        decimal YearSalesTotal, decimal YearCostOfGoodsSold, decimal YearPurchasesTotal, decimal YearExpensesTotal,
        decimal YearMermaValue, decimal YearProfit,
        decimal? PreviousYearProfit,
        decimal? ProfitChangePercent);
}
