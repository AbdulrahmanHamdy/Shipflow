using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Fleet;

namespace ShipFlow.Application.Features.Fleet;

public sealed class ListFleetQueryValidator : AbstractValidator<ListFleetQuery>
{
    public const int MaxPageSize = 100;

    public ListFleetQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}

/// <summary>The user must already be an active Driver member of the caller's company (checked from the database).</summary>
public sealed record RegisterDriverCommand(Guid UserId, string FullName, string LicenseNumber);

public sealed class RegisterDriverCommandValidator : AbstractValidator<RegisterDriverCommand>
{
    public RegisterDriverCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(Driver.MaxNameLength);
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(Driver.MaxLicenseLength);
    }
}

public sealed record UpdateDriverCommand(string FullName, string LicenseNumber);

public sealed class UpdateDriverCommandValidator : AbstractValidator<UpdateDriverCommand>
{
    public UpdateDriverCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(Driver.MaxNameLength);
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(Driver.MaxLicenseLength);
    }
}

public sealed class RegisterDriverHandler
{
    private readonly IValidator<RegisterDriverCommand> _validator;
    private readonly ViewerResolver _viewers;
    private readonly ICompanyStore _companies;
    private readonly IFleetStore _fleet;
    private readonly IFleetTransaction _transaction;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDriverHandler(
        IValidator<RegisterDriverCommand> validator, ViewerResolver viewers, ICompanyStore companies,
        IFleetStore fleet, IFleetTransaction transaction, IUnitOfWork unitOfWork)
    {
        _validator = validator;
        _viewers = viewers;
        _companies = companies;
        _fleet = fleet;
        _transaction = transaction;
        _unitOfWork = unitOfWork;
    }

    public async Task<DriverDto> HandleAsync(RegisterDriverCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.IsCompanyAdmin)
        {
            throw new ForbiddenException("Only a company administrator can register drivers.");
        }

        var companyId = viewer.CompanyId!.Value;

        // Locking the member serializes this with removing that member: a driver profile can never be created for
        // a membership that is being ended at the same moment.
        return await _transaction.RunAsync(FleetLocks.ForMember(command.UserId), async ct =>
        {
            var company = await _companies.FindAsync(companyId, ct) ?? throw new NotFoundException("Company not found.");

            // The user id comes from the client, but it only ever resolves inside the caller's own company.
            if (!company.HasActiveMember(command.UserId, CompanyRole.Driver))
            {
                throw new NotFoundException("No active Driver member with that user id in your company.");
            }

            var driver = company.RegisterDriver(command.UserId, command.FullName, command.LicenseNumber);
            await _fleet.AddDriverAsync(driver, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return driver.ToDto();
        }, cancellationToken);
    }
}

public sealed class ListDriversHandler
{
    private readonly IValidator<ListFleetQuery> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IFleetStore _fleet;

    public ListDriversHandler(IValidator<ListFleetQuery> validator, ViewerResolver viewers, IFleetStore fleet)
    {
        _validator = validator;
        _viewers = viewers;
        _fleet = fleet;
    }

    public async Task<PagedResult<DriverDto>> HandleAsync(ListFleetQuery query, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, query, cancellationToken);
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.CanDispatch)
        {
            throw new ForbiddenException("Only a company administrator or dispatcher can list drivers.");
        }

        var page = await _fleet.ListDriversAsync(viewer.CompanyId!.Value, query.IsActive, query.Page, query.PageSize, cancellationToken);
        return new PagedResult<DriverDto>(page.Items.Select(d => d.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class GetDriverHandler
{
    private readonly ViewerResolver _viewers;
    private readonly IFleetStore _fleet;

    public GetDriverHandler(ViewerResolver viewers, IFleetStore fleet)
    {
        _viewers = viewers;
        _fleet = fleet;
    }

    public async Task<DriverDto> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.CanDispatch)
        {
            throw new ForbiddenException("Only a company administrator or dispatcher can view drivers.");
        }

        // Scoped by the caller's company: another company's driver is exactly as "not found" as a missing one.
        var driver = await _fleet.FindDriverAsync(viewer.CompanyId!.Value, id, cancellationToken)
            ?? throw new NotFoundException("Driver not found.");
        return driver.ToDto();
    }
}

public sealed class UpdateDriverHandler
{
    private readonly IValidator<UpdateDriverCommand> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IFleetStore _fleet;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDriverHandler(
        IValidator<UpdateDriverCommand> validator, ViewerResolver viewers, IFleetStore fleet, IUnitOfWork unitOfWork)
    {
        _validator = validator;
        _viewers = viewers;
        _fleet = fleet;
        _unitOfWork = unitOfWork;
    }

    public async Task<DriverDto> HandleAsync(Guid id, UpdateDriverCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.IsCompanyAdmin)
        {
            throw new ForbiddenException("Only a company administrator can update drivers.");
        }

        var driver = await _fleet.FindDriverForUpdateAsync(viewer.CompanyId!.Value, id, cancellationToken)
            ?? throw new NotFoundException("Driver not found.");
        driver.UpdateDetails(command.FullName, command.LicenseNumber);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return driver.ToDto();
    }
}

/// <summary>Activate / deactivate a driver. Both run under the driver's fleet lock (see <see cref="IFleetTransaction"/>).</summary>
public sealed class DriverActivationHandler
{
    private readonly ViewerResolver _viewers;
    private readonly ICompanyStore _companies;
    private readonly IFleetStore _fleet;
    private readonly IAssignmentStore _assignments;
    private readonly IFleetTransaction _transaction;
    private readonly IUnitOfWork _unitOfWork;

    public DriverActivationHandler(
        ViewerResolver viewers, ICompanyStore companies, IFleetStore fleet, IAssignmentStore assignments,
        IFleetTransaction transaction, IUnitOfWork unitOfWork)
    {
        _viewers = viewers;
        _companies = companies;
        _fleet = fleet;
        _assignments = assignments;
        _transaction = transaction;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Re-activates a driver, but only while their user is still an active Driver member (domain rule). Serialized with
    /// member removal, so a driver can never end up active without a membership.
    /// </summary>
    public Task<DriverDto> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        RunAsync(id, async (companyId, driver, ct) =>
        {
            var company = await _companies.FindAsync(companyId, ct) ?? throw new NotFoundException("Company not found.");
            company.ActivateDriver(driver);
        }, cancellationToken);

    /// <summary>Deactivates a driver. Refused while the driver has an active assignment (unassign first).</summary>
    public Task<DriverDto> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        RunAsync(id, async (_, driver, ct) =>
        {
            if (await _assignments.HasActiveForDriverAsync(driver.Id, ct))
            {
                throw new ConflictException("The driver has an active assignment. Unassign it first.");
            }

            driver.Deactivate();
        }, cancellationToken);

    private async Task<DriverDto> RunAsync(
        Guid id, Func<Guid, Driver, CancellationToken, Task> change, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.IsCompanyAdmin)
        {
            throw new ForbiddenException("Only a company administrator can activate or deactivate drivers.");
        }

        var companyId = viewer.CompanyId!.Value;
        if (await _fleet.FindDriverAsync(companyId, id, cancellationToken) is null)
        {
            // Checked before locking so another tenant's id never takes a lock.
            throw new NotFoundException("Driver not found.");
        }

        return await _transaction.RunAsync(FleetLocks.ForDriver(id), async ct =>
        {
            var driver = await _fleet.FindDriverForUpdateAsync(companyId, id, ct) ?? throw new NotFoundException("Driver not found.");
            await change(companyId, driver, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return driver.ToDto();
        }, cancellationToken);
    }
}
