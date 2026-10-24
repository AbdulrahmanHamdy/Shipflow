using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Companies;

namespace ShipFlow.API.Controllers;

/// <summary>
/// Company onboarding and membership. Every route is relative to the caller's OWN company (<c>/me</c>), which is resolved
/// from the database; there is no company id in any request. Identity roles follow memberships (D-023): a user who creates a
/// company or is added as a member must sign in again to receive the new role in a token.
/// </summary>
[ApiController]
[Route("api/companies")]
[Produces("application/json")]
public sealed class CompaniesController : ControllerBase
{
    /// <summary>Creates a company; the caller becomes its CompanyAdmin. 409 when the caller already belongs to a company.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireCustomer)]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyCommand command, [FromServices] CreateCompanyHandler handler, CancellationToken ct)
    {
        var company = await handler.HandleAsync(command, ct);
        return Created("/api/companies/me", company);
    }

    /// <summary>The caller's company and the caller's role in it.</summary>
    [HttpGet("me")]
    [Authorize(Policy = Policies.RequireCompanyStaff)]
    public async Task<ActionResult<CompanyDto>> GetMine([FromServices] GetMyCompanyHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(ct));

    [HttpGet("me/members")]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> GetMembers(
        [FromQuery] GetMembersQuery query, [FromServices] GetMembersHandler handler, CancellationToken ct) =>
        Ok(await handler.HandleAsync(query, ct));

    /// <summary>Adds an existing account (by email) as Dispatcher or Driver.</summary>
    [HttpPost("me/members")]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMember(
        [FromBody] AddMemberCommand command, [FromServices] AddMemberHandler handler, CancellationToken ct)
    {
        var member = await handler.HandleAsync(command, ct);
        return Created("/api/companies/me/members", member);
    }

    /// <summary>Ends a membership, revokes the role and deactivates the member's driver profile, in one commit.</summary>
    [HttpDelete("me/members/{userId:guid}")]
    [Authorize(Policy = Policies.RequireCompanyAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(
        Guid userId, [FromServices] RemoveMemberHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(userId, ct);
        return NoContent();
    }
}
