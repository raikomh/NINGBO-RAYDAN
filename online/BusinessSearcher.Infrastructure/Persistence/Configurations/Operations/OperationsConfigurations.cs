using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.Operations
{
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> b)
        {
            b.ToTable("op_suppliers");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Contact).HasMaxLength(150);
            b.Property(x => x.Phone).HasMaxLength(45);
            b.Property(x => x.Email).HasMaxLength(254);
            b.Property(x => x.Address).HasMaxLength(400);
            b.Property(x => x.Category).HasMaxLength(120);
            b.HasIndex(x => x.TenantId);
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
    {
        public void Configure(EntityTypeBuilder<Warehouse> b)
        {
            b.ToTable("op_warehouses");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Location).HasMaxLength(300);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
    {
        public void Configure(EntityTypeBuilder<Expense> b)
        {
            b.ToTable("op_expenses");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.AmountUSD).HasColumnType("decimal(18,2)");
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.Date);
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class ExchangeRateLogConfiguration : IEntityTypeConfiguration<ExchangeRateLog>
    {
        public void Configure(EntityTypeBuilder<ExchangeRateLog> b)
        {
            b.ToTable("op_exchange_rates");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Rate).HasColumnType("decimal(18,4)");
            b.HasIndex(x => new { x.TenantId, x.Date });
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class RoleSalaryConfigConfiguration : IEntityTypeConfiguration<RoleSalaryConfig>
    {
        public void Configure(EntityTypeBuilder<RoleSalaryConfig> b)
        {
            b.ToTable("op_role_salary_configs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.BaseSalary).HasColumnType("decimal(18,2)");
            b.Property(x => x.SalesPercentage).HasColumnType("decimal(5,2)");
            b.HasIndex(x => new { x.TenantId, x.Role }).IsUnique();
            b.Ignore(x => x.DomainEvents);
        }
    }
}
