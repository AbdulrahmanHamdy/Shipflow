using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Companies;

namespace ShipFlow.Application.Features.Companies;

/// <summary>The caller becomes the company's administrator; there is no user, role or company id in the request.</summary>
public sealed record CreateCompanyCommand(string Name);

public sealed class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator() =>
        RuleFor(x => x.Name).NotEmpty().MaximumLength(TransportCompany.MaxNameLength);
}

public sealed class CreateCompanyHandler
{
    private readonly IValidator<CreateCompanyCommand> _validator;
    private readonly ICurrentUser _user;
    private readonly IUserDirectory _users;
    private readonly ICompanyAccess _access;
    private readonly ICompanyStore _companies;
    private readonly IMembershipRoles _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public CreateCompanyHandler(
        IValidator<CreateCompanyCommand> validator, ICurrentUser user, IUserDirectory users, ICompanyAccess access,
        ICompanyStore companies, IMembershipRoles roles, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _validator = validator;
        _user = user;
        _users = users;
        _access = access;
        _companies = companies;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    /// <summary>
    /// Creates the company, the caller's Admin membership and the matching <c>CompanyAdmin</c> Identity role in one commit.
    /// The role claim only appears in tokens issued after this call, so the caller signs in again (D-023).
    /// </summary>
    public async Task<CompanyDto> HandleAsync(CreateCompanyCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var userId = _user.UserId ?? throw new ForbiddenException("Authentication is required.");

        // A deactivated account may still hold a valid token for a few minutes; it cannot found a company.
        if (await _users.FindActiveByIdAsync(userId, cancellationToken) is null)
        {
            throw new ForbiddenException("This account is not active.");
        }

        // One active membership per user across all companies (D-024). The unique index is the race backstop.
        if (await _access.FindActiveMembershipAsync(userId, cancellationToken) is not null)
        {
            throw new ConflictException("You already belong to a transport company.");
        }

        var company = TransportCompany.Create(command.Name, userId, _time.GetUtcNow().UtcDateTime);
        await _companies.AddAsync(company, cancellationToken);
        await _roles.StageGrantAsync(userId, CompanyRole.Admin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return company.ToDto(CompanyRole.Admin);
    }
}

public sealed class GetMyCompanyHandler
{
    private readonly ViewerResolver _viewers;
    private readonly ICompanyStore _companies;

    public GetMyCompanyHandler(ViewerResolver viewers, ICompanyStore companies)
    {
        _viewers = viewers;
        _companies = companies;
    }

    public async Task<CompanyDto> HandleAsync(CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (viewer.CompanyId is not { } companyId || viewer.Role is not { } role)
        {
            throw new ForbiddenException("You do not belong to a transport company.");
        }

        var company = await _companies.FindAsync(companyId, cancellationToken) ?? throw new NotFoundException("Company not found.");
        return company.ToDto(role);
    }
}

public sealed record GetMembersQuery(bool IncludeFormer = false);

public sealed class GetMembersHandler
{
    private readonly ViewerResolver _viewers;
    private readonly ICompanyStore _companies;

    public GetMembersHandler(ViewerResolver viewers, ICompanyStore companies)
    {
        _viewers = viewers;
        _companies = companies;
    }

    public async Task<IReadOnlyList<MemberDto>> HandleAsync(GetMembersQuery query, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.IsCompanyAdmin)
        {
            throw new ForbiddenException("Only a company administrator can list members.");
        }

        return await _companies.ListMembersAsync(viewer.CompanyId!.Value, query.IncludeFormer, cancellationToken);
    }
}

/// <summary>Only Dispatcher and Driver members can be added through the API; the company's admin is its creator.</summary>
public sealed record AddMemberCommand(string Email, CompanyRole Role);

public sealed class AddMemberCommandValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Role).IsInEnum()
            .Must(r => r is CompanyRole.Dispatcher or CompanyRole.Driver)
            .WithMessage("Only the Dispatcher and Driver roles can be granted to members.");
    }
}

