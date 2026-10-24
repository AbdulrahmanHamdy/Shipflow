namespace ShipFlow.Application.Common.Security;

/// <summary>
/// Single source of truth for password rules. Request validation and the Identity options both read it,
/// so a password accepted by one is never rejected by the other for a rule listed here.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;
    public const int MaxLength = 128;
    public const bool RequireDigit = true;
    public const bool RequireLowercase = true;
    public const bool RequireUppercase = true;
    public const bool RequireNonAlphanumeric = false;
}
