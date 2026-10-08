using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.Operations
{
    public class ManagerRepository : IManagerRepository
    {
        private readonly OperationsDbContext _db;
        public ManagerRepository(OperationsDbContext db) => _db = db;

        public Task<Manager?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.Managers.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == id, ct);

        public Task<Manager?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default)
        {
            var c = code.Trim().ToUpperInvariant();
            return _db.Managers.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Code == c, ct);
        }

        public Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? exceptId = null, CancellationToken ct = default)
        {
            var c = code.Trim().ToUpperInvariant();
            return _db.Managers.AnyAsync(m => m.TenantId == tenantId && m.Code == c && (exceptId == null || m.Id != exceptId), ct);
        }

        public async Task<IReadOnlyList<Manager>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => await _db.Managers.Where(m => m.TenantId == tenantId).OrderBy(m => m.Code).ToListAsync(ct);

        public async Task AddAsync(Manager manager, CancellationToken ct = default) => await _db.Managers.AddAsync(manager, ct);
        public Task UpdateAsync(Manager manager, CancellationToken ct = default) { _db.Managers.Update(manager); return Task.CompletedTask; }
    }
}
