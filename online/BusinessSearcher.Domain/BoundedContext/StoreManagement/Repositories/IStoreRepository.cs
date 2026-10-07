using BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Entities;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;

namespace BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories
{
    public interface IStoreRepository
    {
        Task<Store?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Store?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Store?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> TenantHasStoreAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task AddAsync(Store store, CancellationToken cancellationToken = default);
        Task UpdateAsync(Store store, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }

    public interface ISearchRepository
    {
        Task<IEnumerable<ProductSearchResult>> SearchProductsAsync(
            string? query,
            string? city,
            Guid?   categoryId,
            bool?   onlyAvailable,
            decimal? minPrice,
            decimal? maxPrice,
            Guid?   storeId = null,
            int page     = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);

        Task<int> CountSearchResultsAsync(
            string? query,
            string? city,
            Guid?   categoryId,
            bool?   onlyAvailable,
            decimal? minPrice,
            decimal? maxPrice,
            Guid?   storeId = null,
            CancellationToken cancellationToken = default);

        // Directorio de mayoristas (visible solo para tenants Minorista autenticados)
        Task<IEnumerable<ProductSearchResult>> GetWholesaleCatalogAsync(
            string? query,
            string? city,
            int page     = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);

        Task<int> CountWholesaleCatalogAsync(
            string? query,
            string? city,
            CancellationToken cancellationToken = default);
    }

    // Proyección plana para resultados de búsqueda cross-tenant
    public sealed class ProductSearchResult
    {
        public Guid     ProductId          { get; init; }
        public string   ProductName        { get; init; } = default!;
        public string?  ProductDescription { get; init; }
        public decimal  Price              { get; init; }
        public string   Currency           { get; init; } = default!;
        public string?  ImageUrl           { get; init; }
        public bool     IsAvailable        { get; init; }
        public int      Stock              { get; init; }
        public string?  CategoryName       { get; init; }
        public Guid     StoreId            { get; init; }
        public string   StoreName          { get; init; } = default!;
        public string   StoreAddress       { get; init; } = default!;
        public string   City               { get; init; } = default!;
        public double?  Latitude           { get; init; }
        public double?  Longitude          { get; init; }
        public string?  StorePhone         { get; init; }
        public bool     IsStoreOpen        { get; init; }
        public string?  StoreLogoUrl       { get; init; }
        public Guid     TenantId           { get; init; }
        public int      MinOrderQuantity   { get; init; } = 1;
        /// <summary>Tipo del negocio dueño (Wholesale/Retail) para colorear el pin en el mapa.</summary>
        public TenantType? TenantType      { get; init; }
    }
}
