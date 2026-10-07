using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Repositories
{
    public interface ISupplierRepository
    {
        Task<Supplier?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Supplier>> GetByTenantAsync(Guid tenantId, string? search, CancellationToken ct = default);
        Task AddAsync(Supplier supplier, CancellationToken ct = default);
        Task UpdateAsync(Supplier supplier, CancellationToken ct = default);
    }

    public interface IWarehouseRepository
    {
        Task<Warehouse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Warehouse>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task AddAsync(Warehouse warehouse, CancellationToken ct = default);
        Task UpdateAsync(Warehouse warehouse, CancellationToken ct = default);
    }

    public interface IExpenseRepository
    {
        Task<IReadOnlyList<Expense>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to, CancellationToken ct = default);
        Task AddAsync(Expense expense, CancellationToken ct = default);
    }

    public interface IExchangeRateRepository
    {
        Task<ExchangeRateLog?> GetLatestAsync(Guid tenantId, CancellationToken ct = default);
        Task<IReadOnlyList<ExchangeRateLog>> GetHistoryAsync(Guid tenantId, int limit, CancellationToken ct = default);
        Task AddAsync(ExchangeRateLog log, CancellationToken ct = default);
    }

    public interface IRoleSalaryConfigRepository
    {
        Task<IReadOnlyList<RoleSalaryConfig>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task<RoleSalaryConfig?> GetByRoleAsync(Guid tenantId, BusinessSearcher.Domain.BoundedContext.Operations.Enums.OperationsRole role, CancellationToken ct = default);
        Task AddAsync(RoleSalaryConfig config, CancellationToken ct = default);
        Task UpdateAsync(RoleSalaryConfig config, CancellationToken ct = default);
        Task DeleteAsync(RoleSalaryConfig config, CancellationToken ct = default);
    }
}
