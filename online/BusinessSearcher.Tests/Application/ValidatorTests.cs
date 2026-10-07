using BusinessSearcher.Application.Features.TenantManagement.Commands.RegisterTenant;
using BusinessSearcher.Application.Features.TenantManagement.Commands.LoginTenant;
using BusinessSearcher.Application.Features.TenantManagement.Commands.ChangePassword;
using Xunit;

namespace BusinessSearcher.Tests.Application
{
    public class AuthCommandValidatorTests
    {
        private readonly RegisterTenantCommandValidator _registerValidator = new();
        private readonly LoginTenantCommandValidator _loginValidator = new();
        private readonly ChangePasswordCommandValidator _changePasswordValidator = new();

        [Theory]
        [InlineData("ValidBusiness", "user@example.com", "SecurePass123!@#")]
        [InlineData("A Super Long Business Name That Still Works Fine Here", "test@domain.co.uk", "Complex@Pass#123")]
        public async Task RegisterCommand_WithValidData_ShouldValidateSuccessfully(
            string businessName, string email, string password)
        {
            var cmd = new RegisterTenantCommand(businessName, email, password, "Retail");
            var result = await _registerValidator.ValidateAsync(cmd);
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("", "user@test.com", "Pass123!@#")] // Empty name
        [InlineData("AB", "user@test.com", "Pass123!@#")] // Too short
        [InlineData("A", "user@test.com", "Pass123!@#")] // Too short
        public async Task RegisterCommand_WithShortBusinessName_ShouldFail(
            string businessName, string email, string password)
        {
            var cmd = new RegisterTenantCommand(businessName, email, password, "Retail");
            var result = await _registerValidator.ValidateAsync(cmd);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("Business", "invalidemail", "Pass123!@#")]
        [InlineData("Business", "user@", "Pass123!@#")]
        [InlineData("Business", "@test.com", "Pass123!@#")]
        [InlineData("Business", "user @test.com", "Pass123!@#")] // Space
        public async Task RegisterCommand_WithInvalidEmail_ShouldFail(
            string businessName, string email, string password)
        {
            var cmd = new RegisterTenantCommand(businessName, email, password, "Retail");
            var result = await _registerValidator.ValidateAsync(cmd);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("Business", "user@test.com", "short")] // Too short
        [InlineData("Business", "user@test.com", "noupppercase123!")] // No uppercase
        [InlineData("Business", "user@test.com", "NOLOWERCASE123!")] // No lowercase
        [InlineData("Business", "user@test.com", "NoSpecial123")] // No special char
        [InlineData("Business", "user@test.com", "NoNumber!@#$%")] // No number
        public async Task RegisterCommand_WithWeakPassword_ShouldFail(
            string businessName, string email, string password)
        {
            var cmd = new RegisterTenantCommand(businessName, email, password, "Retail");
            var result = await _registerValidator.ValidateAsync(cmd);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("user@test.com", "password123")]
        [InlineData("admin@company.com", "MySecure@Pass123")]
        public async Task LoginCommand_WithValidCredentials_ShouldValidateSuccessfully(
            string email, string password)
        {
            var cmd = new LoginTenantCommand(email, password);
            var result = await _loginValidator.ValidateAsync(cmd);
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("", "password")]
        [InlineData("invalidemail", "password")]
        [InlineData("user@test.com", "")]
        public async Task LoginCommand_WithInvalidData_ShouldFail(string email, string password)
        {
            var cmd = new LoginTenantCommand(email, password);
            var result = await _loginValidator.ValidateAsync(cmd);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("CurrentPass123!@", "NewPass456!@#")]
        [InlineData("OldSecure@123", "NewSecure@456")]
        public async Task ChangePasswordCommand_WithValidPasswords_ShouldValidateSuccessfully(
            string current, string newPass)
        {
            var cmd = new ChangePasswordCommand(current, newPass);
            var result = await _changePasswordValidator.ValidateAsync(cmd);
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("Current123!@", "weak")] // New password too weak
        [InlineData("Current123!@", "Current123!@")] // Same as current
        [InlineData("", "NewPass123!@")] // Empty current
        [InlineData("Current123!@", "")] // Empty new
        public async Task ChangePasswordCommand_WithInvalidPasswords_ShouldFail(
            string current, string newPass)
        {
            var cmd = new ChangePasswordCommand(current, newPass);
            var result = await _changePasswordValidator.ValidateAsync(cmd);
            Assert.False(result.IsValid);
        }
    }

    public class EmailValidationTests
    {
        [Theory]
        [InlineData("user@example.com")]
        [InlineData("firstname.lastname@example.co.uk")]
        [InlineData("test+tag@domain.org")]
        [InlineData("123@test.com")]
        public void ValidEmails_ShouldCreateSuccessfully(string email)
        {
            var emailVo = BusinessSearcher.Domain.BoundedContext.TenantManagement.ValueObjects.Email.Create(email);
            Assert.Equal(email, emailVo.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("notanemail")]
        [InlineData("user@")]
        [InlineData("@domain.com")]
        [InlineData("user @example.com")] // Space
        public void InvalidEmails_ShouldThrowDomainException(string email)
        {
            Assert.Throws<BusinessSearcher.Domain.Exceptions.DomainException>(() =>
                BusinessSearcher.Domain.BoundedContext.TenantManagement.ValueObjects.Email.Create(email));
        }

        [Fact]
        public void Email_ShouldBeNormalizedToLowercase()
        {
            var email = BusinessSearcher.Domain.BoundedContext.TenantManagement.ValueObjects.Email.Create("User@EXAMPLE.COM");
            Assert.Equal("user@example.com", email.Value);
        }
    }

    public class PasswordValidationTests
    {
        [Theory]
        [InlineData("StrongPass123!@#")]
        [InlineData("Complex@Pass999")]
        [InlineData("MyPassword2024!")]
        public void StrongPasswords_ShouldValidateSuccessfully(string password)
        {
            var validator = new RegisterTenantCommandValidator();
            var cmd = new RegisterTenantCommand("Business", "user@test.com", password, "Retail");
            var result = validator.Validate(cmd);
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("short")] // < 8 chars
        [InlineData("nouppercase123!")] // No uppercase
        [InlineData("NOLOWERCASE123!")] // No lowercase
        [InlineData("NoNumber!@#$")] // No number
        [InlineData("NoSpecial123456")] // No special char
        public void WeakPasswords_ShouldFailValidation(string password)
        {
            var validator = new RegisterTenantCommandValidator();
            var cmd = new RegisterTenantCommand("Business", "user@test.com", password, "Retail");
            var result = validator.Validate(cmd);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }
    }
}
