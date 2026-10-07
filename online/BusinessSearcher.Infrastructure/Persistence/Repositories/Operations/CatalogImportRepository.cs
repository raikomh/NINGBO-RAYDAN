using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.Operations
{
    public class CatalogImportRepository : ICatalogImportRepository
    {
        private readonly OperationsDbContext _db;
        public CatalogImportRepository(OperationsDbContext db) => _db = db;

        public Task<CatalogImport?> GetByWarehouseAndDateAsync(Guid tenantId, Guid warehouseId, DateOnly catalogDate,
            CancellationToken ct = default)
            => _db.CatalogImports.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.WarehouseId == warehouseId
                && c.CatalogDate == catalogDate, ct);

        public async Task AddAsync(CatalogImport catalogImport, CancellationToken ct = default)
            => await _db.CatalogImports.AddAsync(catalogImport, ct);
    }
}
