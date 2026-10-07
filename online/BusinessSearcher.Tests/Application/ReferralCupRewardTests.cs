using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Features.Radar.Commands;
using BusinessSearcher.Application.Features.Radar.Commands.SetClientPlan;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using Moq;
using Xunit;

namespace BusinessSearcher.Tests.Application
{
    public class ClientReferralCupWalletTests
    {
        [Fact]
        public void CreditReferralCup_ShouldIncreaseBalanceAndLogTransaction()
        {
            var client = Client.Create("Test User", "user@test.com", "hash");

            client.CreditReferralCup(10m, ReferralCupTransactionType.PerReferralBonus);
            client.CreditReferralCup(10m, ReferralCupTransactionType.PerReferralBonus);

            Assert.Equal(20m, client.ReferralCupBalance);
            Assert.Equal(2, client.ReferralCupTransactions.Count);
        }

        [Fact]
        public void CreditReferralCup_WithZeroOrNegativeAmount_ShouldThrow()
        {
            var client = Client.Create("Test User", "user@test.com", "hash");
            Assert.Throws<DomainException>(() => client.CreditReferralCup(0m, ReferralCupTransactionType.PerReferralBonus));
            Assert.Throws<DomainException>(() => client.CreditReferralCup(-5m, ReferralCupTransactionType.PerReferralBonus));
        }

        [Fact]
        public void SettleReferralCupPayout_ShouldMoveBalanceToPaidTotalAndResetToZero()
        {
            var client = Client.Create("Test User", "user@test.com", "hash");
            client.CreditReferralCup(30m, ReferralCupTransactionType.PerReferralBonus);

            var paid = client.SettleReferralCupPayout();

            Assert.Equal(30m, paid);
            Assert.Equal(0m, client.ReferralCupBalance);
            Assert.Equal(30m, client.ReferralCupPaidTotal);
        }

        [Fact]
        public void SettleReferralCupPayout_WithNoBalance_ShouldBeNoOp()
        {
            var client = Client.Create("Test User", "user@test.com", "hash");

            var paid = client.SettleReferralCupPayout();

            Assert.Equal(0m, paid);
            Assert.Equal(0m, client.ReferralCupPaidTotal);
        }
    }

