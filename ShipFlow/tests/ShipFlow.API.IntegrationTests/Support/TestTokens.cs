using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.API.IntegrationTests.Support;

/// <summary>Builds access tokens by hand so tests can produce valid and deliberately invalid ones.</summary>
public static class TestTokens
{
    public static string Create(
        string[] roles,
        string? signingKey = null,
        string? issuer = null,
        string? audience = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        Guid? userId = null,
        string email = "user@example.com")
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, (userId ?? Guid.NewGuid()).ToString()),
            new(ClaimNames.Email, email)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimNames.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? ShipFlowApiFactory.SigningKey));
        var token = new JwtSecurityToken(
            issuer ?? ShipFlowApiFactory.Issuer,
            audience ?? ShipFlowApiFactory.Audience,
            claims,
            notBefore ?? now.AddMinutes(-1),
            expires ?? now.AddMinutes(10),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string Unsigned(string[] roles)
    {
        var header = Base64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64Url(
            "{\"sub\":\"" + Guid.NewGuid() + "\",\"role\":\"" + roles[0] + "\",\"iss\":\"" + ShipFlowApiFactory.Issuer
            + "\",\"aud\":\"" + ShipFlowApiFactory.Audience + "\",\"exp\":" + DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() + "}");
        return header + "." + payload + ".";
    }

    private static string Base64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
