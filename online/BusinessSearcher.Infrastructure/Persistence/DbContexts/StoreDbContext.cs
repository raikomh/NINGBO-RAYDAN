using BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.DbContexts
{
    public class StoreDbContext : DbContext
    {
        private readonly string _schema;

        public DbSet<Store>               Stores              => Set<Store>();
        public DbSet<StoreSchedule>       StoreSchedules      => Set<StoreSchedule>();

        public StoreDbContext(DbContextOptions<StoreDbContext> options, string schema)
            : base(options)
        {
            _schema = schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (Database.IsRelational())
                modelBuilder.HasDefaultSchema(_schema);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(StoreDbContext).Assembly,
                t => t.Namespace != null && t.Namespace.Contains("StoreManagement"));

            base.OnModelCreating(modelBuilder);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified))
            {
                if (entry.Entity is Domain.Common.Entity)
                    entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
