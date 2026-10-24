using System.Security.Claims;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.API.Auth;

/// <summary>Reads the caller's identity from the validated access token on the current request.</summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimNames.Subject), out var id) && id != Guid.Empty ? id : null;

    public string? Email => Principal?.FindFirstValue(ClaimNames.Email);

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimNames.Role).Select(c => c.Value).ToArray() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
