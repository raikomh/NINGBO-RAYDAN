using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.Exceptions;
using Xunit;

namespace BusinessSearcher.Tests.Domain
{
    public class TenantTests
    {
        private static Tenant CreateValidTenant() =>
            Tenant.Create("Mi Restaurante", "test@test.com", "hashedpassword123", TenantType.Retail);

        [Fact]
        public void Create_WithValidData_ShouldCreateTenant()
        {
            var tenant = CreateValidTenant();

            Assert.Equal("Mi Restaurante", tenant.BusinessName);
            Assert.Equal("test@test.com", tenant.Email.Value);
            Assert.Equal("Standard", tenant.Plan);
            Assert.Equal(TenantStatus.Trial, tenant.Status);
            Assert.Single(tenant.DomainEvents); // TenantRegisteredEvent
        }

        [Theory]
        [InlineData(TenantType.Wholesale)]
        [InlineData(TenantType.Retail)]
        public void Create_WithType_SetsType(TenantType type)
        {
            var tenant = Tenant.Create("Mi Negocio", "test@test.com", "hash", type);
            Assert.Equal(type, tenant.Type);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("AB")]
        public void Create_WithInvalidBusinessName_ShouldThrowDomainException(string name)
        {
            Assert.Throws<DomainException>(() =>
                Tenant.Create(name, "test@test.com", "hash", TenantType.Retail));
        }

        [Fact]
        public void Create_WithInvalidEmail_ShouldThrowDomainException()
        {
            Assert.Throws<DomainException>(() =>
                Tenant.Create("Mi Negocio", "not-an-email", "hash", TenantType.Retail));
        }

        [Fact]
        public void Activate_WhenTrial_ShouldSetStatusActive()
        {
            var tenant = CreateValidTenant();
            tenant.Activate();
            Assert.Equal(TenantStatus.Active, tenant.Status);
        }

        [Fact]
        public void Activate_WhenAlreadyActive_ShouldThrowDomainException()
        {
            var tenant = CreateValidTenant();
            tenant.Activate();
            Assert.Throws<DomainException>(() => tenant.Activate());
        }

        [Fact]
        public void RegisterPayment_WithValidAmount_ShouldAddPaymentAndUpdateDates()
        {
            var tenant = CreateValidTenant();
            tenant.RegisterPayment(25000m, "COP", "REF-001");

            Assert.Single(tenant.Payments);
            Assert.Equal(25000m, tenant.GetTotalPaid());
            Assert.NotNull(tenant.LastPaymentDate);
            Assert.NotNull(tenant.NextPaymentDate);
        }

        [Fact]
        public void RegisterPayment_WithZeroAmount_ShouldThrowDomainException()
        {
            var tenant = CreateValidTenant();
            Assert.Throws<DomainException>(() => tenant.RegisterPayment(0m));
        }

        [Fact]
        public void IssueRefreshToken_ShouldAddActiveToken()
        {
            var tenant = CreateValidTenant();
            var token  = tenant.IssueRefreshToken("token123", TimeSpan.FromDays(30));

            Assert.Single(tenant.RefreshTokens);
            Assert.True(token.IsActive);
            Assert.False(token.IsExpired);
        }

        [Fact]
        public void RotateRefreshToken_WithExpiredOrRevokedToken_ShouldRevokeAllAndThrow()
        {
            var tenant = CreateValidTenant();
            tenant.IssueRefreshToken("old-token", TimeSpan.FromDays(30));
            tenant.RevokeRefreshToken("old-token");

            Assert.Throws<DomainException>(() =>
                tenant.RotateRefreshToken("old-token", "new-token", TimeSpan.FromDays(30)));
        }

        [Fact]
        public void RequestPasswordReset_ShouldCreateResetTokenAndEmitEvent()
        {
            var tenant = CreateValidTenant();
            tenant.ClearDomainEvents();

            var resetToken = tenant.RequestPasswordReset("reset-token-123", TimeSpan.FromMinutes(60));

            Assert.Single(tenant.PasswordResetTokens);
            Assert.True(resetToken.IsValid);
            Assert.Contains(tenant.DomainEvents, e => e is PasswordResetRequestedEvent);
        }

        [Fact]
        public void ResetPassword_WithValidToken_ShouldUpdatePasswordAndRevokeTokens()
        {
            var tenant = CreateValidTenant();
            tenant.IssueRefreshToken("active-token", TimeSpan.FromDays(30));
            tenant.RequestPasswordReset("reset-token", TimeSpan.FromMinutes(60));

            tenant.ResetPassword("reset-token", "newhashedpassword");

            Assert.Equal("newhashedpassword", tenant.PasswordHash);
            Assert.All(tenant.RefreshTokens, rt => Assert.False(rt.IsActive));
        }

    }
}
