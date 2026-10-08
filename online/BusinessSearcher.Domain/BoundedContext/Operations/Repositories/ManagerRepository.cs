using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Repositories
{
    public interface IManagerRepository
    {
        Task<Manager?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<Manager?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default);
        Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? exceptId = null, CancellationToken ct = default);
        Task<IReadOnlyList<Manager>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task AddAsync(Manager manager, CancellationToken ct = default);
        Task UpdateAsync(Manager manager, CancellationToken ct = default);
    }
}
