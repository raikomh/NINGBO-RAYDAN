using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Repositories
{
    public interface IPurchaseRequestRepository
    {
        Task<PurchaseRequest?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<PurchaseRequest>> GetByTenantAsync(Guid tenantId, PurchaseRequestStatus? status, CancellationToken ct = default);
        Task<bool> HasPendingForProductAsync(Guid tenantId, Guid productId, Guid warehouseId, CancellationToken ct = default);
        Task AddAsync(PurchaseRequest request, CancellationToken ct = default);
        Task UpdateAsync(PurchaseRequest request, CancellationToken ct = default);
    }

    public interface IPurchaseRepository
    {
        Task<Purchase?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Purchase>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to, Guid? supplierId, CancellationToken ct = default);
        Task AddAsync(Purchase purchase, CancellationToken ct = default);
    }

    public interface IInventoryMovementRepository
    {
        Task<IReadOnlyList<InventoryMovement>> GetByTenantAsync(Guid tenantId, DateTime? from, DateTime? to,
            Guid? productId, Guid? warehouseId, CancellationToken ct = default);
        Task AddAsync(InventoryMovement movement, CancellationToken ct = default);
    }

    public interface IInventoryCountRepository
    {
        Task<InventoryCount?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<InventoryCount>> GetByTenantAsync(Guid tenantId, Guid? warehouseId, CancellationToken ct = default);
        Task AddAsync(InventoryCount count, CancellationToken ct = default);
        Task UpdateAsync(InventoryCount count, CancellationToken ct = default);
    }
}
