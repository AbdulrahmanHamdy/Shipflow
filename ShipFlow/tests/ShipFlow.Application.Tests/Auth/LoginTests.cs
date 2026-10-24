using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Auth.Login;

namespace ShipFlow.Application.Tests.Auth;

public class LoginValidatorTests
{
    private static readonly LoginCommandValidator Validator = new();

    [Theory]
    [InlineData("", "x")]
    [InlineData("a@b.com", "")]
    [InlineData("", "")]
    public void MissingField_Fails(string email, string password) =>
        Assert.False(Validator.Validate(new LoginCommand(email, password)).IsValid);

    [Fact]
    public void WeakButPresentPassword_IsNotRejectedByValidation() =>
        // Password rules are for registration. At login, "wrong" and "weak" must look the same to the caller.
        Assert.True(Validator.Validate(new LoginCommand("a@b.com", "short")).IsValid);

    [Fact]
    public void OversizedPassword_Fails() =>
        Assert.False(Validator.Validate(new LoginCommand("a@b.com", new string('x', PasswordPolicy.MaxLength + 1))).IsValid);
}

public class LoginHandlerTests
{
    private static readonly AuthenticatedUser Ada = new(Guid.NewGuid(), "ada@example.com", "Ada", [Roles.Customer]);

    private static LoginHandler Create(FakeIdentityService identity, FakeTokenService tokens) =>
        new(new LoginCommandValidator(), identity, tokens);

    [Fact]
    public async Task ValidCredentials_ReturnsBearerToken_WithUser()
    {
        var identity = new FakeIdentityService { LoginResult = Ada };
        var tokens = new FakeTokenService();

        var result = await Create(identity, tokens).HandleAsync(new LoginCommand(" ada@example.com ", "pw"), CancellationToken.None);

        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("token-for-" + Ada.Id, result.AccessToken);
        Assert.Equal(FakeTokenService.Expiry, result.ExpiresAtUtc);
        Assert.Equal(Ada.Id, result.User.Id);
        Assert.Equal("ada@example.com", Assert.Single(identity.LoginAttempts).Email);
    }

    [Fact]
    public async Task InvalidCredentials_ThrowAuthenticationFailed_AndIssueNoToken()
    {
        var identity = new FakeIdentityService { LoginResult = null };
        var tokens = new FakeTokenService();

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            Create(identity, tokens).HandleAsync(new LoginCommand("ada@example.com", "wrong"), CancellationToken.None));

        Assert.Equal("Invalid email or password.", ex.Message);
        Assert.Null(tokens.LastUser);
    }

    [Fact]
    public async Task MissingFields_ThrowValidationFailed_AndNeverReachIdentity()
    {
        var identity = new FakeIdentityService { LoginResult = Ada };

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Create(identity, new FakeTokenService()).HandleAsync(new LoginCommand("", ""), CancellationToken.None));

        Assert.Empty(identity.LoginAttempts);
    }
}
