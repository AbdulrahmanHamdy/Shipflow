namespace ShipFlow.Application.Common.Security;

/// <summary>
/// Claim names used in ShipFlow access tokens. Raw JWT names are used on purpose: inbound claim mapping is
/// disabled so the token's own names are what code sees.
/// </summary>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string TokenId = "jti";
}
