using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories
{
    public interface ITenantRepository
    {
        Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Tenant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<Tenant?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task<Tenant?> GetByPasswordResetTokenAsync(string resetToken, CancellationToken cancellationToken = default);
        Task<Tenant?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default);
        Task<Tenant?> GetBySyncApiKeyHashAsync(string hash, CancellationToken cancellationToken = default);
        Task<IEnumerable<Tenant>> GetActiveTenantAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Tenant>> GetAllAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);

        // Admin queries
        Task<(IEnumerable<Tenant> Items, int Total)> GetAllWithFiltersAsync(
            int page, int pageSize,
            string? status, string? search, bool? isApproved = null,
            CancellationToken cancellationToken = default);
        Task<AdminGlobalStats> GetGlobalStatsAsync(CancellationToken cancellationToken = default);

        Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
        Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
        Task<bool> ExistsEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<int> CountAsync(CancellationToken cancellationToken = default);
    }

    public record AdminGlobalStats(
        int TotalTenants,
        int TrialTenants,
        int ActiveTenants,
        int SuspendedTenants,
        int InactiveTenants,
        int NewLast30Days,
        decimal TotalRevenue);

    public interface IMarketingVisitRepository
    {
        Task AddAsync(Aggregates.MarketingVisit visit, CancellationToken cancellationToken = default);

        /// <summary>Resumen de visitas agrupadas por origen (cantidad + última visita).</summary>
        Task<IReadOnlyList<MarketingVisitSummary>> GetSummaryAsync(CancellationToken cancellationToken = default);
    }

    public record MarketingVisitSummary(string Source, int VisitCount, DateTime LastVisitAt);

    public interface ISyncLogRepository
    {
        Task AddAsync(Aggregates.SyncLog log, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Aggregates.SyncLog>> GetRecentByTenantAsync(
            Guid tenantId, int take = 20, CancellationToken cancellationToken = default);
    }
}
