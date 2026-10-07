using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.Operations
{
    public class BusinessInfoConfiguration : IEntityTypeConfiguration<BusinessInfo>
    {
        public void Configure(EntityTypeBuilder<BusinessInfo> b)
        {
            b.ToTable("op_business_info");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Address).HasMaxLength(400);
            b.Property(x => x.Phone).HasMaxLength(45);
            b.Property(x => x.Email).HasMaxLength(254);
            b.Property(x => x.TaxId).HasMaxLength(80);
            b.Property(x => x.LogoUrl).HasMaxLength(500);
            b.HasIndex(x => x.TenantId).IsUnique();
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> b)
        {
            b.ToTable("op_audit_logs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.UserName).HasMaxLength(150);
            b.Property(x => x.UserRole).HasMaxLength(30);
            b.Property(x => x.Action).HasMaxLength(100).IsRequired();
            b.Property(x => x.TargetEntity).HasMaxLength(100);
            b.Property(x => x.Details).HasMaxLength(1000);
            b.Property(x => x.IpAddress).HasMaxLength(45);
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.Method).HasMaxLength(10);
            b.Property(x => x.Path).HasMaxLength(300);
            b.HasIndex(x => new { x.TenantId, x.Timestamp });
            b.HasIndex(x => x.UserId);
            b.Ignore(x => x.DomainEvents);
        }
    }

    public class SettingConfiguration : IEntityTypeConfiguration<Setting>
    {
        public void Configure(EntityTypeBuilder<Setting> b)
        {
            b.ToTable("op_settings");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Key).HasMaxLength(100).IsRequired();
            b.Property(x => x.Value).HasMaxLength(2000).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
            b.Ignore(x => x.DomainEvents);
        }
    }
}
