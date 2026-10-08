using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.Operations
{
    public class SaleConfiguration : IEntityTypeConfiguration<Sale>
    {
        public void Configure(EntityTypeBuilder<Sale> b)
        {
            b.ToTable("op_sales");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
            b.Property(x => x.Discount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Total).HasColumnType("decimal(18,2)");
            b.Property(x => x.SubtotalUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.TotalUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.TaxAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.ExchangeRate).HasColumnType("decimal(18,4)");
            b.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.PaymentCurrency).HasConversion<string>().HasMaxLength(3);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
            b.Property(x => x.WarehouseName).HasMaxLength(150);
            b.Property(x => x.ManagerCode).HasMaxLength(30);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Date });
            b.HasIndex(x => x.RegisterId);
            b.Ignore(x => x.DomainEvents);

            b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasMany(x => x.Payments).WithOne().HasForeignKey(p => p.SaleId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
    {
        public void Configure(EntityTypeBuilder<SaleItem> b)
        {
            b.ToTable("op_sale_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.DiscountValue).HasColumnType("decimal(18,2)");
            b.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(15);
            b.HasIndex(x => x.ProductId);
            b.Ignore(x => x.LineDiscount);
            b.Ignore(x => x.LineTotal);
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
    {
        public void Configure(EntityTypeBuilder<SalePayment> b)
        {
            b.ToTable("op_sale_payments");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3);
            b.Property(x => x.ChangeCurrency).HasConversion<string>().HasMaxLength(3);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.AmountUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.CashTendered).HasColumnType("decimal(18,2)");
            b.Property(x => x.Change).HasColumnType("decimal(18,2)");
            b.Property(x => x.TransactionId).HasMaxLength(100);
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
    {
        public void Configure(EntityTypeBuilder<CashRegister> b)
        {
            b.ToTable("op_cash_registers");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            foreach (var col in new[] { "InitialAmount", "ExpectedAmount", "ActualAmount", "Difference",
                                        "InitialAmountUSD", "ExpectedAmountUSD", "ActualAmountUSD", "DifferenceUSD",
                                        "TotalSales", "TotalExpenses", "TotalCashIn", "TotalCashOut" })
                b.Property(col).HasColumnType("decimal(18,2)");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
    {
        public void Configure(EntityTypeBuilder<CashMovement> b)
        {
            b.ToTable("op_cash_movements");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(15);
            b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.AmountUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => new { x.TenantId, x.RegisterId });
            b.Ignore(x => x.DomainEvents);
        }
    }
}
