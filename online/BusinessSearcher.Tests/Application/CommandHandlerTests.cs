using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Features.TenantManagement.Commands.LoginTenant;
using BusinessSearcher.Application.Features.TenantManagement.Commands.RegisterTenant;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using Moq;
using Xunit;
using Tenant = BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates.Tenant;
using RadarClientRepository = BusinessSearcher.Domain.BoundedContext.Radar.Repositories.IClientRepository;
using RadarMipymeReferralRepository = BusinessSearcher.Domain.BoundedContext.Radar.Repositories.IMipymeReferralRepository;

namespace BusinessSearcher.Tests.Application
{
    public class RegisterTenantCommandHandlerTests
    {
        private static RegisterTenantCommandHandler CreateHandler(
            Mock<ITenantRepository>? repoMock = null,
            Mock<IUnitOfWork>? uowMock = null,
            Mock<IPasswordHasher>? hasherMock = null,
            Mock<ISecureTokenGenerator>? tokenGenMock = null,
            Mock<ITenantSchemaManager>? schemaMock = null,
            Mock<IEmailService>? emailMock = null,
            Mock<RadarClientRepository>? radarClientsMock = null,
            Mock<RadarMipymeReferralRepository>? mipymeReferralsMock = null,
            Mock<IRadarUnitOfWork>? radarUowMock = null)
        {
            var repo    = repoMock    ?? new Mock<ITenantRepository>();
            var uow     = uowMock     ?? new Mock<IUnitOfWork>();
            var hasher  = hasherMock  ?? new Mock<IPasswordHasher>();
            var tokenGen = tokenGenMock ?? new Mock<ISecureTokenGenerator>();
            var schema  = schemaMock  ?? new Mock<ITenantSchemaManager>();
            var email   = emailMock   ?? new Mock<IEmailService>();
            // Programa de referidos cliente-refiere-MiPyme (bounded context Radar): sin
            // código de referido en los tests existentes, GetByReferralCodeAsync no se
            // configura y Moq devuelve null por defecto, así que esa rama no se ejercita.
            var radarClients    = radarClientsMock    ?? new Mock<RadarClientRepository>();
            var mipymeReferrals = mipymeReferralsMock ?? new Mock<RadarMipymeReferralRepository>();
            var radarUow        = radarUowMock        ?? new Mock<IRadarUnitOfWork>();

            hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashedpassword");
            tokenGen.Setup(t => t.Generate(It.IsAny<int>())).Returns("fake-refresh-token");

            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<RegisterTenantCommandHandler>>();

            var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            config.Setup(c => c["Frontend:DashboardUrl"]).Returns("https://app.businesssearcher.com");

            return new RegisterTenantCommandHandler(repo.Object, uow.Object, hasher.Object,
                tokenGen.Object, schema.Object, email.Object, config.Object, logger.Object,
                radarClients.Object, mipymeReferrals.Object, radarUow.Object);
        }

