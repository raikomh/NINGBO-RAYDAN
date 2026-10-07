using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.Operations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> b)
        {
            b.ToTable("op_categories");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Code).HasMaxLength(50);
            b.HasIndex(x => x.TenantId);
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> b)
        {
            b.ToTable("op_products");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Barcode).HasMaxLength(80);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Unit).HasMaxLength(30).IsRequired();
            b.Property(x => x.CostPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.SellPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.CostPriceUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.SellPriceUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.TaxRate).HasColumnType("decimal(9,4)");
            b.Property(x => x.BatchNumber).HasMaxLength(80);
            b.Property(x => x.ImageUrl).HasMaxLength(500);
            b.Property(x => x.MinOrderQuantity).HasDefaultValue(1);
            b.HasIndex(x => x.TenantId);
            // Un código de barras identifica un solo producto activo por negocio: dos catálogos con el
            // mismo producto actualizan el stock, nunca crean otro registro. Los inactivos conservan su código.
            b.HasIndex(x => new { x.TenantId, x.Barcode }).IsUnique()
                .HasFilter("is_active AND barcode IS NOT NULL");
            b.HasIndex(x => new { x.IsPubliclyVisible, x.ForSale, x.IsActive });
            b.Ignore(x => x.DomainEvents);

            b.HasMany(x => x.Stocks)
                .WithOne()
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Stocks).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(x => x.TotalStock);
        }
    }

    public class ProductStockConfiguration : IEntityTypeConfiguration<ProductStock>
    {
        public void Configure(EntityTypeBuilder<ProductStock> b)
        {
            b.ToTable("op_product_stocks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.AverageCost).HasColumnType("decimal(18,2)");
            b.Property(x => x.AverageCostUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.SellPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.SellPriceUSD).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.ProductId, x.WarehouseId }).IsUnique();
            b.HasIndex(x => x.WarehouseId);
            b.Ignore(x => x.DomainEvents);
            // Token de concurrencia optimista (columna de sistema de Postgres, sin migración): evita que
            // dos operaciones concurrentes (dos ventas, o una venta y una compra) sobre el mismo renglón
            // de stock se pisen una a otra en silencio. Ver OpsMapper.SaveWithStockRetryAsync.
            b.UseXminAsConcurrencyToken();
        }
    }

    public class ProductPriceHistoryConfiguration : IEntityTypeConfiguration<ProductPriceHistory>
    {
        public void Configure(EntityTypeBuilder<ProductPriceHistory> b)
        {
            b.ToTable("op_product_price_history");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.OldCostPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.NewCostPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.OldSellPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.NewSellPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.OldCostPriceUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.NewCostPriceUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.OldSellPriceUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.NewSellPriceUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.Reason).HasMaxLength(500);
            b.HasIndex(x => new { x.TenantId, x.ProductId });
            b.Ignore(x => x.DomainEvents);
        }
    }
}
