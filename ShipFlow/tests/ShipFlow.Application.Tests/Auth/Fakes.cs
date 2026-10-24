using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.Application.Tests.Auth;

internal sealed class FakeIdentityService : IIdentityService
{
    public List<(string Email, string Password, string FullName)> Registered { get; } = [];

    public List<(string Email, string Password)> LoginAttempts { get; } = [];

    public bool EmailTaken { get; set; }

    public AuthenticatedUser? LoginResult { get; set; }

    public Task<AuthenticatedUser> RegisterCustomerAsync(string email, string password, string fullName, CancellationToken cancellationToken)
    {
        if (EmailTaken)
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        Registered.Add((email, password, fullName));
        return Task.FromResult(new AuthenticatedUser(Guid.NewGuid(), email, fullName, [Roles.SelfRegistration]));
    }

    public Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        LoginAttempts.Add((email, password));
        return Task.FromResult(LoginResult);
    }
}

internal sealed class FakeTokenService : ITokenService
{
    public static readonly DateTime Expiry = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public AuthenticatedUser? LastUser { get; private set; }

    public AccessToken CreateAccessToken(AuthenticatedUser user)
    {
        LastUser = user;
        return new AccessToken("token-for-" + user.Id, Expiry);
    }
}
