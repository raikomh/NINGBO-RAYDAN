using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Repositories
{
    public interface ICategoryRepository
    {
        Task<Category?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Category>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task AddAsync(Category category, CancellationToken ct = default);
        Task UpdateAsync(Category category, CancellationToken ct = default);
    }

    public interface IProductRepository
    {
        /// <summary>Trae el producto con sus existencias por almacén (Include Stocks).</summary>
        Task<Product?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        /// <summary>Solo productos activos (uso en POS y búsqueda).</summary>
        Task<Product?> GetByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct = default);
        /// <summary>Activos e inactivos: sirve para no crear duplicados del mismo código de barras.</summary>
        Task<Product?> GetByBarcodeAnyStateAsync(Guid tenantId, string barcode, CancellationToken ct = default);
        Task<IReadOnlyList<Product>> GetByTenantAsync(Guid tenantId, string? search, Guid? categoryId,
            Guid? warehouseId, bool? lowStockOnly, CancellationToken ct = default);
        Task AddAsync(Product product, CancellationToken ct = default);
        Task UpdateAsync(Product product, CancellationToken ct = default);
    }

    public interface IProductPriceHistoryRepository
    {
        Task<IReadOnlyList<ProductPriceHistory>> GetByProductAsync(Guid tenantId, Guid productId, CancellationToken ct = default);
        /// <summary>Cambios de precio recientes de cualquier producto del negocio (para el feed de notificaciones).</summary>
        Task<IReadOnlyList<ProductPriceHistory>> GetRecentAsync(Guid tenantId, DateTime since, CancellationToken ct = default);
        Task AddAsync(ProductPriceHistory history, CancellationToken ct = default);
    }
}
