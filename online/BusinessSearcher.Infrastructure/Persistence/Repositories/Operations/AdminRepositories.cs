using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.Operations
{
    public class BusinessInfoRepository : IBusinessInfoRepository
    {
        private readonly OperationsDbContext _db;
        public BusinessInfoRepository(OperationsDbContext db) => _db = db;

        public Task<BusinessInfo?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => _db.BusinessInfos.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        public async Task AddAsync(BusinessInfo info, CancellationToken ct = default) => await _db.BusinessInfos.AddAsync(info, ct);
        public Task UpdateAsync(BusinessInfo info, CancellationToken ct = default) { _db.BusinessInfos.Update(info); return Task.CompletedTask; }
    }

    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly OperationsDbContext _db;
        public AuditLogRepository(OperationsDbContext db) => _db = db;

        public async Task<IReadOnlyList<AuditLog>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to,
            Guid? userId, string? action, int limit, CancellationToken ct = default)
        {
            var q = _db.AuditLogs.Where(a => a.TenantId == tenantId);
            if (from.HasValue)   q = q.Where(a => a.Timestamp >= from.Value.ToUtc());
            if (to.HasValue)     q = q.Where(a => a.Timestamp <= to.Value.ToUtc());
            if (userId.HasValue) q = q.Where(a => a.UserId == userId.Value);
            if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action.Contains(action));
            return await q.OrderByDescending(a => a.Timestamp).Take(limit).ToListAsync(ct);
        }

        public async Task AddAsync(AuditLog log, CancellationToken ct = default) => await _db.AuditLogs.AddAsync(log, ct);
    }

    public class SettingRepository : ISettingRepository
    {
        private readonly OperationsDbContext _db;
        public SettingRepository(OperationsDbContext db) => _db = db;

        public async Task<IReadOnlyList<Setting>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => await _db.Settings.Where(s => s.TenantId == tenantId).OrderBy(s => s.Key).ToListAsync(ct);

        public Task<Setting?> GetByKeyAsync(Guid tenantId, string key, CancellationToken ct = default)
            => _db.Settings.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, ct);

        public async Task AddAsync(Setting setting, CancellationToken ct = default) => await _db.Settings.AddAsync(setting, ct);
        public Task UpdateAsync(Setting setting, CancellationToken ct = default) { _db.Settings.Update(setting); return Task.CompletedTask; }
    }
}
