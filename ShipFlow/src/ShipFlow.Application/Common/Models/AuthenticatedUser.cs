namespace ShipFlow.Application.Common.Models;

/// <summary>An authenticated platform user as the application layer sees it (no Identity types leak out).</summary>
public sealed record AuthenticatedUser(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles);

/// <summary>A signed access token and when it stops being valid.</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
