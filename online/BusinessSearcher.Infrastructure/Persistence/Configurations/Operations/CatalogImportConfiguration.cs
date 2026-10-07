using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.Operations
{
    public class CatalogImportConfiguration : IEntityTypeConfiguration<CatalogImport>
    {
        public void Configure(EntityTypeBuilder<CatalogImport> b)
        {
            b.ToTable("op_catalog_imports");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.FileName).HasMaxLength(255);
            b.Property(x => x.CatalogDate).HasColumnType("date");
            // Un mismo catálogo (fecha) solo puede subirse una vez a cada almacén.
            b.HasIndex(x => new { x.TenantId, x.WarehouseId, x.CatalogDate }).IsUnique();
            b.Ignore(x => x.DomainEvents);
        }
    }
}
