using Microsoft.AspNetCore.Identity;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly TimeProvider _time;

    public IdentityService(UserManager<ApplicationUser> users, TimeProvider time)
    {
        _users = users;
        _time = time;
    }

    public async Task<AuthenticatedUser> RegisterCustomerAsync(
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };

        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw Translate(created);
        }

        // Only the self-registration role can be granted here; there is no parameter for anything else.
        var roleResult = await _users.AddToRoleAsync(user, Roles.SelfRegistration);
        if (!roleResult.Succeeded)
        {
            // Do not leave an account without a role behind.
            await _users.DeleteAsync(user);
            throw new InvalidOperationException(
                "Could not assign the default role: " + string.Join("; ", roleResult.Errors.Select(e => e.Code)));
        }

        return new AuthenticatedUser(user.Id, user.Email!, user.FullName, [Roles.SelfRegistration]);
    }

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _users.FindByEmailAsync(email);
        if (user is null)
        {
            // Spend roughly the same time as a real check so response time does not reveal which emails exist.
            _users.PasswordHasher.HashPassword(new ApplicationUser(), password);
            return null;
        }

        if (!user.IsActive || await _users.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await _users.CheckPasswordAsync(user, password))
        {
            // Counts towards lockout; AccessFailedAsync locks the account at the configured threshold.
            await _users.AccessFailedAsync(user);
            return null;
        }

        await _users.ResetAccessFailedCountAsync(user);

        var roles = (await _users.GetRolesAsync(user)).ToArray();
        return new AuthenticatedUser(user.Id, user.Email!, user.FullName, roles);
    }

    private static AppException Translate(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return new ConflictException("An account with this email address already exists.");
        }

        var errors = new Dictionary<string, List<string>>();
        foreach (var error in result.Errors)
        {
            var field = error.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password"
                : error.Code.Contains("Email", StringComparison.Ordinal) || error.Code.Contains("UserName", StringComparison.Ordinal) ? "Email"
                : string.Empty;

            if (!errors.TryGetValue(field, out var list))
            {
                list = [];
                errors[field] = list;
            }

            list.Add(error.Description);
        }

        return new ValidationFailedException(errors.ToDictionary(p => p.Key, p => p.Value.ToArray()));
    }
}
