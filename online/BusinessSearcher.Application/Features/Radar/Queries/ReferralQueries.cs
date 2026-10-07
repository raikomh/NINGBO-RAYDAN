using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.Radar.Commands.SetClientPlan;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Queries.Referrals
{
    /// <summary>Estado del programa de referidos del cliente autenticado.</summary>
    public record GetMyReferralsQuery : IRequest<MyReferralsDto>;

    public class GetMyReferralsQueryHandler : IRequestHandler<GetMyReferralsQuery, MyReferralsDto>
    {
        private readonly IClientRepository   _clients;
        private readonly IRadarUnitOfWork    _uow;
        private readonly ICurrentUserService _currentUser;

        public GetMyReferralsQueryHandler(
            IClientRepository clients, IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _clients = clients; _uow = uow; _currentUser = currentUser;
        }

        public async Task<MyReferralsDto> Handle(GetMyReferralsQuery request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            // Clientes creados antes del programa no tienen código: se genera al vuelo.
            if (string.IsNullOrWhiteSpace(client.ReferralCode))
            {
                client.AssignReferralCode(await GenerateUniqueCodeAsync(client.FullName, ct));
                await _clients.UpdateAsync(client, ct);
                await _uow.SaveChangesAsync(ct);
            }

            var month = client.ValidReferralsThisMonth;
            var tiers = ReferralReward.Tiers
                .Select(t => new ReferralTierDto(t.threshold, t.days, month >= t.threshold))
                .ToList();

            var next = ReferralReward.Tiers
                .Where(t => t.threshold > month)
                .Select(t => (int?)t.threshold)
                .FirstOrDefault();

            return new MyReferralsDto(
                ReferralCode:            client.ReferralCode!,
                ValidReferralsTotal:     client.ValidReferralsTotal,
                ValidReferralsThisMonth: month,
                NextThreshold:           next,
                MissingForNext:          next.HasValue ? next.Value - month : 0,
                PremiumUntil:            client.PremiumUntil?.ToString("o"),
                Tiers:                   tiers,
                CupBalance:              client.ReferralCupBalance,
                CupPaidTotal:            client.ReferralCupPaidTotal);
        }

        private async Task<string> GenerateUniqueCodeAsync(string fullName, CancellationToken ct)
        {
            var letters = new string((fullName ?? "USER")
                .ToUpperInvariant().Where(char.IsLetterOrDigit).Take(5).ToArray());
            if (letters.Length < 3) letters = "USER";

            for (var i = 0; i < 20; i++)
            {
                var code = $"{letters}{Random.Shared.Next(10, 99)}";
                if (!await _clients.ExistsReferralCodeAsync(code, ct)) return code;
            }
            return $"{letters}{Guid.NewGuid():N}".Substring(0, 12).ToUpperInvariant();
        }
    }

    /// <summary>
    /// Ranking de referidos válidos del mes (competencia mensual). Usado tanto por el
    /// cliente (ve el top + su propia posición) como por el admin (ve todo, sin "MyRank").
    /// Si no se pasa año/mes, usa el mes calendario actual (UTC).
    /// </summary>
    public record GetReferralLeaderboardQuery(int? Year = null, int? Month = null, int Take = 20)
        : IRequest<ReferralLeaderboardDto>;

    public class GetReferralLeaderboardQueryHandler
        : IRequestHandler<GetReferralLeaderboardQuery, ReferralLeaderboardDto>
    {
        private readonly IClientRepository   _clients;
        private readonly IReferralRepository _referrals;
        private readonly ICurrentUserService _currentUser;

        public GetReferralLeaderboardQueryHandler(
            IClientRepository clients, IReferralRepository referrals, ICurrentUserService currentUser)
        {
            _clients = clients; _referrals = referrals; _currentUser = currentUser;
        }

        public async Task<ReferralLeaderboardDto> Handle(GetReferralLeaderboardQuery request, CancellationToken ct)
        {
            var now   = DateTime.UtcNow;
            var year  = request.Year  ?? now.Year;
            var month = request.Month ?? now.Month;

            var counts = await _referrals.GetValidCountsByInviterForMonthAsync(year, month, ct);
            var ranked = counts
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.LastValidatedAt)
                .ToList();

            var take = Math.Clamp(request.Take, 1, 100);
            var top = new List<ReferralLeaderboardEntryDto>();
            int? myRank = null;
            var myCount = 0;

            for (var i = 0; i < ranked.Count; i++)
            {
                var rank = i + 1;
                var isMe = ranked[i].InviterClientId == _currentUser.AccountId;
                if (isMe)
                {
                    myRank  = rank;
                    myCount = ranked[i].Count;
                }

                if (i >= take) continue;

                var inviter = await _clients.GetByIdAsync(ranked[i].InviterClientId, ct);
                top.Add(new ReferralLeaderboardEntryDto(rank, inviter?.FullName ?? "—", ranked[i].Count, isMe));
            }

            return new ReferralLeaderboardDto(year, month, top, myRank, myCount);
        }
    }
}
