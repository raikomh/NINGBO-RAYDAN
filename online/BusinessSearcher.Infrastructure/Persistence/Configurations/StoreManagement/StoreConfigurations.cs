using BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.StoreManagement
{
    public class StoreConfiguration : IEntityTypeConfiguration<Store>
    {
        public void Configure(EntityTypeBuilder<Store> builder)
        {
            builder.ToTable("stores");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
            builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(500);
            builder.Property(s => s.LogoUrl).HasColumnName("logo_url").HasColumnType("text");
            builder.Property(s => s.IsActive).HasColumnName("is_active").IsRequired();
            builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            // Address como owned entity
            builder.OwnsOne(s => s.Address, addr =>
            {
                addr.Property(a => a.Street).HasColumnName("address_street").HasMaxLength(300).IsRequired();
                addr.Property(a => a.City).HasColumnName("address_city").HasMaxLength(100).IsRequired();
                addr.Property(a => a.State).HasColumnName("address_state").HasMaxLength(100).IsRequired();
                addr.Property(a => a.Country).HasColumnName("address_country").HasMaxLength(100).IsRequired();
                addr.Property(a => a.Latitude).HasColumnName("latitude");
                addr.Property(a => a.Longitude).HasColumnName("longitude");
            });

            // PhoneNumber como owned entity
            builder.OwnsOne(s => s.Phone, phone =>
            {
                phone.Property(p => p.Value).HasColumnName("phone").HasMaxLength(30).IsRequired();
            });

            // Relaciones con colecciones
            builder.HasMany(s => s.Schedules)
                .WithOne().HasForeignKey(sc => sc.StoreId).OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => s.TenantId);
            builder.HasIndex(s => s.IsActive);

            builder.Ignore(s => s.DomainEvents);
        }
    }

    public class StoreScheduleConfiguration : IEntityTypeConfiguration<StoreSchedule>
    {
        public void Configure(EntityTypeBuilder<StoreSchedule> builder)
        {
            builder.ToTable("store_schedules");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(s => s.StoreId).HasColumnName("store_id").IsRequired();
            builder.Property(s => s.DayOfWeek).HasColumnName("day_of_week")
                .HasConversion<string>().HasMaxLength(15).IsRequired();
            builder.Property(s => s.IsClosed).HasColumnName("is_closed").IsRequired();
            builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            builder.OwnsOne(s => s.Time, t =>
            {
                t.Property(x => x.OpenTime).HasColumnName("open_time").IsRequired();
                t.Property(x => x.CloseTime).HasColumnName("close_time").IsRequired();
            });

            builder.HasIndex(s => new { s.StoreId, s.DayOfWeek }).IsUnique();
        }
    }
}
