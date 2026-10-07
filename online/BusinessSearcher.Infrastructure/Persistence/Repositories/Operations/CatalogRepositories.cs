using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.Operations
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly OperationsDbContext _db;
        public CategoryRepository(OperationsDbContext db) => _db = db;

        public Task<Category?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

        public async Task<IReadOnlyList<Category>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => await _db.Categories.Where(c => c.TenantId == tenantId && c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);

        public async Task AddAsync(Category category, CancellationToken ct = default) => await _db.Categories.AddAsync(category, ct);
        public Task UpdateAsync(Category category, CancellationToken ct = default) { _db.Categories.Update(category); return Task.CompletedTask; }
    }

    public class ProductRepository : IProductRepository
    {
        private readonly OperationsDbContext _db;
        public ProductRepository(OperationsDbContext db) => _db = db;

        public Task<Product?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => _db.Products.Include(p => p.Stocks).FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

        public Task<Product?> GetByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct = default)
            => _db.Products.Include(p => p.Stocks)
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IsActive && p.Barcode == barcode, ct);

        public Task<Product?> GetByBarcodeAnyStateAsync(Guid tenantId, string barcode, CancellationToken ct = default)
            => _db.Products.Include(p => p.Stocks)
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Barcode == barcode, ct);

        public async Task<IReadOnlyList<Product>> GetByTenantAsync(Guid tenantId, string? search, Guid? categoryId,
            Guid? warehouseId, bool? lowStockOnly, CancellationToken ct = default)
        {
            var q = _db.Products.Include(p => p.Stocks).Where(p => p.TenantId == tenantId && p.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(p => p.Name.ToLower().Contains(s) || (p.Barcode != null && p.Barcode.ToLower().Contains(s)));
            }
            if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId.Value);

            var list = await q.OrderBy(p => p.Name).ToListAsync(ct);

            if (warehouseId.HasValue)
                list = list.Where(p => p.Stocks.Any(st => st.WarehouseId == warehouseId.Value)).ToList();
            if (lowStockOnly == true)
                list = list.Where(p => p.TotalStock <= p.MinStock).ToList();
            return list;
        }

        public async Task AddAsync(Product product, CancellationToken ct = default) => await _db.Products.AddAsync(product, ct);
        public Task UpdateAsync(Product product, CancellationToken ct = default) { _db.Products.Update(product); return Task.CompletedTask; }
    }

    public class ProductPriceHistoryRepository : IProductPriceHistoryRepository
    {
        private readonly OperationsDbContext _db;
        public ProductPriceHistoryRepository(OperationsDbContext db) => _db = db;

        public async Task<IReadOnlyList<ProductPriceHistory>> GetByProductAsync(Guid tenantId, Guid productId, CancellationToken ct = default)
            => await _db.ProductPriceHistory
                .Where(h => h.TenantId == tenantId && h.ProductId == productId)
                .OrderByDescending(h => h.ChangeDate).ToListAsync(ct);

        public async Task<IReadOnlyList<ProductPriceHistory>> GetRecentAsync(Guid tenantId, DateTime since, CancellationToken ct = default)
            => await _db.ProductPriceHistory
                .Where(h => h.TenantId == tenantId && h.ChangeDate >= since)
                .OrderByDescending(h => h.ChangeDate).ToListAsync(ct);

        public async Task AddAsync(ProductPriceHistory history, CancellationToken ct = default) => await _db.ProductPriceHistory.AddAsync(history, ct);
    }
}
