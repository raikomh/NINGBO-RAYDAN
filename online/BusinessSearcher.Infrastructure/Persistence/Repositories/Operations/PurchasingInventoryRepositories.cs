using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.Operations
{
    public class PurchaseRequestRepository : IPurchaseRequestRepository
    {
        private readonly OperationsDbContext _db;
        public PurchaseRequestRepository(OperationsDbContext db) => _db = db;

        public Task<PurchaseRequest?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.PurchaseRequests.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

        public async Task<IReadOnlyList<PurchaseRequest>> GetByTenantAsync(Guid tenantId, PurchaseRequestStatus? status, CancellationToken ct = default)
        {
            var q = _db.PurchaseRequests.Where(x => x.TenantId == tenantId);
            if (status.HasValue) q = q.Where(x => x.Status == status.Value);
            return await q.OrderByDescending(x => x.Date).ToListAsync(ct);
        }

        public Task<bool> HasPendingForProductAsync(Guid tenantId, Guid productId, Guid warehouseId, CancellationToken ct = default)
            => _db.PurchaseRequests.AnyAsync(x => x.TenantId == tenantId && x.ProductId == productId
                && x.WarehouseId == warehouseId && x.Status == PurchaseRequestStatus.Pending, ct);

        public async Task AddAsync(PurchaseRequest request, CancellationToken ct = default) => await _db.PurchaseRequests.AddAsync(request, ct);
        public Task UpdateAsync(PurchaseRequest request, CancellationToken ct = default) { _db.PurchaseRequests.Update(request); return Task.CompletedTask; }
    }

    public class PurchaseRepository : IPurchaseRepository
    {
        private readonly OperationsDbContext _db;
        public PurchaseRepository(OperationsDbContext db) => _db = db;

        public Task<Purchase?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.Purchases.Include(p => p.Items).FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

        public async Task<IReadOnlyList<Purchase>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to, Guid? supplierId, CancellationToken ct = default)
        {
            var q = _db.Purchases.Include(p => p.Items).Where(p => p.TenantId == tenantId);
            if (from.HasValue)       q = q.Where(p => p.Date >= from.Value.ToUtc());
            if (to.HasValue)         q = q.Where(p => p.Date <= to.Value.ToUtc());
            if (supplierId.HasValue) q = q.Where(p => p.SupplierId == supplierId.Value);
            return await q.OrderByDescending(p => p.Date).ToListAsync(ct);
        }

        public async Task AddAsync(Purchase purchase, CancellationToken ct = default) => await _db.Purchases.AddAsync(purchase, ct);
    }

    public class InventoryMovementRepository : IInventoryMovementRepository
    {
        private readonly OperationsDbContext _db;
        public InventoryMovementRepository(OperationsDbContext db) => _db = db;

        public async Task<IReadOnlyList<InventoryMovement>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to,
            Guid? productId, Guid? warehouseId, CancellationToken ct = default)
        {
            var q = _db.InventoryMovements.Where(m => m.TenantId == tenantId);
            if (from.HasValue)       q = q.Where(m => m.Date >= from.Value.ToUtc());
            if (to.HasValue)         q = q.Where(m => m.Date <= to.Value.ToUtc());
            if (productId.HasValue)  q = q.Where(m => m.ProductId == productId.Value);
            if (warehouseId.HasValue) q = q.Where(m => m.FromWarehouseId == warehouseId.Value || m.ToWarehouseId == warehouseId.Value);
            return await q.OrderByDescending(m => m.Date).ToListAsync(ct);
        }

        public async Task AddAsync(InventoryMovement movement, CancellationToken ct = default) => await _db.InventoryMovements.AddAsync(movement, ct);
    }

    public class InventoryCountRepository : IInventoryCountRepository
    {
        private readonly OperationsDbContext _db;
        public InventoryCountRepository(OperationsDbContext db) => _db = db;

        public Task<InventoryCount?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.InventoryCounts.Include(c => c.Items).FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

        public async Task<IReadOnlyList<InventoryCount>> GetByTenantAsync(Guid tenantId, Guid? warehouseId, CancellationToken ct = default)
        {
            var q = _db.InventoryCounts.Include(c => c.Items).Where(c => c.TenantId == tenantId);
            if (warehouseId.HasValue) q = q.Where(c => c.WarehouseId == warehouseId.Value);
            return await q.OrderByDescending(c => c.Date).ToListAsync(ct);
        }

        public async Task AddAsync(InventoryCount count, CancellationToken ct = default) => await _db.InventoryCounts.AddAsync(count, ct);
        public Task UpdateAsync(InventoryCount count, CancellationToken ct = default) { _db.InventoryCounts.Update(count); return Task.CompletedTask; }
    }
}
