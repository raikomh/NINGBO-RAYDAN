using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.Operations
{
    public class OperationsUserConfiguration : IEntityTypeConfiguration<OperationsUser>
    {
        public void Configure(EntityTypeBuilder<OperationsUser> b)
        {
            b.ToTable("op_users");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Email).HasMaxLength(254).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.AvatarUrl).HasMaxLength(500);
            b.HasIndex(x => x.Email).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.Ignore(x => x.DomainEvents);
        }
    }
}
