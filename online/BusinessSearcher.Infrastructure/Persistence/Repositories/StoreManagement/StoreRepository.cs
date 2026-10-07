using BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.StoreManagement
{
    // ── StoreRepository ───────────────────────────────────────────────────────────
    public class StoreRepository : IStoreRepository
    {
        private readonly StoreDbContext _context;

        public StoreRepository(StoreDbContext context)
            => _context = context;

        public async Task<Store?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.Stores
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        public async Task<Store?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.Stores
                .Include(s => s.Schedules)
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        public async Task<Store?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => await _context.Stores
                .Include(s => s.Schedules)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.Stores.AnyAsync(s => s.Id == id, cancellationToken);

        public async Task<bool> TenantHasStoreAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => await _context.Stores.AnyAsync(s => s.TenantId == tenantId, cancellationToken);

        public async Task AddAsync(Store store, CancellationToken cancellationToken = default)
            => await _context.Stores.AddAsync(store, cancellationToken);

        public Task UpdateAsync(Store store, CancellationToken cancellationToken = default)
        {
            _context.Stores.Update(store);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var store = await _context.Stores.FindAsync(new object[] { id }, cancellationToken);
            if (store is not null)
                _context.Stores.Remove(store);
        }
    }

    // ── SearchRepository ───────────────────────────────────────────────────────────
    // El catálogo (Operations.Product) vive en el schema "public", filtrado por
    // TenantId: una sola consulta indexada, sin necesidad de recorrer schemas.
    // Los datos de presentación de la tienda (nombre/dirección/teléfono/horario)
    // siguen viviendo en el schema por-tenant (StoreManagement), así que solo se
    // consultan para los tenants que efectivamente aparecen en la página de
    // resultados (a lo sumo pageSize, no todos los tenants activos).
    public class SearchRepository : ISearchRepository
    {
        private readonly string _connectionString;
        private readonly TenantManagementDbContext _tenantCtx;
        private readonly DbContexts.OperationsDbContext _opsDb;

        public SearchRepository(
            string connectionString, TenantManagementDbContext tenantCtx, DbContexts.OperationsDbContext opsDb)
        {
            _connectionString = connectionString;
            _tenantCtx        = tenantCtx;
            _opsDb            = opsDb;
        }

        public async Task<IEnumerable<ProductSearchResult>> SearchProductsAsync(
            string? query, string? city, Guid? categoryId,
            bool? onlyAvailable, decimal? minPrice, decimal? maxPrice,
            Guid? storeId = null, int page = 1, int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var tenantIds = await ResolveTenantIdsAsync(city, storeId, null, cancellationToken);
            if (tenantIds.Count == 0) return Enumerable.Empty<ProductSearchResult>();

            var typeMap = await GetTenantTypeMapAsync(tenantIds, cancellationToken);

            var products = await BuildProductQuery(tenantIds, query, categoryId, onlyAvailable, minPrice, maxPrice)
                .OrderBy(x => x.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var storeInfos = await FetchStoreInfosAsync(
                products.Select(p => p.TenantId).Distinct().ToList(), cancellationToken);

            return products.Select(p => MapToResult(p, storeInfos.GetValueOrDefault(p.TenantId), typeMap.GetValueOrDefault(p.TenantId)));
        }

        public async Task<int> CountSearchResultsAsync(
            string? query, string? city, Guid? categoryId,
            bool? onlyAvailable, decimal? minPrice, decimal? maxPrice,
            Guid? storeId = null,
            CancellationToken cancellationToken = default)
        {
            var tenantIds = await ResolveTenantIdsAsync(city, storeId, null, cancellationToken);
            if (tenantIds.Count == 0) return 0;

            return await BuildProductQuery(tenantIds, query, categoryId, onlyAvailable, minPrice, maxPrice)
                .CountAsync(cancellationToken);
        }

        // ── Directorio de mayoristas (minoristas autenticados) ────────────────────

        public async Task<IEnumerable<ProductSearchResult>> GetWholesaleCatalogAsync(
            string? query, string? city, int page = 1, int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var tenantIds = await ResolveTenantIdsAsync(city, null, TenantType.Wholesale, cancellationToken);
            if (tenantIds.Count == 0) return Enumerable.Empty<ProductSearchResult>();

            var typeMap = tenantIds.ToDictionary(id => id, _ => (TenantType?)TenantType.Wholesale);

            var products = await BuildProductQuery(tenantIds, query, null, true, null, null)
                .OrderBy(x => x.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var storeInfos = await FetchStoreInfosAsync(
                products.Select(p => p.TenantId).Distinct().ToList(), cancellationToken);

            return products.Select(p => MapToResult(p, storeInfos.GetValueOrDefault(p.TenantId), typeMap.GetValueOrDefault(p.TenantId)));
        }

        public async Task<int> CountWholesaleCatalogAsync(
            string? query, string? city, CancellationToken cancellationToken = default)
        {
            var tenantIds = await ResolveTenantIdsAsync(city, null, TenantType.Wholesale, cancellationToken);
            if (tenantIds.Count == 0) return 0;

            return await BuildProductQuery(tenantIds, query, null, true, null, null)
                .CountAsync(cancellationToken);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private sealed class ProductProjection
        {
            public Guid    Id           { get; set; }
            public Guid    TenantId     { get; set; }
            public string  Name         { get; set; } = default!;
            public string? Description  { get; set; }
            public decimal SellPrice    { get; set; }
            public string? ImageUrl     { get; set; }
            public int     TotalStock   { get; set; }
            public string? CategoryName { get; set; }
            public int     MinOrderQuantity { get; set; }
        }

        /// <summary>Tenants activos (opcionalmente filtrados por ciudad/tipo/storeId=tenantId).</summary>
        private async Task<List<Guid>> ResolveTenantIdsAsync(
            string? city, Guid? storeId, TenantType? type, CancellationToken ct)
        {
            var q = _tenantCtx.Tenants
                .Where(t => t.Status == TenantStatus.Active || t.Status == TenantStatus.Trial);

            if (type.HasValue)
                q = q.Where(t => t.Type == type.Value);

            // Cada tenant tiene exactamente una tienda: storeId se trata como TenantId.
            if (storeId.HasValue)
                q = q.Where(t => t.Id == storeId.Value);

            var tenantIds = await q.Select(t => t.Id).ToListAsync(ct);

            if (!string.IsNullOrWhiteSpace(city))
                tenantIds = await FilterTenantsByCityAsync(tenantIds, city, ct);

            return tenantIds;
        }

        private async Task<Dictionary<Guid, TenantType?>> GetTenantTypeMapAsync(List<Guid> tenantIds, CancellationToken ct)
            => await _tenantCtx.Tenants
                .Where(t => tenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => (TenantType?)t.Type, ct);

        private IQueryable<ProductProjection> BuildProductQuery(
            List<Guid> tenantIds, string? query, Guid? categoryId,
            bool? onlyAvailable, decimal? minPrice, decimal? maxPrice)
        {
            var products = _opsDb.Products
                .Where(p => p.IsActive && p.ForSale && p.IsPubliclyVisible && tenantIds.Contains(p.TenantId));

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                products = products.Where(p =>
                    EF.Functions.ILike(p.Name, $"%{q}%") ||
                    (p.Description != null && EF.Functions.ILike(p.Description, $"%{q}%")));
            }

            if (categoryId.HasValue)
                products = products.Where(p => p.CategoryId == categoryId.Value);

            if (minPrice.HasValue)
                products = products.Where(p => p.SellPrice >= minPrice.Value);

            if (maxPrice.HasValue)
                products = products.Where(p => p.SellPrice <= maxPrice.Value);

            if (onlyAvailable.HasValue)
                products = onlyAvailable.Value
                    ? products.Where(p => p.Stocks.Sum(s => s.Quantity) > 0)
                    : products.Where(p => p.Stocks.Sum(s => s.Quantity) <= 0);

            return from p in products
                   join c in _opsDb.Categories on p.CategoryId equals c.Id into cats
                   from c in cats.DefaultIfEmpty()
                   select new ProductProjection
                   {
                       Id               = p.Id,
                       TenantId         = p.TenantId,
                       Name             = p.Name,
                       Description      = p.Description,
                       SellPrice        = p.SellPrice,
                       ImageUrl         = p.ImageUrl,
                       TotalStock       = p.Stocks.Sum(s => s.Quantity),
                       CategoryName     = c == null ? null : c.Name,
                       MinOrderQuantity = p.MinOrderQuantity
                   };
        }

        /// <summary>De la lista de tenants candidatos, cuáles tienen tienda activa en la ciudad dada.</summary>
        private async Task<List<Guid>> FilterTenantsByCityAsync(List<Guid> tenantIds, string city, CancellationToken ct)
        {
            if (tenantIds.Count == 0) return tenantIds;

            var matches = new List<Guid>();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            foreach (var tenantId in tenantIds)
            {
                var schema = $"tenant_{tenantId:N}";
                await using var cmd = new NpgsqlCommand(
                    $@"SELECT 1 FROM ""{schema}"".stores WHERE is_active = true AND address_city ILIKE '%' || @city || '%' LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@city", city);
                try
                {
                    var result = await cmd.ExecuteScalarAsync(ct);
                    if (result is not null) matches.Add(tenantId);
                }
                catch { /* Schema puede no existir aún */ }
            }

            return matches;
        }

        /// <summary>Datos de presentación de la tienda (nombre/dirección/teléfono/horario), solo para los
        /// tenants pedidos (típicamente los de la página de resultados actual, no todos los activos).</summary>
        private async Task<Dictionary<Guid, StoreInfo>> FetchStoreInfosAsync(List<Guid> tenantIds, CancellationToken ct)
        {
            var result = new Dictionary<Guid, StoreInfo>();
            if (tenantIds.Count == 0) return result;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            foreach (var tenantId in tenantIds)
            {
                var schema = $"tenant_{tenantId:N}";
                await using var cmd = new NpgsqlCommand($@"
                    SELECT
                        s.id AS store_id, s.name, s.address_street || ', ' || s.address_city AS address,
                        s.address_city AS city, s.latitude, s.longitude, s.phone, s.logo_url,
                        CASE WHEN EXISTS (
                            SELECT 1 FROM ""{schema}"".store_schedules ss
                            WHERE ss.store_id = s.id
                              AND ss.day_of_week = TO_CHAR(NOW() AT TIME ZONE 'UTC', 'FMDay')
                              AND ss.is_closed   = false
                              AND ss.open_time  <= CURRENT_TIME
                              AND ss.close_time >= CURRENT_TIME
                        ) THEN true ELSE false END AS is_open
                    FROM ""{schema}"".stores s
                    WHERE s.tenant_id = @tenantId AND s.is_active = true
                    LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@tenantId", tenantId);

                try
                {
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        result[tenantId] = new StoreInfo(
                            r.GetGuid(r.GetOrdinal("store_id")),
                            r.GetString(r.GetOrdinal("name")),
                            r.GetString(r.GetOrdinal("address")),
                            r.GetString(r.GetOrdinal("city")),
                            r.IsDBNull(r.GetOrdinal("latitude"))  ? null : r.GetDouble(r.GetOrdinal("latitude")),
                            r.IsDBNull(r.GetOrdinal("longitude")) ? null : r.GetDouble(r.GetOrdinal("longitude")),
                            r.IsDBNull(r.GetOrdinal("phone"))     ? null : r.GetString(r.GetOrdinal("phone")),
                            r.IsDBNull(r.GetOrdinal("logo_url"))  ? null : r.GetString(r.GetOrdinal("logo_url")),
                            r.GetBoolean(r.GetOrdinal("is_open")));
                    }
                }
                catch { /* Schema puede no existir aún, o el tenant no tiene tienda activa */ }
            }

            return result;
        }

        private sealed record StoreInfo(
            Guid Id, string Name, string Address, string City,
            double? Latitude, double? Longitude, string? Phone, string? LogoUrl, bool IsOpen);

        private static ProductSearchResult MapToResult(ProductProjection p, StoreInfo? store, TenantType? tenantType)
            => new()
            {
                ProductId          = p.Id,
                ProductName        = p.Name,
                ProductDescription = p.Description,
                Price              = p.SellPrice,
                Currency           = "CUP",
                ImageUrl           = p.ImageUrl,
                IsAvailable        = p.TotalStock > 0,
                Stock              = p.TotalStock,
                CategoryName       = p.CategoryName,
                // Cada tenant tiene exactamente una tienda: StoreId = TenantId a efectos de búsqueda.
                StoreId            = p.TenantId,
                StoreName          = store?.Name ?? "",
                StoreAddress       = store?.Address ?? "",
                City               = store?.City ?? "",
                Latitude           = store?.Latitude,
                Longitude          = store?.Longitude,
                StorePhone         = store?.Phone,
                StoreLogoUrl       = store?.LogoUrl,
                IsStoreOpen        = store?.IsOpen ?? false,
                TenantId           = p.TenantId,
                MinOrderQuantity   = p.MinOrderQuantity,
                TenantType         = tenantType
            };
    }
}
