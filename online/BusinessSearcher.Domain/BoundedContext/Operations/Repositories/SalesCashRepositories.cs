using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Repositories
{
    public interface ISaleRepository
    {
        Task<Sale?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Sale>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to,
            Guid? registerId, Guid? cashierId, Guid? warehouseId = null, CancellationToken ct = default);
        Task AddAsync(Sale sale, CancellationToken ct = default);
        Task UpdateAsync(Sale sale, CancellationToken ct = default);
        /// <summary>Borra para siempre las ventas con algún renglón en ese almacén (y sus items/pagos, en
        /// cascada). Devuelve cuántas ventas borró. Operación destructiva — no hay forma de recuperarlas.</summary>
        Task<int> DeleteByWarehouseAsync(Guid tenantId, Guid warehouseId, CancellationToken ct = default);
        /// <summary>Borra para siempre las ventas de ese almacén que tengan algún renglón de SOLO esos
        /// productos (y sus items/pagos, en cascada). Si la venta tiene otros productos no incluidos en la
        /// lista, se borra igual entera (mismo criterio que DeleteByWarehouseAsync, acotado a estos productos).
        /// Devuelve cuántas ventas borró.</summary>
        Task<int> DeleteByWarehouseAndProductsAsync(Guid tenantId, Guid warehouseId, IReadOnlyList<Guid> productIds, CancellationToken ct = default);
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
