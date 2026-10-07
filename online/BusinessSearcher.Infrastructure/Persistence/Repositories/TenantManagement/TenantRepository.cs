using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.TenantManagement
{
    public class TenantRepository : ITenantRepository
    {
        private readonly TenantManagementDbContext _context;

        public TenantRepository(TenantManagementDbContext context)
            => _context = context;

        public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.Tenants
                .Include(t => t.Payments)
                .Include(t => t.PaymentClaims)
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        public async Task<Tenant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var normalized = email.ToLowerInvariant().Trim();
            return await _context.Tenants
                .Include(t => t.Payments)
                .FirstOrDefaultAsync(t => t.Email.Value == normalized, cancellationToken);
        }

        public async Task<Tenant?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
            => await _context.Tenants
                .Include(t => t.RefreshTokens)
                .FirstOrDefaultAsync(
                    t => t.RefreshTokens.Any(rt => rt.Token == refreshToken),
                    cancellationToken);

        public async Task<Tenant?> GetByPasswordResetTokenAsync(string resetToken, CancellationToken cancellationToken = default)
            => await _context.Tenants
                .Include(t => t.PasswordResetTokens)
                .FirstOrDefaultAsync(
                    t => t.PasswordResetTokens.Any(rt => rt.Token == resetToken),
                    cancellationToken);

        public async Task<Tenant?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default)
            => await _context.Tenants
                .Include(t => t.EmailVerificationTokens)
                .FirstOrDefaultAsync(
                    t => t.EmailVerificationTokens.Any(evt => evt.Token == token),
                    cancellationToken);

        public async Task<Tenant?> GetBySyncApiKeyHashAsync(string hash, CancellationToken cancellationToken = default)
            => await _context.Tenants.FirstOrDefaultAsync(t => t.SyncApiKeyHash == hash, cancellationToken);

        public async Task<IEnumerable<Tenant>> GetActiveTenantAsync(CancellationToken cancellationToken = default)
            => await _context.Tenants
                .Where(t => t.Status == TenantStatus.Active || t.Status == TenantStatus.Trial)
                .OrderBy(t => t.BusinessName)
                .ToListAsync(cancellationToken);

        public async Task<IEnumerable<Tenant>> GetAllAsync(
            int skip = 0, int take = 50, CancellationToken cancellationToken = default)
            => await _context.Tenants
                .OrderByDescending(t => t.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

        public async Task<(IEnumerable<Tenant> Items, int Total)> GetAllWithFiltersAsync(
            int page, int pageSize, string? status, string? search, bool? isApproved = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Tenants.Include(t => t.Payments).Include(t => t.PaymentClaims).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TenantStatus>(status, true, out var s))
                query = query.Where(t => t.Status == s);

            if (isApproved.HasValue)
                query = query.Where(t => t.IsApproved == isApproved.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLowerInvariant();
                query = query.Where(t =>
                    t.BusinessName.ToLower().Contains(term) ||
                    t.Email.Value.Contains(term));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<AdminGlobalStats> GetGlobalStatsAsync(CancellationToken cancellationToken = default)
        {
            var tenants      = await _context.Tenants.Include(t => t.Payments).ToListAsync(cancellationToken);
            var cutoff       = DateTime.UtcNow.AddDays(-30);
            var totalRevenue = tenants.Sum(t => t.GetTotalPaid());

            return new AdminGlobalStats(
                TotalTenants:    tenants.Count,
                TrialTenants:    tenants.Count(t => t.Status == TenantStatus.Trial),
                ActiveTenants:   tenants.Count(t => t.Status == TenantStatus.Active),
                SuspendedTenants:tenants.Count(t => t.Status == TenantStatus.Suspended),
                InactiveTenants: tenants.Count(t => t.Status == TenantStatus.Inactive),
                NewLast30Days:   tenants.Count(t => t.CreatedAt >= cutoff),
                TotalRevenue:    totalRevenue);
        }

        public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
            => await _context.Tenants.AddAsync(tenant, cancellationToken);

        public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            _context.Tenants.Update(tenant);
            return Task.CompletedTask;
        }

        public async Task<bool> ExistsEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var normalized = email.ToLowerInvariant().Trim();
            return await _context.Tenants.AnyAsync(
                t => t.Email.Value == normalized, cancellationToken);
        }

        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
            => await _context.Tenants.CountAsync(cancellationToken);
    }
}
