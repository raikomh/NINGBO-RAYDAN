using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Repositories
{
    public interface IBusinessInfoRepository
    {
        Task<BusinessInfo?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task AddAsync(BusinessInfo info, CancellationToken ct = default);
        Task UpdateAsync(BusinessInfo info, CancellationToken ct = default);
    }

    public interface IAuditLogRepository
    {
        Task<IReadOnlyList<AuditLog>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to,
            Guid? userId, string? action, int limit, CancellationToken ct = default);
        Task AddAsync(AuditLog log, CancellationToken ct = default);
    }

    public interface ISettingRepository
    {
        Task<IReadOnlyList<Setting>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task<Setting?> GetByKeyAsync(Guid tenantId, string key, CancellationToken ct = default);
        Task AddAsync(Setting setting, CancellationToken ct = default);
        Task UpdateAsync(Setting setting, CancellationToken ct = default);
    }
}