    public class ReferralRewardTests
    {
        [Fact]
        public async Task ValidateAndRewardAsync_WithPendingReferral_ShouldCreditTenCupAndValidateOnce()
        {
            var inviter  = Client.Create("Inviter", "inviter@test.com", "hash");
            var invited  = Client.Create("Invited", "invited@test.com", "hash");
            var referral = Referral.Create(inviter.Id, invited.Id);

            var clientsMock = new Mock<IClientRepository>();
            clientsMock.Setup(c => c.GetByIdAsync(inviter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inviter);

            var referralsMock = new Mock<IReferralRepository>();
            referralsMock.Setup(r => r.GetPendingByInvitedAsync(invited.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(referral);

            // Primera vez: valida y premia.
            await ReferralReward.ValidateAndRewardAsync(invited.Id, clientsMock.Object, referralsMock.Object, CancellationToken.None);

            Assert.Equal(10m, inviter.ReferralCupBalance);
            Assert.Equal(1, inviter.ValidReferralsThisMonth);
            Assert.Equal(ReferralStatus.Valid, referral.Status);

            // Segunda vez (p. ej. si además se hace Premium después de ser aprobado):
            // MarkValid() ya no hace nada porque el estado ya es Valid, así que no debe
            // volver a acreditar CUP ni volver a subir el contador mensual.
            referralsMock.Setup(r => r.GetPendingByInvitedAsync(invited.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Referral?)null); // ya no está "pendiente"

            await ReferralReward.ValidateAndRewardAsync(invited.Id, clientsMock.Object, referralsMock.Object, CancellationToken.None);

            Assert.Equal(10m, inviter.ReferralCupBalance);
            Assert.Equal(1, inviter.ValidReferralsThisMonth);
        }

        [Fact]
        public async Task ValidateAndRewardAsync_AtTenthReferral_ShouldAlsoGrantPremiumDaysTier()
        {
            var inviter = Client.Create("Inviter", "inviter@test.com", "hash");
            var clientsMock = new Mock<IClientRepository>();
            clientsMock.Setup(c => c.GetByIdAsync(inviter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inviter);

            var referralsMock = new Mock<IReferralRepository>();

            for (var i = 0; i < 10; i++)
            {
                var invited  = Client.Create($"Invited {i}", $"invited{i}@test.com", "hash");
                var referral = Referral.Create(inviter.Id, invited.Id);
                referralsMock.Setup(r => r.GetPendingByInvitedAsync(invited.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(referral);

                await ReferralReward.ValidateAndRewardAsync(invited.Id, clientsMock.Object, referralsMock.Object, CancellationToken.None);
            }

            Assert.Equal(100m, inviter.ReferralCupBalance); // 10 referidos × 10 CUP
            Assert.Equal(10, inviter.ValidReferralsThisMonth);
            Assert.NotNull(inviter.PremiumUntil); // tier de 10 referidos = 7 días Premium
        }
    }

    public class AdminCloseMonthlyReferralCompetitionCommandHandlerTests
    {
        [Fact]
        public async Task Handle_WithNoValidReferrals_ShouldSettleWithZeroPrizeAndNoWinner()
        {
            var clientsMock     = new Mock<IClientRepository>();
            var referralsMock   = new Mock<IReferralRepository>();
            var settlementsMock = new Mock<IReferralMonthlySettlementRepository>();
            var uowMock         = new Mock<IRadarUnitOfWork>();

            settlementsMock.Setup(s => s.GetByPeriodAsync(2026, 8, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ReferralMonthlySettlement?)null);
            referralsMock.Setup(r => r.GetValidCountsByInviterForMonthAsync(2026, 8, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<(Guid, int, DateTime)>());

            var handler = new AdminCloseMonthlyReferralCompetitionCommandHandler(
                clientsMock.Object, referralsMock.Object, settlementsMock.Object, uowMock.Object);

            var result = await handler.Handle(new AdminCloseMonthlyReferralCompetitionCommand(2026, 8), CancellationToken.None);

            Assert.Null(result.WinnerClientId);
            Assert.Equal(0m, result.PrizeAmount);
            settlementsMock.Verify(s => s.AddAsync(It.IsAny<ReferralMonthlySettlement>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithValidReferrals_ShouldCreditWinnerWithTwentyPercentOfAssumedRevenue()
        {
            var winner = Client.Create("Top Inviter", "top@test.com", "hash");
            var runnerUp = Guid.NewGuid();

            var clientsMock     = new Mock<IClientRepository>();
            var referralsMock   = new Mock<IReferralRepository>();
            var settlementsMock = new Mock<IReferralMonthlySettlementRepository>();
            var uowMock         = new Mock<IRadarUnitOfWork>();

            settlementsMock.Setup(s => s.GetByPeriodAsync(2026, 8, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ReferralMonthlySettlement?)null);

            // R = 5 (winner) + 3 (runner-up) = 8 referidos válidos del mes.
            referralsMock.Setup(r => r.GetValidCountsByInviterForMonthAsync(2026, 8, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new (Guid, int, DateTime)[]
                {
                    (winner.Id, 5, DateTime.UtcNow),
                    (runnerUp, 3, DateTime.UtcNow),
                });
            clientsMock.Setup(c => c.GetByIdAsync(winner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(winner);

            var handler = new AdminCloseMonthlyReferralCompetitionCommandHandler(
                clientsMock.Object, referralsMock.Object, settlementsMock.Object, uowMock.Object);

            var result = await handler.Handle(new AdminCloseMonthlyReferralCompetitionCommand(2026, 8), CancellationToken.None);

            // Premio = SOLO los referidos del ganador (5), no el total de la plataforma (8):
            // P = R_ganador * 40 * 0.20 = 5 * 8 = 40
            Assert.Equal(winner.Id, result.WinnerClientId);
            Assert.Equal(40m, result.PrizeAmount);
            Assert.Equal(5, result.TotalValidReferrals);
            Assert.Equal(40m, winner.ReferralCupBalance);
        }

        [Fact]
        public async Task Handle_WhenMonthAlreadySettled_ShouldThrowDomainException()
        {
            var settlementsMock = new Mock<IReferralMonthlySettlementRepository>();
            settlementsMock.Setup(s => s.GetByPeriodAsync(2026, 8, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ReferralMonthlySettlement.Create(2026, 8, Guid.NewGuid(), 64m, 8));

            var handler = new AdminCloseMonthlyReferralCompetitionCommandHandler(
                new Mock<IClientRepository>().Object, new Mock<IReferralRepository>().Object,
                settlementsMock.Object, new Mock<IRadarUnitOfWork>().Object);

            await Assert.ThrowsAsync<DomainException>(() =>
                handler.Handle(new AdminCloseMonthlyReferralCompetitionCommand(2026, 8), CancellationToken.None));
        }
    }
}
