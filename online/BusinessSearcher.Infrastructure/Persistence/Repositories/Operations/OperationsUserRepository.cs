using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.Operations
{
    public class OperationsUserRepository : IOperationsUserRepository
    {
        private readonly OperationsDbContext _db;
        public OperationsUserRepository(OperationsDbContext db) => _db = db;

        public Task<OperationsUser?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.OperationsUsers.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == id, ct);

        public Task<OperationsUser?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            var e = email.Trim().ToLowerInvariant();
            return _db.OperationsUsers.FirstOrDefaultAsync(u => u.Email == e && u.IsActive, ct);
        }

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        {
            var e = email.Trim().ToLowerInvariant();
            return _db.OperationsUsers.AnyAsync(u => u.Email == e, ct);
        }

        public async Task<IReadOnlyList<OperationsUser>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => await _db.OperationsUsers.Where(u => u.TenantId == tenantId).OrderBy(u => u.Name).ToListAsync(ct);

        public async Task AddAsync(OperationsUser user, CancellationToken ct = default) => await _db.OperationsUsers.AddAsync(user, ct);
        public Task UpdateAsync(OperationsUser user, CancellationToken ct = default) { _db.OperationsUsers.Update(user); return Task.CompletedTask; }
    }
}
