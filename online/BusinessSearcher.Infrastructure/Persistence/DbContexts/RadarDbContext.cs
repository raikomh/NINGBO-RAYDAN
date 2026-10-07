using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.DbContexts
{
    /// <summary>
    /// Contexto del radar colaborativo (schema public): clientes consumidores y los
    /// reportes/alertas de disponibilidad. Es global/cross-tenant por diseño.
    /// </summary>
    public class RadarDbContext : DbContext
    {
        public DbSet<Client>              Clients              => Set<Client>();
        public DbSet<ClientProduct>       ClientProducts       => Set<ClientProduct>();
        public DbSet<AvailabilityReport>  AvailabilityReports  => Set<AvailabilityReport>();
        public DbSet<ReportConfirmation>  ReportConfirmations  => Set<ReportConfirmation>();
        public DbSet<AvailabilityAlert>   AvailabilityAlerts   => Set<AvailabilityAlert>();
        public DbSet<CommunityQuestion>   CommunityQuestions   => Set<CommunityQuestion>();
        public DbSet<CommunityAnswer>     CommunityAnswers     => Set<CommunityAnswer>();
        public DbSet<Referral>            Referrals            => Set<Referral>();
        public DbSet<ReferralMonthlySettlement> ReferralMonthlySettlements => Set<ReferralMonthlySettlement>();
        public DbSet<ClientPremiumClaim>  ClientPremiumClaims  => Set<ClientPremiumClaim>();
        public DbSet<MipymeReferral>      MipymeReferrals      => Set<MipymeReferral>();
        public DbSet<MipymeReferralMonthlySettlement> MipymeReferralMonthlySettlements => Set<MipymeReferralMonthlySettlement>();

        public RadarDbContext(DbContextOptions<RadarDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (Database.IsRelational())
                modelBuilder.HasDefaultSchema("public");

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(RadarDbContext).Assembly,
                t => t.Namespace != null && t.Namespace.Contains("Configurations.Radar"));

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
