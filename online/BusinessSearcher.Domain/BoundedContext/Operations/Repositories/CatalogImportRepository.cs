using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Repositories
{
    public interface ICatalogImportRepository
    {
        /// <summary>Catálogo (por fecha) ya subido a un almacén, o null si nunca se subió.</summary>
        Task<CatalogImport?> GetByWarehouseAndDateAsync(Guid tenantId, Guid warehouseId, DateOnly catalogDate,
            CancellationToken ct = default);
        Task AddAsync(CatalogImport catalogImport, CancellationToken ct = default);
    }
}
