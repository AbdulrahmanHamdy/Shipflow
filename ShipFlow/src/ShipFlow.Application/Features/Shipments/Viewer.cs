using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Shipments;

namespace ShipFlow.Application.Features.Shipments;

/// <summary>
/// The caller, with everything authorization needs resolved server-side: user id from the token, company and company
/// role from the database. A company id supplied by the client is never used.
/// </summary>
public sealed record Viewer(Guid UserId, bool IsPlatformAdmin, Guid? CompanyId, CompanyRole? Role)
{
    public bool CanManageQuotes => CompanyId is not null && Role is CompanyRole.Admin or CompanyRole.Dispatcher;

    /// <summary>Assign/unassign and look at availability: the same people who may quote (admin or dispatcher of an active company).</summary>
    public bool CanDispatch => CanManageQuotes;

    /// <summary>Manages members and the fleet of their own company.</summary>
    public bool IsCompanyAdmin => CompanyId is not null && Role is CompanyRole.Admin;

    /// <summary>Owner, platform admin, the carrier company, or any active company while the shipment is open for quotes.</summary>
    public bool CanView(Shipment s) =>
        IsPlatformAdmin
        || s.IsOwnedBy(UserId)
        || (CompanyId is not null && (s.CarrierCompanyId == CompanyId || s.Status == ShipmentStatus.Quoting));

    public bool SeesAllQuotes(Shipment s) => IsPlatformAdmin || s.IsOwnedBy(UserId);

    public bool IsCarrierOf(Shipment s) => CompanyId is not null && s.CarrierCompanyId == CompanyId;
}

public sealed class ViewerResolver
{
    private readonly ICurrentUser _user;
    private readonly ICompanyAccess _companies;

    public ViewerResolver(ICurrentUser user, ICompanyAccess companies)
    {
        _user = user;
        _companies = companies;
    }

    public async Task<Viewer> ResolveAsync(CancellationToken cancellationToken)
    {
        if (!_user.IsAuthenticated || _user.UserId is not { } userId)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        // Company membership comes from the database on every call: a removed member loses access at once,
        // even while their token is still valid.
        var membership = await _companies.FindActiveMembershipAsync(userId, cancellationToken);
        return new Viewer(userId, _user.IsInRole(Roles.PlatformAdmin), membership?.CompanyId, membership?.Role);
    }
}
