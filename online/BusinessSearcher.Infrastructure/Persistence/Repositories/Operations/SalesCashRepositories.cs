using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.Operations
{
    /// <summary>Los filtros de fecha llegan del query string como DateTime "Unspecified" (p.ej. "2026-08-11");
    /// Npgsql exige Kind=Utc para comparar contra columnas timestamptz. Aquí solo se fuerza el Kind: la
    /// expansión de "hasta" al final del día (para que un rango de un solo día incluya todo ese día) se
    /// hace en cada query handler, no aquí, porque algunos llamadores (p.ej. nómina) ya pasan un instante
    /// preciso con su propio ajuste de zona horaria y no debe volver a extenderse.</summary>
    internal static class DateRangeUtcExtensions
    {
        public static DateTime ToUtc(this DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public class SaleRepository : ISaleRepository
    {
        private readonly OperationsDbContext _db;
        public SaleRepository(OperationsDbContext db) => _db = db;

        public Task<Sale?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.Sales.Include(s => s.Items).Include(s => s.Payments)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);

        public async Task<IReadOnlyList<Sale>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to,
            Guid? registerId, Guid? cashierId, Guid? warehouseId = null, CancellationToken ct = default)
        {
            var q = _db.Sales.Include(s => s.Items).Include(s => s.Payments).Where(s => s.TenantId == tenantId);
            if (from.HasValue)       q = q.Where(s => s.Date >= from.Value.ToUtc());
            if (to.HasValue)         q = q.Where(s => s.Date <= to.Value.ToUtc());
            if (registerId.HasValue) q = q.Where(s => s.RegisterId == registerId.Value);
            if (cashierId.HasValue)  q = q.Where(s => s.CashierId == cashierId.Value);
            if (warehouseId.HasValue) q = q.Where(s => s.Items.Any(i => i.WarehouseId == warehouseId.Value));
            return await q.OrderByDescending(s => s.Date).ToListAsync(ct);
        }

        public async Task AddAsync(Sale sale, CancellationToken ct = default) => await _db.Sales.AddAsync(sale, ct);
        public Task UpdateAsync(Sale sale, CancellationToken ct = default) { _db.Sales.Update(sale); return Task.CompletedTask; }
    }

    public class CashRegisterRepository : ICashRegisterRepository
    {
        private readonly OperationsDbContext _db;
        public CashRegisterRepository(OperationsDbContext db) => _db = db;

        public Task<CashRegister?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.CashRegisters.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

        public Task<CashRegister?> GetOpenForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => _db.CashRegisters
                .Where(c => c.TenantId == tenantId && c.Status == CashRegisterStatus.Open && c.OpenedBy == userId)
                .OrderByDescending(c => c.OpenDate).FirstOrDefaultAsync(ct);

        public async Task<IReadOnlyList<CashRegister>> GetOpenByWarehouseAsync(Guid tenantId, Guid warehouseId, CancellationToken ct = default)
            => await _db.CashRegisters
                .Where(c => c.TenantId == tenantId && c.Status == CashRegisterStatus.Open && c.WarehouseId == warehouseId)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<CashRegister>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var q = _db.CashRegisters.Where(c => c.TenantId == tenantId);
            if (from.HasValue) q = q.Where(c => c.OpenDate >= from.Value.ToUtc());
            if (to.HasValue)   q = q.Where(c => c.OpenDate <= to.Value.ToUtc());
            return await q.OrderByDescending(c => c.OpenDate).ToListAsync(ct);
        }

        public async Task AddAsync(CashRegister register, CancellationToken ct = default) => await _db.CashRegisters.AddAsync(register, ct);
        public Task UpdateAsync(CashRegister register, CancellationToken ct = default) { _db.CashRegisters.Update(register); return Task.CompletedTask; }
    }

    public class CashMovementRepository : ICashMovementRepository
    {
        private readonly OperationsDbContext _db;
        public CashMovementRepository(OperationsDbContext db) => _db = db;

        public async Task<IReadOnlyList<CashMovement>> GetByRegisterAsync(Guid tenantId, Guid registerId, CancellationToken ct = default)
            => await _db.CashMovements.Where(m => m.TenantId == tenantId && m.RegisterId == registerId)
                .OrderByDescending(m => m.Date).ToListAsync(ct);

        public async Task AddAsync(CashMovement movement, CancellationToken ct = default) => await _db.CashMovements.AddAsync(movement, ct);
    }
}
