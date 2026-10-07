using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Queries.MipymeReferrals
{
    /// <summary>
    /// Estadística personal del programa de referidos de MiPymes del cliente autenticado.
    /// El saldo/ganancia en CUP se consulta en el endpoint unificado /api/v1/client/referrals/me
    /// (Client.ReferralCupBalance), que ya incluye los bonos de MiPymes — no hay saldo aparte.
    /// </summary>
    public record GetMyMipymeReferralsQuery : IRequest<MyMipymeReferralsDto>;

    public class GetMyMipymeReferralsQueryHandler : IRequestHandler<GetMyMipymeReferralsQuery, MyMipymeReferralsDto>
    {
        private readonly IClientRepository   _clients;
        private readonly ICurrentUserService _currentUser;

        public GetMyMipymeReferralsQueryHandler(IClientRepository clients, ICurrentUserService currentUser)
        {
            _clients = clients; _currentUser = currentUser;
        }

        public async Task<MyMipymeReferralsDto> Handle(GetMyMipymeReferralsQuery request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            return new MyMipymeReferralsDto(client.MipymeReferralsTotal, client.MipymeReferralsThisMonth);
        }
    }

    /// <summary>
    /// Ranking de MiPymes referidas y validadas en el mes (competencia mensual paralela a la
    /// de clientes). Mismo ranking/paginado/clamping que GetReferralLeaderboardQuery.
    /// </summary>
    public record GetMipymeReferralLeaderboardQuery(int? Year = null, int? Month = null, int Take = 20)
        : IRequest<MipymeReferralLeaderboardDto>;

    public class GetMipymeReferralLeaderboardQueryHandler
        : IRequestHandler<GetMipymeReferralLeaderboardQuery, MipymeReferralLeaderboardDto>
    {
        private readonly IClientRepository         _clients;
        private readonly IMipymeReferralRepository _mipymeReferrals;
        private readonly ICurrentUserService       _currentUser;

        public GetMipymeReferralLeaderboardQueryHandler(
            IClientRepository clients, IMipymeReferralRepository mipymeReferrals, ICurrentUserService currentUser)
        {
            _clients = clients; _mipymeReferrals = mipymeReferrals; _currentUser = currentUser;
        }

        public async Task<MipymeReferralLeaderboardDto> Handle(GetMipymeReferralLeaderboardQuery request, CancellationToken ct)
        {
            var now   = DateTime.UtcNow;
            var year  = request.Year  ?? now.Year;
            var month = request.Month ?? now.Month;

            var counts = await _mipymeReferrals.GetValidCountsByReferrerForMonthAsync(year, month, ct);
            var ranked = counts
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.LastValidatedAt)
                .ToList();

            var take = Math.Clamp(request.Take, 1, 100);
            var top = new List<MipymeReferralLeaderboardEntryDto>();
            int? myRank = null;
            var myCount = 0;

            for (var i = 0; i < ranked.Count; i++)
            {
                var rank = i + 1;
                var isMe = ranked[i].ReferrerClientId == _currentUser.AccountId;
                if (isMe)
                {
                    myRank  = rank;
                    myCount = ranked[i].Count;
                }

                if (i >= take) continue;

                var referrer = await _clients.GetByIdAsync(ranked[i].ReferrerClientId, ct);
                top.Add(new MipymeReferralLeaderboardEntryDto(rank, referrer?.FullName ?? "—", ranked[i].Count, isMe));
            }

            return new MipymeReferralLeaderboardDto(year, month, top, myRank, myCount);
        }
    }
}
