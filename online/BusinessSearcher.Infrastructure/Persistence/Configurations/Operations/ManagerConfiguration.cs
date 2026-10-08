using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.Operations
{
    public class ManagerConfiguration : IEntityTypeConfiguration<Manager>
    {
        public void Configure(EntityTypeBuilder<Manager> b)
        {
            b.ToTable("op_managers");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).HasMaxLength(30).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.IdNumber).HasMaxLength(20).IsRequired();
            b.Property(x => x.Municipality).HasMaxLength(100).IsRequired();
            b.Property(x => x.Province).HasMaxLength(100).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            b.Ignore(x => x.DomainEvents);
        }
    }
}
