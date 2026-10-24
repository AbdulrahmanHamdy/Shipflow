using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipFlow.API.Configuration;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Features.Auth;
using ShipFlow.Application.Features.Auth.Login;
using ShipFlow.Application.Features.Auth.Register;

namespace ShipFlow.API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Creates a Customer account. Roles cannot be requested; any extra fields in the body are ignored.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        [FromServices] RegisterHandler handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(command, cancellationToken);
        return Created("/api/auth/me", user);
    }

    /// <summary>Exchanges credentials for a short-lived bearer access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResult>> Login(
        [FromBody] LoginCommand command,
        [FromServices] LoginHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(command, cancellationToken));

    /// <summary>The caller's identity as established by the token (no database call).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> Me([FromServices] ICurrentUser currentUser) =>
        Ok(new CurrentUserResponse(currentUser.UserId, currentUser.Email, currentUser.Roles));
}

public sealed record CurrentUserResponse(Guid? Id, string? Email, IReadOnlyCollection<string> Roles);
