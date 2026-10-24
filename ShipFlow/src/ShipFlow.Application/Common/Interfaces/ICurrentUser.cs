namespace ShipFlow.Application.Common.Interfaces;

/// <summary>
/// The caller as established by the validated access token. Identity only: company membership and other
/// resource ownership are always resolved from the database, never from client-supplied ids.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
}
