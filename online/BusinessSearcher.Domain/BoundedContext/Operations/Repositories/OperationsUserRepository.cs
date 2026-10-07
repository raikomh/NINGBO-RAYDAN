using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Repositories
{
    public interface IOperationsUserRepository
    {
        Task<OperationsUser?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        /// <summary>Busca por email en todos los negocios (para el login del sub-usuario).</summary>
        Task<OperationsUser?> GetByEmailAsync(string email, CancellationToken ct = default);
        Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
        Task<IReadOnlyList<OperationsUser>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task AddAsync(OperationsUser user, CancellationToken ct = default);
        Task UpdateAsync(OperationsUser user, CancellationToken ct = default);
    }
}
