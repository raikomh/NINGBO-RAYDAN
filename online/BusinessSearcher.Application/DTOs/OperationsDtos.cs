namespace BusinessSearcher.Application.DTOs.Operations
{
    // ── Proveedores ──
    public record SupplierDto(
        Guid Id, string Name, string? Contact, string Phone, string? Email,
        string? Address, double? Latitude, double? Longitude, string? Category);

    public record CreateSupplierDto(
        string Name, string Phone, string? Email = null, string? Contact = null,
        string? Address = null, string? Category = null, double? Latitude = null, double? Longitude = null);

    // ── Almacenes ──
    public record WarehouseDto(Guid Id, string Name, string? Location, string? Description);
    public record CreateWarehouseDto(string Name, string? Location = null, string? Description = null);

    // ── Gastos ──
    public record ExpenseDto(
        Guid Id, string Type, decimal Amount, decimal? AmountUSD, string Description, DateTime Date);

    public record CreateExpenseDto(
        string Type, decimal Amount, string Description, decimal? AmountUSD = null, Guid? RegisterId = null);

    // ── Tasa de cambio ──
    public record ExchangeRateDto(Guid Id, decimal Rate, DateTime Date);
    public record SetExchangeRateDto(decimal Rate);

    // ── Roles y salarios ──
    public record RoleSalaryConfigDto(string Role, decimal BaseSalary, decimal SalesPercentage, bool IsConfigured);
    public record SaveRoleSalaryConfigDto(decimal BaseSalary, decimal SalesPercentage);

    // ── Nómina (gasto de salario) ──
    public record PayrollWorkerLineDto(
        Guid WorkerId, string WorkerName, string Role, decimal BaseSalary, decimal MinimoExento,
        decimal SalesPercentage, decimal Ventas, decimal Comision, decimal SalarioACobrar);

    public record PayrollPreviewDto(DateTime From, DateTime To, decimal MinimoExento, IReadOnlyList<PayrollWorkerLineDto> Workers, decimal Total);

    public record RegisterPayrollExpenseDto(
        DateTime From, DateTime To, IReadOnlyList<Guid>? WorkerIds = null,
        string? Description = null, decimal? AmountUSD = null);
}
