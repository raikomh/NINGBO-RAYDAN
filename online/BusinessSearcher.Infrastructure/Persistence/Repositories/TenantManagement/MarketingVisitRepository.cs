using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.TenantManagement
{
    public class MarketingVisitRepository : IMarketingVisitRepository
    {
        private readonly TenantManagementDbContext _context;

        public MarketingVisitRepository(TenantManagementDbContext context) => _context = context;

        public async Task AddAsync(MarketingVisit visit, CancellationToken cancellationToken = default)
            => await _context.MarketingVisits.AddAsync(visit, cancellationToken);

        public async Task<IReadOnlyList<MarketingVisitSummary>> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            var grouped = await _context.MarketingVisits
                .GroupBy(v => v.Source)
                .Select(g => new { Source = g.Key, Count = g.Count(), Last = g.Max(v => v.CreatedAt) })
                .OrderByDescending(g => g.Count)
                .ToListAsync(cancellationToken);

            return grouped.Select(g => new MarketingVisitSummary(g.Source, g.Count, g.Last)).ToList();
        }
    }
}
