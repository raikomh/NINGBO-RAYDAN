using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.Operations
{
    public class SupplierRepository : ISupplierRepository
    {
        private readonly OperationsDbContext _db;
        public SupplierRepository(OperationsDbContext db) => _db = db;

        public Task<Supplier?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.Suppliers.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);

        public async Task<IReadOnlyList<Supplier>> GetByTenantAsync(Guid tenantId, string? search, CancellationToken ct = default)
        {
            var q = _db.Suppliers.Where(s => s.TenantId == tenantId && s.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(x => x.Name.ToLower().Contains(s));
            }
            return await q.OrderBy(x => x.Name).ToListAsync(ct);
        }

        public async Task AddAsync(Supplier supplier, CancellationToken ct = default) => await _db.Suppliers.AddAsync(supplier, ct);
        public Task UpdateAsync(Supplier supplier, CancellationToken ct = default) { _db.Suppliers.Update(supplier); return Task.CompletedTask; }
    }

    public class WarehouseRepository : IWarehouseRepository
    {
        private readonly OperationsDbContext _db;
        public WarehouseRepository(OperationsDbContext db) => _db = db;

        public Task<Warehouse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.Warehouses.FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == id, ct);

        public async Task<IReadOnlyList<Warehouse>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => await _db.Warehouses.Where(w => w.TenantId == tenantId && w.IsActive).OrderBy(w => w.Name).ToListAsync(ct);

        public async Task AddAsync(Warehouse warehouse, CancellationToken ct = default) => await _db.Warehouses.AddAsync(warehouse, ct);
        public Task UpdateAsync(Warehouse warehouse, CancellationToken ct = default) { _db.Warehouses.Update(warehouse); return Task.CompletedTask; }
    }

    public class ExpenseRepository : IExpenseRepository
    {
        private readonly OperationsDbContext _db;
        public ExpenseRepository(OperationsDbContext db) => _db = db;

        public async Task<IReadOnlyList<Expense>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var q = _db.Expenses.Where(e => e.TenantId == tenantId);
            if (from.HasValue) q = q.Where(e => e.Date >= from.Value.ToUtc());
            if (to.HasValue)   q = q.Where(e => e.Date <= to.Value.ToUtc());
            return await q.OrderByDescending(e => e.Date).ToListAsync(ct);
        }

        public async Task AddAsync(Expense expense, CancellationToken ct = default) => await _db.Expenses.AddAsync(expense, ct);
    }

    public class ExchangeRateRepository : IExchangeRateRepository
    {
        private readonly OperationsDbContext _db;
        public ExchangeRateRepository(OperationsDbContext db) => _db = db;

        public Task<ExchangeRateLog?> GetLatestAsync(Guid tenantId, CancellationToken ct = default)
            => _db.ExchangeRateLogs.Where(r => r.TenantId == tenantId).OrderByDescending(r => r.Date).FirstOrDefaultAsync(ct);

        public async Task<IReadOnlyList<ExchangeRateLog>> GetHistoryAsync(Guid tenantId, int limit, CancellationToken ct = default)
            => await _db.ExchangeRateLogs.Where(r => r.TenantId == tenantId).OrderByDescending(r => r.Date).Take(limit).ToListAsync(ct);

        public async Task AddAsync(ExchangeRateLog log, CancellationToken ct = default) => await _db.ExchangeRateLogs.AddAsync(log, ct);
    }

    public class RoleSalaryConfigRepository : IRoleSalaryConfigRepository
    {
        private readonly OperationsDbContext _db;
        public RoleSalaryConfigRepository(OperationsDbContext db) => _db = db;

        public async Task<IReadOnlyList<RoleSalaryConfig>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => await _db.RoleSalaryConfigs.Where(c => c.TenantId == tenantId).ToListAsync(ct);

        public Task<RoleSalaryConfig?> GetByRoleAsync(Guid tenantId, Domain.BoundedContext.Operations.Enums.OperationsRole role, CancellationToken ct = default)
            => _db.RoleSalaryConfigs.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Role == role, ct);

        public async Task AddAsync(RoleSalaryConfig config, CancellationToken ct = default) => await _db.RoleSalaryConfigs.AddAsync(config, ct);
        public Task UpdateAsync(RoleSalaryConfig config, CancellationToken ct = default) { _db.RoleSalaryConfigs.Update(config); return Task.CompletedTask; }
        public Task DeleteAsync(RoleSalaryConfig config, CancellationToken ct = default) { _db.RoleSalaryConfigs.Remove(config); return Task.CompletedTask; }
    }
}
