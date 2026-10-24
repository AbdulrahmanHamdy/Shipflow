using System.Text;

namespace ShipFlow.Infrastructure.Identity;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section. The signing key is a secret: supply it through user-secrets,
/// environment variables or a secret store, never through a committed file.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs at least 256 bits of key material.</summary>
    public const int MinSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int ClockSkewSeconds { get; set; } = 30;

    public static bool IsValid(JwtOptions options) =>
        !string.IsNullOrWhiteSpace(options.Issuer)
        && !string.IsNullOrWhiteSpace(options.Audience)
        && Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) >= MinSigningKeyBytes
        && options.AccessTokenMinutes is > 0 and <= 24 * 60
        && options.ClockSkewSeconds is >= 0 and <= 300;
}