public sealed class AddMemberHandler
{
    private readonly IValidator<AddMemberCommand> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IUserDirectory _users;
    private readonly ICompanyAccess _access;
    private readonly ICompanyStore _companies;
    private readonly IMembershipRoles _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public AddMemberHandler(
        IValidator<AddMemberCommand> validator, ViewerResolver viewers, IUserDirectory users, ICompanyAccess access,
        ICompanyStore companies, IMembershipRoles roles, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _validator = validator;
        _viewers = viewers;
        _users = users;
        _access = access;
        _companies = companies;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    /// <summary>
    /// Adds an existing account to the caller's company. The company comes from the caller's own membership. The membership and
    /// the matching Identity role are committed together; the new member must sign in again to get the role in a token.
    /// </summary>
    public async Task<MemberDto> HandleAsync(AddMemberCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.IsCompanyAdmin)
        {
            throw new ForbiddenException("Only a company administrator can add members.");
        }

        // Admin-only, so this reveals whether an email is registered only to company administrators.
        var target = await _users.FindActiveByEmailAsync(command.Email.Trim(), cancellationToken)
            ?? throw new NotFoundException("No active account with that email address.");

        if (await _access.FindActiveMembershipAsync(target.Id, cancellationToken) is not null)
        {
            throw new ConflictException("That user already belongs to a transport company.");
        }

        var company = await _companies.FindForUpdateAsync(viewer.CompanyId!.Value, cancellationToken)
            ?? throw new NotFoundException("Company not found.");

        var membership = company.AddMember(target.Id, command.Role, _time.GetUtcNow().UtcDateTime);
        await _roles.StageGrantAsync(target.Id, command.Role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MemberDto(
            target.Id, target.Email, target.FullName, membership.Role.ToString(), membership.IsActive,
            membership.JoinedAtUtc, membership.LeftAtUtc);
    }
}

public sealed class RemoveMemberHandler
{
    private readonly ViewerResolver _viewers;
    private readonly ICompanyStore _companies;
    private readonly IFleetStore _fleet;
    private readonly IAssignmentStore _assignments;
    private readonly IMembershipRoles _roles;
    private readonly IFleetTransaction _transaction;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public RemoveMemberHandler(
        ViewerResolver viewers, ICompanyStore companies, IFleetStore fleet, IAssignmentStore assignments,
        IMembershipRoles roles, IFleetTransaction transaction, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _viewers = viewers;
        _companies = companies;
        _fleet = fleet;
        _assignments = assignments;
        _roles = roles;
        _transaction = transaction;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    /// <summary>
    /// Ends a membership. In ONE commit: the membership is closed, the Identity role is revoked and the member's driver
    /// profile (if any) is deactivated. A driver with an active assignment cannot be removed (unassign first), so a
    /// removed member never keeps a booking. Serialized with assigning/activating that driver by the fleet locks.
    /// </summary>
    public async Task HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.IsCompanyAdmin)
        {
            throw new ForbiddenException("Only a company administrator can remove members.");
        }

        var companyId = viewer.CompanyId!.Value;
        var driverId = (await _fleet.FindDriverByUserAsync(companyId, userId, cancellationToken))?.Id;

        var locks = new FleetLocks(driverId is null ? [] : [driverId.Value], [], [userId]);
        await _transaction.RunAsync(locks, async ct =>
        {
            var company = await _companies.FindForUpdateAsync(companyId, ct) ?? throw new NotFoundException("Company not found.");
            var membership = company.Memberships.FirstOrDefault(m => m.IsActive && m.UserId == userId)
                ?? throw new NotFoundException("Member not found.");

            // Domain rule: a company keeps at least one active administrator.
            company.RemoveMember(userId, _time.GetUtcNow().UtcDateTime);

            if (driverId is { } id && await _fleet.FindDriverForUpdateAsync(companyId, id, ct) is { } driver)
            {
                if (await _assignments.HasActiveForDriverAsync(driver.Id, ct))
                {
                    throw new ConflictException("The member's driver profile still has an active assignment. Unassign it first.");
                }

                driver.Deactivate();
            }

            await _roles.StageRevokeAsync(userId, membership.Role, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }
}
