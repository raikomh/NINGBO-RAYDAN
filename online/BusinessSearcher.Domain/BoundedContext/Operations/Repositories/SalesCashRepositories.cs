using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Repositories
{
    public interface ISaleRepository
    {
        Task<Sale?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Sale>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to,
            Guid? registerId, Guid? cashierId, CancellationToken ct = default);
        Task AddAsync(Sale sale, CancellationToken ct = default);
        Task UpdateAsync(Sale sale, CancellationToken ct = default);
    }

    public interface ITerminalRepository
    {
        Task<Terminal?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Terminal>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task AddAsync(Terminal terminal, CancellationToken ct = default);
        Task UpdateAsync(Terminal terminal, CancellationToken ct = default);
    }

    public interface ICashRegisterRepository
    {
        Task<CashRegister?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<CashRegister?> GetOpenForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
        /// <summary>Cajas abiertas de un almacén (para vincularlas con el cierre de un conteo físico).</summary>
        Task<IReadOnlyList<CashRegister>> GetOpenByWarehouseAsync(Guid tenantId, Guid warehouseId, CancellationToken ct = default);
        Task<IReadOnlyList<CashRegister>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to, CancellationToken ct = default);
        Task AddAsync(CashRegister register, CancellationToken ct = default);
        Task UpdateAsync(CashRegister register, CancellationToken ct = default);
    }

    public interface ICashMovementRepository
    {
        Task<IReadOnlyList<CashMovement>> GetByRegisterAsync(Guid tenantId, Guid registerId, CancellationToken ct = default);
        Task AddAsync(CashMovement movement, CancellationToken ct = default);
    }
}
