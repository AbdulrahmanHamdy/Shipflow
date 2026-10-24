using ShipFlow.Application.Common.Models;

namespace ShipFlow.Application.Common.Interfaces;

/// <summary>Issues signed access tokens (implemented in Infrastructure).</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(AuthenticatedUser user);
}
