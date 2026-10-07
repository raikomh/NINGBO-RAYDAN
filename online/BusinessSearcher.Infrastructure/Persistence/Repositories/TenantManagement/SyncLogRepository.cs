using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.TenantManagement
{
    public class SyncLogRepository : ISyncLogRepository
    {
        private readonly TenantManagementDbContext _context;

        public SyncLogRepository(TenantManagementDbContext context) => _context = context;

        public async Task AddAsync(SyncLog log, CancellationToken cancellationToken = default)
            => await _context.SyncLogs.AddAsync(log, cancellationToken);

        public async Task<IReadOnlyList<SyncLog>> GetRecentByTenantAsync(
            Guid tenantId, int take = 20, CancellationToken cancellationToken = default)
            => await _context.SyncLogs
                .Where(s => s.TenantId == tenantId)
                .OrderByDescending(s => s.StartedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
    }
}
