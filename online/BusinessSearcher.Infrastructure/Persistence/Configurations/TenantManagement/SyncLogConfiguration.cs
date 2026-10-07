using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.TenantManagement
{
    public class SyncLogConfiguration : IEntityTypeConfiguration<SyncLog>
    {
        public void Configure(EntityTypeBuilder<SyncLog> builder)
        {
            builder.ToTable("sync_logs", "public");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
            builder.Property(s => s.Direction).HasColumnName("direction").HasConversion<string>().HasMaxLength(10).IsRequired();
            builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(s => s.StartedAt).HasColumnName("started_at").IsRequired();
            builder.Property(s => s.CompletedAt).HasColumnName("completed_at");
            builder.Property(s => s.ItemsSent).HasColumnName("items_sent").IsRequired();
            builder.Property(s => s.ItemsAccepted).HasColumnName("items_accepted").IsRequired();
            builder.Property(s => s.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            builder.Property(s => s.CounterpartyUrl).HasColumnName("counterparty_url").HasMaxLength(500);
            builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(s => s.TenantId);
            builder.HasIndex(s => new { s.TenantId, s.StartedAt });

            builder.Ignore(s => s.DomainEvents);
        }
    }
}