        [Fact]
        public async Task Handle_WithValidCommand_ShouldRegisterTenantPendingApproval()
        {
            var cmd     = new RegisterTenantCommand("Mi Negocio", "test@test.com", "Pass123!@#", "Retail");
            var handler = CreateHandler();

            var result = await handler.Handle(cmd, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Mi Negocio", result.BusinessName);
        }

        [Theory]
        [InlineData("", "test@test.com", "Pass123!@#")]
        [InlineData("AB", "test@test.com", "Pass123!@#")] // < 3 chars
        public async Task Handle_WithInvalidBusinessName_ShouldThrowDomainException(
            string name, string email, string password)
        {
            var cmd     = new RegisterTenantCommand(name, email, password, "Retail");
            var handler = CreateHandler();

            await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WithWeakPassword_ShouldThrowValidationException()
        {
            // ValidationException viene del Validator, no del Handler
            // Este test documenta que la validación ocurre antes
            var cmd = new RegisterTenantCommand("Mi Negocio", "test@test.com", "weak", "Retail");
            var validator = new RegisterTenantCommandValidator();

            var result = await validator.ValidateAsync(cmd);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public async Task Handle_WithInvalidEmail_ShouldThrowDomainException()
        {
            var cmd     = new RegisterTenantCommand("Mi Negocio", "not-an-email", "Pass123!@#", "Retail");
            var handler = CreateHandler();

            await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WithInvalidTenantType_ShouldThrowValidationException()
        {
            var cmd = new RegisterTenantCommand("Mi Negocio", "test@test.com", "Pass123!@#", "NotAValidType");
            var validator = new RegisterTenantCommandValidator();

            var result = await validator.ValidateAsync(cmd);

            Assert.False(result.IsValid);
        }
    }

    public class LoginTenantCommandHandlerTests
    {
        private static LoginTenantCommandHandler CreateHandler(
            Mock<ITenantRepository>? repoMock = null,
            Mock<IUnitOfWork>? uowMock = null,
            Mock<IPasswordHasher>? hasherMock = null,
            Mock<IJwtTokenService>? jwtMock = null,
            Mock<ISecureTokenGenerator>? tokenGenMock = null)
        {
            var repo    = repoMock    ?? new Mock<ITenantRepository>();
            var uow     = uowMock     ?? new Mock<IUnitOfWork>();
            var hasher  = hasherMock  ?? new Mock<IPasswordHasher>();
            var jwt     = jwtMock     ?? new Mock<IJwtTokenService>();
            var tokenGen = tokenGenMock ?? new Mock<ISecureTokenGenerator>();
            var config   = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();

            // Solo por defecto: si el test aporta su propio hasher, su Setup debe prevalecer.
            if (hasherMock is null)
                hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TenantType>()))
                .Returns("fake-jwt-token");
            tokenGen.Setup(t => t.Generate(It.IsAny<int>())).Returns("fake-refresh-token");
            config.Setup(c => c["Chat:AdminEmail"]).Returns("admin@businesssearcher.dev");

            return new LoginTenantCommandHandler(repo.Object, uow.Object, hasher.Object, jwt.Object, tokenGen.Object, config.Object);
        }

        [Fact]
        public async Task Handle_WithValidCredentials_ShouldReturnAuthResult()
        {
            var repoMock = new Mock<ITenantRepository>();
            var tenant = Tenant.Create(
                "Mi Negocio", "test@test.com", "hashedpassword", TenantType.Retail);
            // El login exige email verificado y aprobación de un admin
            tenant.VerifyEmail(tenant.RequestEmailVerification("verify-token", TimeSpan.FromDays(1)).Token);
            tenant.Approve();
            repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);

            var cmd     = new LoginTenantCommand("test@test.com", "testpass123");
            var handler = CreateHandler(repoMock);

            var result = await handler.Handle(cmd, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.Token);
            Assert.Equal(tenant.BusinessName, result.Tenant.BusinessName);
        }

        [Fact]
        public async Task Handle_WithUnapprovedAccount_ShouldThrowDomainException()
        {
            var repoMock = new Mock<ITenantRepository>();
            var tenant = Tenant.Create(
                "Mi Negocio", "test@test.com", "hashedpassword", TenantType.Retail);
            tenant.VerifyEmail(tenant.RequestEmailVerification("verify-token", TimeSpan.FromDays(1)).Token);
            // No se llama a tenant.Approve(): sigue pendiente de aprobación

            repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);

            var cmd     = new LoginTenantCommand("test@test.com", "testpass123");
            var handler = CreateHandler(repoMock);

            await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WithNonexistentEmail_ShouldThrowUnauthorized()
        {
            var repoMock = new Mock<ITenantRepository>();
            repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Tenant?)null);

            var cmd     = new LoginTenantCommand("nonexistent@test.com", "password");
            var handler = CreateHandler(repoMock);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WithWrongPassword_ShouldThrowUnauthorized()
        {
            var hasherMock = new Mock<IPasswordHasher>();
            hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            var repoMock = new Mock<ITenantRepository>();
            var tenant = Tenant.Create(
                "Mi Negocio", "test@test.com", "hashedpassword", TenantType.Retail);
            repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);

            var cmd     = new LoginTenantCommand("test@test.com", "wrongpassword");
            var handler = CreateHandler(repoMock, hasherMock: hasherMock);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WithSuspendedAccount_ShouldThrowDomainException()
        {
            var repoMock = new Mock<ITenantRepository>();
            var tenant = Tenant.Create(
                "Mi Negocio", "test@test.com", "hashedpassword", TenantType.Retail);
            tenant.Suspend();

            repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);

            var cmd     = new LoginTenantCommand("test@test.com", "testpass");
            var handler = CreateHandler(repoMock);

            await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        }
    }
}
