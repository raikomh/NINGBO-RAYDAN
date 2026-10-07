using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.DbContexts
{
    public class TenantManagementDbContext : DbContext
    {
        public DbSet<Tenant>                 Tenants                  => Set<Tenant>();
        public DbSet<TenantPayment>          TenantPayments           => Set<TenantPayment>();
        public DbSet<PaymentClaim>           PaymentClaims            => Set<PaymentClaim>();
        public DbSet<ChatMessage>            ChatMessages             => Set<ChatMessage>();
        public DbSet<EmailVerificationToken> EmailVerificationTokens  => Set<EmailVerificationToken>();
        public DbSet<MarketingVisit>         MarketingVisits          => Set<MarketingVisit>();
        public DbSet<SyncLog>                SyncLogs                 => Set<SyncLog>();

        public TenantManagementDbContext(DbContextOptions<TenantManagementDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (Database.IsRelational())
                modelBuilder.HasDefaultSchema("public");

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(TenantManagementDbContext).Assembly,
                t => t.Namespace != null && t.Namespace.Contains("TenantManagement"));

            base.OnModelCreating(modelBuilder);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Auditoría automática: UpdatedAt
            foreach (var entry in ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified))
            {
                if (entry.Entity is Domain.Common.Entity entity)
                    entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
