using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.Infrastructure.Identity;

public sealed class JwtTokenService : ITokenService
{
    private readonly IOptions<JwtOptions> _options;
    private readonly TimeProvider _time;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider time)
    {
        _options = options;
        _time = time;
    }

    public AccessToken CreateAccessToken(AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var options = _options.Value;
        var now = _time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, user.Id.ToString()),
            new(ClaimNames.Email, user.Email),
            new(ClaimNames.Name, user.FullName),
            new(ClaimNames.TokenId, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimNames.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
