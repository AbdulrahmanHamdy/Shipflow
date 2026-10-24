using ShipFlow.Application.Common.Models;

namespace ShipFlow.Application.Features.Auth;

public sealed record UserDto(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles)
{
    public static UserDto From(AuthenticatedUser user) => new(user.Id, user.Email, user.FullName, user.Roles);
}

public sealed record LoginResult(string AccessToken, string TokenType, DateTime ExpiresAtUtc, UserDto User);
