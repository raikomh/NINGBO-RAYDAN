using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Features.TenantManagement.Commands.ChangePassword;
using BusinessSearcher.Application.Features.TenantManagement.Commands.ForgotPassword;
using BusinessSearcher.Application.Features.TenantManagement.Commands.ResetPassword;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.Exceptions;
using Moq;
using Xunit;

namespace BusinessSearcher.Tests.Application
{
    public class PasswordRecoveryCommandHandlerTests
    {
        [Fact]
        public async Task ForgotPassword_WithExistingEmail_ShouldSendEmail()
        {
            // Arrange
            var emailServiceMock = new Mock<IEmailService>();
            var repoMock = new Mock<BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories.ITenantRepository>();
            var tokenGenMock = new Mock<ISecureTokenGenerator>();
            var uowMock = new Mock<IUnitOfWork>();
            var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ForgotPasswordCommandHandler>>();

            var tenant = BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates.Tenant.Create(
                "Test Business", "user@test.com", "hashed", TenantType.Retail);

            repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);
            tokenGenMock.Setup(t => t.Generate(It.IsAny<int>())).Returns("reset-token-123");
            configMock.Setup(c => c["Frontend:ResetPasswordUrl"]).Returns("http://localhost:3000/reset");

            var handler = new ForgotPasswordCommandHandler(
                repoMock.Object, uowMock.Object, tokenGenMock.Object,
                emailServiceMock.Object, configMock.Object, loggerMock.Object);

            var cmd = new ForgotPasswordCommand("user@test.com");

            // Act
            await handler.Handle(cmd, CancellationToken.None);

            // Assert
            emailServiceMock.Verify(e => e.SendPasswordResetEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ForgotPassword_WithNonexistentEmail_ShouldNotThrow()
        {
            // Por seguridad, no revelamos si el email existe o no
            var emailServiceMock = new Mock<IEmailService>();
            var repoMock = new Mock<BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories.ITenantRepository>();
            var tokenGenMock = new Mock<ISecureTokenGenerator>();
            var uowMock = new Mock<IUnitOfWork>();
            var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ForgotPasswordCommandHandler>>();

            repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates.Tenant?)null);

            var handler = new ForgotPasswordCommandHandler(
                repoMock.Object, uowMock.Object, tokenGenMock.Object,
                emailServiceMock.Object, configMock.Object, loggerMock.Object);

            var cmd = new ForgotPasswordCommand("nonexistent@test.com");

            // Act & Assert — No lanza excepción
            await handler.Handle(cmd, CancellationToken.None);

            // Email NO se envía (porque el tenant no existe)
            emailServiceMock.Verify(e => e.SendPasswordResetEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ResetPassword_WithValidToken_ShouldUpdatePasswordAndNotifyUser()
        {
            // Arrange
            var repoMock = new Mock<BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories.ITenantRepository>();
            var uowMock = new Mock<IUnitOfWork>();
            var hasherMock = new Mock<IPasswordHasher>();
            var emailServiceMock = new Mock<IEmailService>();

            var tenant = BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates.Tenant.Create(
                "Test", "user@test.com", "old-hash", TenantType.Retail);
            tenant.RequestPasswordReset("reset-token", System.TimeSpan.FromMinutes(60));

            repoMock.Setup(r => r.GetByPasswordResetTokenAsync("reset-token", It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);
            hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("new-hashed-password");

            var handler = new ResetPasswordCommandHandler(
                repoMock.Object, uowMock.Object, hasherMock.Object, emailServiceMock.Object);

            var cmd = new ResetPasswordCommand("user@test.com", "reset-token", "NewPass123!@");

            // Act
            await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal("new-hashed-password", tenant.PasswordHash);
            emailServiceMock.Verify(e => e.SendPasswordChangedNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ResetPassword_WithInvalidToken_ShouldThrowDomainException()
        {
            var repoMock = new Mock<BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories.ITenantRepository>();
            repoMock.Setup(r => r.GetByPasswordResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates.Tenant?)null);

            var handler = new ResetPasswordCommandHandler(
                repoMock.Object, new Mock<IUnitOfWork>().Object,
                new Mock<IPasswordHasher>().Object, new Mock<IEmailService>().Object);

            var cmd = new ResetPasswordCommand("user@test.com", "invalid-token", "NewPass123!@");

            // Act & Assert
            await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task ChangePassword_WithCorrectCurrentPassword_ShouldUpdateAndCloseOtherSessions()
        {
            // Arrange
            var repoMock = new Mock<BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories.ITenantRepository>();
            var uowMock = new Mock<IUnitOfWork>();
            var hasherMock = new Mock<IPasswordHasher>();
            var currentUserMock = new Mock<ICurrentUserService>();
            var emailServiceMock = new Mock<IEmailService>();

            var tenant = BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates.Tenant.Create(
                "Test", "user@test.com", "current-hash", TenantType.Retail);
            tenant.IssueRefreshToken("session-1", System.TimeSpan.FromDays(30));

            var tenantId = tenant.Id;
            repoMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);
            hasherMock.Setup(h => h.Verify("current-password", "current-hash")).Returns(true);
            hasherMock.Setup(h => h.Hash("new-password")).Returns("new-hash");
            currentUserMock.Setup(c => c.TenantId).Returns(tenantId);

            var handler = new ChangePasswordCommandHandler(
                repoMock.Object, uowMock.Object, hasherMock.Object,
                currentUserMock.Object, emailServiceMock.Object);

            var cmd = new ChangePasswordCommand("current-password", "new-password");

            // Act
            await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal("new-hash", tenant.PasswordHash);
            Assert.All(tenant.RefreshTokens, rt => Assert.False(rt.IsActive)); // Todas las sesiones cerradas
        }

        [Fact]
        public async Task ChangePassword_WithWrongCurrentPassword_ShouldThrowDomainException()
        {
            var hasherMock = new Mock<IPasswordHasher>();
            hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            var tenant = BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates.Tenant.Create(
                "Test", "user@test.com", "hash", TenantType.Retail);

            var repoMock = new Mock<BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories.ITenantRepository>();
            repoMock.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);

            var currentUserMock = new Mock<ICurrentUserService>();
            currentUserMock.Setup(c => c.TenantId).Returns(tenant.Id);

            var handler = new ChangePasswordCommandHandler(
                repoMock.Object, new Mock<IUnitOfWork>().Object,
                hasherMock.Object, currentUserMock.Object, new Mock<IEmailService>().Object);

            var cmd = new ChangePasswordCommand("wrong-password", "new-password");

            // Act & Assert
            await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        }
    }
}
