using BusinessSearcher.Domain.BoundedContext.StoreManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.ValueObjects;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.Exceptions;
using BusinessSearcher.Tests.Helpers;
using Xunit;

namespace BusinessSearcher.Tests.Integration
{
    /// <summary>
    /// Tests de integración que validan flujos completos del dominio.
    /// Estos tests NO usan base de datos; prueban la lógica de negocio
    /// del agregado en conjunto (múltiples métodos interactuando).
    /// </summary>

    public class TenantLifecycleIntegrationTests
    {
        [Fact]
        public void FullTenantLifecycle_ShouldHandleAllTransitions()
        {
            // Arrange
            var tenant = new TenantBuilder()
                .WithBusinessName("Growing Business")
                .WithEmail("owner@growing.com")
                .Build();

            // Act & Assert - Ciclo completo
            Assert.Equal(TenantStatus.Trial, tenant.Status);

            // Registra pago
            tenant.RegisterPayment(50000m, "COP", "PAY-001");
            Assert.Single(tenant.Payments);
            Assert.Equal(50000m, tenant.GetTotalPaid());

            // Activa (pasa de Trial a Active)
            tenant.Activate();
            Assert.Equal(TenantStatus.Active, tenant.Status);

            // Suscripción se renueva
            tenant.RegisterPayment(50000m, "COP", "PAY-002");
            Assert.Equal(2, tenant.Payments.Count);
            Assert.Equal(100000m, tenant.GetTotalPaid());

            // Desactiva
            tenant.Deactivate();
            Assert.Equal(TenantStatus.Inactive, tenant.Status);

            // Reactiva
            tenant.Activate();
            Assert.Equal(TenantStatus.Active, tenant.Status);

            // Suspende
            tenant.Suspend();
            Assert.Equal(TenantStatus.Suspended, tenant.Status);
        }

        [Fact]
        public void RefreshTokenRotation_WithValidToken_ShouldMaintainActiveSessions()
        {
            // Arrange
            var tenant = new TenantBuilder().Build();
            var oldToken = tenant.IssueRefreshToken("token-1", TimeSpan.FromDays(30));

            // Act - Rota el token
            var newToken = tenant.RotateRefreshToken("token-1", "token-2", TimeSpan.FromDays(30));

            // Assert
            var oldTokenAfterRotation = tenant.RefreshTokens.First(t => t.Token == "token-1");
            Assert.False(oldTokenAfterRotation.IsActive);
            Assert.Equal("token-2", oldTokenAfterRotation.ReplacedByToken);

            Assert.True(newToken.IsActive);
            Assert.Equal("token-2", newToken.Token);
        }

        [Fact]
        public void SecurityBreach_TokenReuse_ShouldInvalidateAllSessions()
        {
            // Arrange
            var tenant = new TenantBuilder().Build();
            tenant.IssueRefreshToken("token-1", TimeSpan.FromDays(30));
            tenant.IssueRefreshToken("token-2", TimeSpan.FromDays(30));

            // Act - Intenta reutilizar un token revocado
            tenant.RevokeRefreshToken("token-1");
            var exception = Assert.Throws<DomainException>(() =>
                tenant.RotateRefreshToken("token-1", "token-3", TimeSpan.FromDays(30)));

            // Assert - Todas las sesiones fueron revocadas como medida de seguridad
            Assert.All(tenant.RefreshTokens, rt => Assert.False(rt.IsActive));
            Assert.Contains("reutilización", exception.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PasswordReset_ShouldInvalidateAllSessions()
        {
            // Arrange
            var tenant = new TenantBuilder().Build();
            tenant.IssueRefreshToken("session-1", TimeSpan.FromDays(30));
            tenant.IssueRefreshToken("session-2", TimeSpan.FromDays(30));
            var resetToken = tenant.RequestPasswordReset("reset-token", TimeSpan.FromMinutes(60));

            // Act
            tenant.ResetPassword("reset-token", "new-hashed-password");

            // Assert
            Assert.Equal("new-hashed-password", tenant.PasswordHash);
            Assert.All(tenant.RefreshTokens, rt => Assert.False(rt.IsActive));
        }
    }

    public class StoreManagementIntegrationTests
    {
        [Fact]
        public void MultipleSchedules_ShouldHandleEachDayIndependently()
        {
            // Arrange
            var store = new StoreBuilder().Build();
            var morningSchedule = ScheduleTime.Create("06:00", "12:00");
            var afternoonSchedule = ScheduleTime.Create("12:00", "22:00");

            // Act - Configura horarios para cada día
            store.AddOrUpdateSchedule(DayOfWeek.Monday, afternoonSchedule);
            store.AddOrUpdateSchedule(DayOfWeek.Saturday, morningSchedule);
            store.AddOrUpdateSchedule(DayOfWeek.Sunday, ScheduleTime.Create("08:00", "20:00"));

            // Assert
            Assert.Equal(3, store.Schedules.Count);

            var monday = store.Schedules.First(s => s.DayOfWeek == DayOfWeek.Monday);
            Assert.Equal(afternoonSchedule.OpenTime, monday.Time.OpenTime);

            var saturday = store.Schedules.First(s => s.DayOfWeek == DayOfWeek.Saturday);
            Assert.Equal(morningSchedule.OpenTime, saturday.Time.OpenTime);
        }
    }

    public class ValidationAndSecurityIntegrationTests
    {
        [Theory]
        [InlineData("", "desc", "country")]
        [InlineData("street", "", "country")]
        [InlineData("street", "city", "")]
        public void Address_RequiresAllFields(string street, string city, string country)
        {
            Assert.Throws<DomainException>(() =>
                new Address(street, city, "state", country));
        }

        [Fact]
        public void Money_CannotBeNegative()
        {
            Assert.Throws<DomainException>(() => new Money(-100m));
        }

        [Theory]
        [InlineData("08:00", "08:00")]
        [InlineData("18:00", "08:00")]
        public void ScheduleTime_CloseTimeCannotBeBeforeOrEqualToOpenTime(string open, string close)
        {
            Assert.Throws<DomainException>(() => ScheduleTime.Create(open, close));
        }

        [Fact]
        public void Tenant_PasswordResetToken_ExpiresAfterUse()
        {
            // Arrange
            var tenant = new TenantBuilder().Build();
            var token = tenant.RequestPasswordReset("token-123", TimeSpan.FromMinutes(60));

            // Act & Assert - Primer uso debe funcionar
            Assert.True(token.IsValid);
            tenant.ResetPassword("token-123", "new-password");

            // El token ahora está usado
            var usedToken = tenant.PasswordResetTokens.First();
            Assert.True(usedToken.IsUsed);
            Assert.False(usedToken.IsValid);

            // Intento de reutilizar falla
            Assert.Throws<DomainException>(() =>
                tenant.ResetPassword("token-123", "another-password"));
        }

        [Fact]
        public void Tenant_CannotRegisterNegativePayment()
        {
            var tenant = new TenantBuilder().Build();
            Assert.Throws<DomainException>(() => tenant.RegisterPayment(-1000m));
        }

        [Fact]
        public void Tenant_CannotRegisterZeroPayment()
        {
            var tenant = new TenantBuilder().Build();
            Assert.Throws<DomainException>(() => tenant.RegisterPayment(0m));
        }
    }
}
