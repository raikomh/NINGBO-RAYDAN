using System.Text.Json;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.Operations
{
    public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
    {
        public void Configure(EntityTypeBuilder<PurchaseRequest> b)
        {
            b.ToTable("op_purchase_requests");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
            b.Property(x => x.Notes).HasMaxLength(500);
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
    {
        public void Configure(EntityTypeBuilder<Purchase> b)
        {
            b.ToTable("op_purchases");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            foreach (var col in new[] { "Total", "TotalUSD", "AssociatedExpenses", "AssociatedExpensesUSD" })
                b.Property(col).HasColumnType("decimal(18,2)");
            b.Property(x => x.ExchangeRate).HasColumnType("decimal(18,4)");
            b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
            b.Property(x => x.InvoiceUrl).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Date });
            b.Ignore(x => x.DomainEvents);

            b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

            // Lista de IDs de solicitudes asociadas → jsonb (conversor explícito + comparer).
            var guidListConverter = new ValueConverter<List<Guid>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());
            var guidListComparer = new ValueComparer<List<Guid>>(
                (a, c) => (a ?? new List<Guid>()).SequenceEqual(c ?? new List<Guid>()),
                v => v.Aggregate(0, (h, id) => HashCode.Combine(h, id.GetHashCode())),
                v => v.ToList());
            b.Property<List<Guid>>("_purchaseRequestIds")
                .HasColumnName("purchase_request_ids")
                .HasColumnType("jsonb")
                .HasConversion(guidListConverter, guidListComparer);
            b.Ignore(x => x.PurchaseRequestIds);
        }
    }

    public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
    {
        public void Configure(EntityTypeBuilder<PurchaseItem> b)
        {
            b.ToTable("op_purchase_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            b.Property(x => x.CostPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.BatchNumber).HasMaxLength(80);
            b.HasIndex(x => x.ProductId);
            b.Ignore(x => x.LineTotal);
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
    {
        public void Configure(EntityTypeBuilder<InventoryMovement> b)
        {
            b.ToTable("op_inventory_movements");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(15);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.HasIndex(x => new { x.TenantId, x.Date });
            b.HasIndex(x => x.ProductId);
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class InventoryCountConfiguration : IEntityTypeConfiguration<InventoryCount>
    {
        public void Configure(EntityTypeBuilder<InventoryCount> b)
        {
            b.ToTable("op_inventory_counts");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
            b.HasIndex(x => new { x.TenantId, x.WarehouseId });
            b.Ignore(x => x.DomainEvents);
            b.Ignore(x => x.Discrepancies);

            b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.InventoryCountId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class InventoryCountItemConfiguration : IEntityTypeConfiguration<InventoryCountItem>
    {
        public void Configure(EntityTypeBuilder<InventoryCountItem> b)
        {
            b.ToTable("op_inventory_count_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            b.HasIndex(x => x.ProductId);
            b.Ignore(x => x.Difference);
            b.Ignore(x => x.DomainEvents);
        }
    }
}
