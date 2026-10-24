using ShipFlow.Application.Common.Models;

namespace ShipFlow.Application.Common.Interfaces;

/// <summary>User account operations backed by ASP.NET Core Identity (implemented in Infrastructure).</summary>
public interface IIdentityService
{
    /// <summary>
    /// Creates a new account with the Customer role only. Throws <c>ConflictException</c> when the email is
    /// taken and <c>ValidationFailedException</c> when Identity rejects the password.
    /// </summary>
    Task<AuthenticatedUser> RegisterCustomerAsync(
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the user when the credentials are valid and the account may sign in; otherwise null.
    /// Failed attempts count towards account lockout. The caller must not reveal which check failed.
    /// </summary>
    Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken);
}
