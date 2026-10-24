using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Auth.Register;

namespace ShipFlow.Application.Tests.Auth;

public class RegisterValidatorTests
{
    private const string GoodPassword = "Str0ngPassw0rd";

    private static readonly RegisterCommandValidator Validator = new();

    private static RegisterCommand Valid() => new("ada@example.com", GoodPassword, "Ada Lovelace");

    [Fact]
    public void ValidCommand_Passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void BadEmail_Fails(string email) =>
        Assert.False(Validator.Validate(Valid() with { Email = email }).IsValid);

    [Fact]
    public void EmailLongerThanLimit_Fails()
    {
        var email = new string('a', RegisterCommandValidator.MaxEmailLength) + "@example.com";

        Assert.False(Validator.Validate(Valid() with { Email = email }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sh0rtPw")]          // shorter than the minimum length
    [InlineData("alllowercase123")]  // no uppercase letter
    [InlineData("ALLUPPERCASE123")]  // no lowercase letter
    [InlineData("NoDigitsInHereAtAll")]
    public void WeakPassword_Fails(string password) =>
        Assert.False(Validator.Validate(Valid() with { Password = password }).IsValid);

    [Fact]
    public void PasswordAtExactMinimumLength_Passes()
    {
        var password = "Aa1" + new string('x', PasswordPolicy.MinLength - 3);

        Assert.True(Validator.Validate(Valid() with { Password = password }).IsValid);
    }

    [Fact]
    public void PasswordAboveMaximumLength_Fails()
    {
        var password = "Aa1" + new string('x', PasswordPolicy.MaxLength);

        Assert.False(Validator.Validate(Valid() with { Password = password }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankFullName_Fails(string name) =>
        Assert.False(Validator.Validate(Valid() with { FullName = name }).IsValid);

    [Fact]
    public void FullNameLongerThanLimit_Fails() =>
        Assert.False(Validator.Validate(Valid() with { FullName = new string('n', RegisterCommandValidator.MaxFullNameLength + 1) }).IsValid);

    [Fact]
    public void RegisterCommand_HasNoRoleOrPrivilegeMember()
    {
        // Public registration must not be able to name a role: if someone adds such a member, this fails.
        var members = typeof(RegisterCommand)
            .GetProperties()
            .Select(p => p.Name)
            .Where(n => n != "EqualityContract")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Email", "FullName", "Password" }, members);
    }
}

public class RegisterHandlerTests
{
    private static RegisterHandler Create(FakeIdentityService identity) =>
        new(new RegisterCommandValidator(), identity);

    [Fact]
    public async Task ValidCommand_RegistersACustomerWithTrimmedValues()
    {
        var identity = new FakeIdentityService();

        var user = await Create(identity).HandleAsync(
            new RegisterCommand("  ada@example.com ", "Str0ngPassw0rd", " Ada Lovelace "),
            CancellationToken.None);

        var call = Assert.Single(identity.Registered);
        Assert.Equal("ada@example.com", call.Email);
        Assert.Equal("Ada Lovelace", call.FullName);
        Assert.Equal(new[] { Roles.Customer }, user.Roles.ToArray());
    }

    [Fact]
    public async Task InvalidCommand_ThrowsValidationFailed_WithFieldMessages_AndNeverReachesIdentity()
    {
        var identity = new FakeIdentityService();

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Create(identity).HandleAsync(new RegisterCommand("nope", "weak", ""), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("Email"));
        Assert.True(ex.Errors.ContainsKey("Password"));
        Assert.True(ex.Errors.ContainsKey("FullName"));
        Assert.Empty(identity.Registered);
    }

    [Fact]
    public async Task DuplicateEmail_PropagatesConflict()
    {
        var identity = new FakeIdentityService { EmailTaken = true };

        await Assert.ThrowsAsync<ConflictException>(() =>
            Create(identity).HandleAsync(new RegisterCommand("ada@example.com", "Str0ngPassw0rd", "Ada"), CancellationToken.None));
    }
}

public class RolesTests
{
    [Fact]
    public void AllRoles_AreTheFiveDefinedRoles() =>
        Assert.Equal(
            new[] { "CompanyAdmin", "Customer", "Dispatcher", "Driver", "PlatformAdmin" },
            Roles.All.OrderBy(r => r, StringComparer.Ordinal).ToArray());

    [Fact]
    public void SelfRegistration_GrantsOnlyCustomer() => Assert.Equal(Roles.Customer, Roles.SelfRegistration);
}
