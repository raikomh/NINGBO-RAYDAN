using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.Radar
{
    public class ClientRepository : IClientRepository
    {
        private readonly RadarDbContext _context;
        public ClientRepository(RadarDbContext context) => _context = context;

        public Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _context.Clients.Include(c => c.PremiumClaims).FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<Client?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            var normalized = email.ToLowerInvariant().Trim();
            return _context.Clients.FirstOrDefaultAsync(c => c.Email.Value == normalized, ct);
        }

        public Task<Client?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
            => _context.Clients
                .Include(c => c.RefreshTokens)
                .FirstOrDefaultAsync(c => c.RefreshTokens.Any(rt => rt.Token == refreshToken), ct);

        public Task<Client?> GetByPasswordResetTokenAsync(string resetToken, CancellationToken ct = default)
            => _context.Clients
                .Include(c => c.PasswordResetTokens)
                .FirstOrDefaultAsync(c => c.PasswordResetTokens.Any(t => t.Token == resetToken), ct);

        public Task<Client?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default)
            => _context.Clients
                .Include(c => c.EmailVerificationTokens)
                .FirstOrDefaultAsync(c => c.EmailVerificationTokens.Any(t => t.Token == token), ct);

        public Task<bool> ExistsEmailAsync(string email, CancellationToken ct = default)
        {
            var normalized = email.ToLowerInvariant().Trim();
            return _context.Clients.AnyAsync(c => c.Email.Value == normalized, ct);
        }

        public Task<Client?> GetByReferralCodeAsync(string referralCode, CancellationToken ct = default)
        {
            var code = referralCode.Trim().ToUpperInvariant();
            return _context.Clients.FirstOrDefaultAsync(c => c.ReferralCode == code, ct);
        }

        public Task<bool> ExistsReferralCodeAsync(string referralCode, CancellationToken ct = default)
        {
            var code = referralCode.Trim().ToUpperInvariant();
            return _context.Clients.AnyAsync(c => c.ReferralCode == code, ct);
        }

        public async Task AddAsync(Client client, CancellationToken ct = default)
            => await _context.Clients.AddAsync(client, ct);

        public Task UpdateAsync(Client client, CancellationToken ct = default)
        {
            _context.Clients.Update(client);
            return Task.CompletedTask;
        }

        public async Task<(IReadOnlyList<Client> Items, int Total)> GetAllWithFiltersAsync(
            int page, int pageSize, string? search, bool? isApproved, CancellationToken ct = default)
        {
            // Incluye ReferralCupTransactions: el panel de admin de pagos (GetReferralPayoutsAdminQuery)
            // necesita el desglose por tipo de movimiento (bono/premio de clientes y de MiPymes).
            var query = _context.Clients
                .Include(c => c.PremiumClaims)
                .Include(c => c.ReferralCupTransactions)
                .AsQueryable();

            if (isApproved.HasValue)
                query = query.Where(c => c.IsApproved == isApproved.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLowerInvariant();
                query = query.Where(c =>
                    c.FullName.ToLower().Contains(term) ||
                    c.Email.Value.Contains(term));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }
    }

    public class ClientProductRepository : IClientProductRepository
    {
        private readonly RadarDbContext _context;
        public ClientProductRepository(RadarDbContext context) => _context = context;

        public Task<ClientProduct?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _context.ClientProducts.FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<IReadOnlyList<ClientProduct>> GetByClientAsync(Guid clientId, CancellationToken ct = default)
            => await _context.ClientProducts
                .Where(p => p.ClientId == clientId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(ct);

        public async Task AddAsync(ClientProduct product, CancellationToken ct = default)
            => await _context.ClientProducts.AddAsync(product, ct);

        public Task UpdateAsync(ClientProduct product, CancellationToken ct = default)
        {
            _context.ClientProducts.Update(product);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(ClientProduct product, CancellationToken ct = default)
        {
            _context.ClientProducts.Remove(product);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<ClientProductSearchResult>> SearchAsync(
            string? query, string? city, bool? onlyAvailable, decimal? minPrice, decimal? maxPrice,
            int page, int pageSize, CancellationToken ct = default)
        {
            // Une producto ↔ cliente (mismo schema public) para traer los datos del vendedor.
            var q = from p in _context.ClientProducts
                    join c in _context.Clients on p.ClientId equals c.Id
                    select new { p, c };

            if (onlyAvailable.HasValue)
                q = q.Where(x => x.p.IsAvailable == onlyAvailable.Value);
            if (minPrice.HasValue)
                q = q.Where(x => x.p.Price >= minPrice.Value);
            if (maxPrice.HasValue)
                q = q.Where(x => x.p.Price <= maxPrice.Value);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = AvailabilityReport.Normalize(query);
                q = q.Where(x => x.p.NormalizedName.Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(city))
            {
                var cityTerm = city.ToLower();
                q = q.Where(x => x.c.City != null && x.c.City.ToLower().Contains(cityTerm));
            }

            var rows = await q
                .OrderByDescending(x => x.p.IsAvailable)
                .ThenByDescending(x => x.p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return rows.Select(x => new ClientProductSearchResult(
                x.p.Id, x.p.Name, x.p.Description, x.p.Price, x.p.Currency,
                x.p.IsAvailable, x.p.ImageUrl,
                x.c.Id, x.c.FullName, x.c.Latitude, x.c.Longitude, x.c.City,
                x.c.PhoneNumber, x.c.Street, x.c.State)).ToList();
        }

        public async Task<IReadOnlyList<ClientSellerLocation>> GetAvailableWithLocationAsync(CancellationToken ct = default)
        {
            // Clientes con ubicación y ≥1 producto disponible; agrupa por cliente para el mapa.
            var rows = await (from p in _context.ClientProducts
                              join c in _context.Clients on p.ClientId equals c.Id
                              where p.IsAvailable && c.Latitude != null && c.Longitude != null
                              group new { p, c } by new { c.Id, c.FullName, c.Latitude, c.Longitude, c.City } into g
                              select new ClientSellerLocation(
                                  g.Key.Id, g.Key.FullName, g.Key.Latitude!.Value, g.Key.Longitude!.Value,
                                  g.Key.City, g.Count()))
                             .ToListAsync(ct);
            return rows;
        }
    }

    public class ReferralRepository : IReferralRepository
    {
        private readonly RadarDbContext _context;
        public ReferralRepository(RadarDbContext context) => _context = context;

        public async Task AddAsync(Referral referral, CancellationToken ct = default)
            => await _context.Referrals.AddAsync(referral, ct);

        public Task<Referral?> GetPendingByInvitedAsync(Guid invitedClientId, CancellationToken ct = default)
            => _context.Referrals.FirstOrDefaultAsync(
                r => r.InvitedClientId == invitedClientId && r.Status == Domain.BoundedContext.Radar.Enums.ReferralStatus.Pending, ct);

        public Task<int> CountValidByInviterAsync(Guid inviterClientId, CancellationToken ct = default)
            => _context.Referrals.CountAsync(
                r => r.InviterClientId == inviterClientId && r.Status == Domain.BoundedContext.Radar.Enums.ReferralStatus.Valid, ct);

        public Task UpdateAsync(Referral referral, CancellationToken ct = default)
        {
            _context.Referrals.Update(referral);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<(Guid InviterClientId, int Count, DateTime LastValidatedAt)>> GetValidCountsByInviterForMonthAsync(
            int year, int month, CancellationToken ct = default)
        {
            var rows = await _context.Referrals
                .Where(r => r.Status == Domain.BoundedContext.Radar.Enums.ReferralStatus.Valid
                    && r.ValidatedAt != null
                    && r.ValidatedAt.Value.Year == year
                    && r.ValidatedAt.Value.Month == month)
                .GroupBy(r => r.InviterClientId)
                .Select(g => new { InviterClientId = g.Key, Count = g.Count(), LastValidatedAt = g.Max(r => r.ValidatedAt!.Value) })
                .ToListAsync(ct);

            return rows.Select(r => (r.InviterClientId, r.Count, r.LastValidatedAt)).ToList();
        }
    }

    public class ReferralMonthlySettlementRepository : IReferralMonthlySettlementRepository
    {
        private readonly RadarDbContext _context;
        public ReferralMonthlySettlementRepository(RadarDbContext context) => _context = context;

        public Task<ReferralMonthlySettlement?> GetByPeriodAsync(int year, int month, CancellationToken ct = default)
            => _context.ReferralMonthlySettlements.FirstOrDefaultAsync(s => s.Year == year && s.Month == month, ct);

        public async Task AddAsync(ReferralMonthlySettlement settlement, CancellationToken ct = default)
            => await _context.ReferralMonthlySettlements.AddAsync(settlement, ct);
    }

    public class MipymeReferralRepository : IMipymeReferralRepository
    {
        private readonly RadarDbContext _context;
        public MipymeReferralRepository(RadarDbContext context) => _context = context;

        public async Task AddAsync(MipymeReferral referral, CancellationToken ct = default)
            => await _context.MipymeReferrals.AddAsync(referral, ct);

        public Task<MipymeReferral?> GetPendingByReferredTenantAsync(Guid referredTenantId, CancellationToken ct = default)
            => _context.MipymeReferrals.FirstOrDefaultAsync(
                r => r.ReferredTenantId == referredTenantId && r.Status == Domain.BoundedContext.Radar.Enums.ReferralStatus.Pending, ct);

        public Task UpdateAsync(MipymeReferral referral, CancellationToken ct = default)
        {
            _context.MipymeReferrals.Update(referral);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<(Guid ReferrerClientId, int Count, DateTime LastValidatedAt)>> GetValidCountsByReferrerForMonthAsync(
            int year, int month, CancellationToken ct = default)
        {
            var rows = await _context.MipymeReferrals
                .Where(r => r.Status == Domain.BoundedContext.Radar.Enums.ReferralStatus.Valid
                    && r.ValidatedAt != null
                    && r.ValidatedAt.Value.Year == year
                    && r.ValidatedAt.Value.Month == month)
                .GroupBy(r => r.ReferrerClientId)
                .Select(g => new { ReferrerClientId = g.Key, Count = g.Count(), LastValidatedAt = g.Max(r => r.ValidatedAt!.Value) })
                .ToListAsync(ct);

            return rows.Select(r => (r.ReferrerClientId, r.Count, r.LastValidatedAt)).ToList();
        }
    }

    public class MipymeReferralMonthlySettlementRepository : IMipymeReferralMonthlySettlementRepository
    {
        private readonly RadarDbContext _context;
        public MipymeReferralMonthlySettlementRepository(RadarDbContext context) => _context = context;

        public Task<MipymeReferralMonthlySettlement?> GetByPeriodAsync(int year, int month, CancellationToken ct = default)
            => _context.MipymeReferralMonthlySettlements.FirstOrDefaultAsync(s => s.Year == year && s.Month == month, ct);

        public async Task AddAsync(MipymeReferralMonthlySettlement settlement, CancellationToken ct = default)
            => await _context.MipymeReferralMonthlySettlements.AddAsync(settlement, ct);
    }

    public class AvailabilityReportRepository : IAvailabilityReportRepository
    {
        private readonly RadarDbContext _context;
        public AvailabilityReportRepository(RadarDbContext context) => _context = context;

        public Task<AvailabilityReport?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _context.AvailabilityReports
                .Include(r => r.Confirmations)
                .FirstOrDefaultAsync(r => r.Id == id, ct);

        public async Task AddAsync(AvailabilityReport report, CancellationToken ct = default)
            => await _context.AvailabilityReports.AddAsync(report, ct);

        public Task UpdateAsync(AvailabilityReport report, CancellationToken ct = default)
        {
            _context.AvailabilityReports.Update(report);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<AvailabilityReport>> GetRecentAsync(
            string? productQuery, string? city, int freshnessMinutes, CancellationToken ct = default)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-Math.Abs(freshnessMinutes));
            var query = _context.AvailabilityReports
                .Include(r => r.Confirmations)
                .Where(r => r.ReportedAt >= cutoff)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(productQuery))
            {
                var normalized = AvailabilityReport.Normalize(productQuery);
                query = query.Where(r => r.NormalizedProductName.Contains(normalized));
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                var c = city.Trim();
                query = query.Where(r => r.City.ToLower() == c.ToLower());
            }

            return await query
                .OrderByDescending(r => r.LastActivityAt)
                .ToListAsync(ct);
        }
    }

    public class AvailabilityAlertRepository : IAvailabilityAlertRepository
    {
        private readonly RadarDbContext _context;
        public AvailabilityAlertRepository(RadarDbContext context) => _context = context;

        public Task<AvailabilityAlert?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _context.AvailabilityAlerts.FirstOrDefaultAsync(a => a.Id == id, ct);

        public async Task<IReadOnlyList<AvailabilityAlert>> GetByClientAsync(Guid clientId, CancellationToken ct = default)
            => await _context.AvailabilityAlerts
                .Where(a => a.ClientId == clientId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<AvailabilityAlert>> GetActiveAsync(CancellationToken ct = default)
            => await _context.AvailabilityAlerts
                .Where(a => a.IsActive)
                .ToListAsync(ct);

        public async Task AddAsync(AvailabilityAlert alert, CancellationToken ct = default)
            => await _context.AvailabilityAlerts.AddAsync(alert, ct);

        public Task UpdateAsync(AvailabilityAlert alert, CancellationToken ct = default)
        {
            _context.AvailabilityAlerts.Update(alert);
            return Task.CompletedTask;
        }
    }

    public class CommunityQuestionRepository : ICommunityQuestionRepository
    {
        private readonly RadarDbContext _context;
        public CommunityQuestionRepository(RadarDbContext context) => _context = context;

        public Task<CommunityQuestion?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _context.CommunityQuestions
                .Include(q => q.Answers)
                .FirstOrDefaultAsync(q => q.Id == id, ct);

        public async Task<IReadOnlyList<CommunityQuestion>> GetRecentAsync(string? city, int limit, CancellationToken ct = default)
        {
            var query = _context.CommunityQuestions.Include(q => q.Answers).AsQueryable();
            if (!string.IsNullOrWhiteSpace(city))
            {
                var c = city.Trim().ToLower();
                query = query.Where(q => q.City != null && q.City.ToLower() == c);
            }
            return await query.OrderByDescending(q => q.CreatedAt).Take(limit).ToListAsync(ct);
        }

        public async Task AddAsync(CommunityQuestion question, CancellationToken ct = default)
            => await _context.CommunityQuestions.AddAsync(question, ct);

        public Task UpdateAsync(CommunityQuestion question, CancellationToken ct = default)
        {
            _context.CommunityQuestions.Update(question);
            return Task.CompletedTask;
        }
    }
}
