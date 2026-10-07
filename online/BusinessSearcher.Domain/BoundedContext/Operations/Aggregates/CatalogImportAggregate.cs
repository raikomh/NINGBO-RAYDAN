using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Operations.Aggregates
{
    /// <summary>
    /// Registro de un catálogo subido a un almacén (punto de venta). El catálogo se identifica por su fecha:
    /// el mismo catálogo (misma fecha) no puede subirse dos veces al mismo almacén, pero sí a otro almacén
    /// o con otra fecha.
    /// </summary>
    public class CatalogImport : Entity, IAggregateRoot
    {
        public Guid     TenantId     { get; private set; }
        public Guid     WarehouseId  { get; private set; }
        public DateOnly CatalogDate  { get; private set; }
        public string   FileName     { get; private set; } = string.Empty;
        public int      ProductCount { get; private set; }
        public Guid?    ImportedBy   { get; private set; }

        private CatalogImport() { }
        private CatalogImport(Guid id) : base(id) { }

        public static CatalogImport Create(Guid tenantId, Guid warehouseId, DateOnly catalogDate,
            string fileName, int productCount, Guid? importedBy)
        {
            if (tenantId == Guid.Empty) throw new DomainException("El catálogo debe pertenecer a un negocio.");
            if (warehouseId == Guid.Empty) throw new DomainException("Indica el almacén de destino.");
            if (productCount < 0) throw new DomainException("La cantidad de productos no puede ser negativa.");
            return new CatalogImport(Guid.NewGuid())
            {
                TenantId = tenantId, WarehouseId = warehouseId, CatalogDate = catalogDate,
                FileName = fileName.Trim(), ProductCount = productCount, ImportedBy = importedBy
            };
        }
    }
}
