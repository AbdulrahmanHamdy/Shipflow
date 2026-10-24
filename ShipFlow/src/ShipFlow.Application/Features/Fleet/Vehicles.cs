using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Features.Fleet;

/// <summary>Limits that also fit the column precision (decimal(12,3)); an input the database would round is rejected instead.</summary>
public static class CapacityLimits
{
    public const decimal MaxWeightKg = 1_000_000m;
    public const decimal MaxVolumeM3 = 100_000m;
    public const int MaxDecimalPlaces = 3;

    public static bool HasAtMostScale(decimal value) => decimal.Round(value, MaxDecimalPlaces) == value;
}

public sealed record RegisterVehicleCommand(string PlateNumber, VehicleType Type, decimal MaxWeightKg, decimal MaxVolumeM3);

public sealed class RegisterVehicleCommandValidator : AbstractValidator<RegisterVehicleCommand>
{
    public RegisterVehicleCommandValidator()
    {
        RuleFor(x => x.PlateNumber).NotEmpty().MaximumLength(Vehicle.MaxPlateLength);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.MaxWeightKg).GreaterThan(0m).LessThanOrEqualTo(CapacityLimits.MaxWeightKg)
            .Must(CapacityLimits.HasAtMostScale).WithMessage($"At most {CapacityLimits.MaxDecimalPlaces} decimal places.");
        RuleFor(x => x.MaxVolumeM3).GreaterThan(0m).LessThanOrEqualTo(CapacityLimits.MaxVolumeM3)
            .Must(CapacityLimits.HasAtMostScale).WithMessage($"At most {CapacityLimits.MaxDecimalPlaces} decimal places.");
    }
}

/// <summary>Plate and type are identity-like and cannot change; capacity can.</summary>
public sealed record UpdateVehicleCapacityCommand(decimal MaxWeightKg, decimal MaxVolumeM3);

public sealed class UpdateVehicleCapacityCommandValidator : AbstractValidator<UpdateVehicleCapacityCommand>
{
    public UpdateVehicleCapacityCommandValidator()
    {
        RuleFor(x => x.MaxWeightKg).GreaterThan(0m).LessThanOrEqualTo(CapacityLimits.MaxWeightKg)
            .Must(CapacityLimits.HasAtMostScale).WithMessage($"At most {CapacityLimits.MaxDecimalPlaces} decimal places.");
        RuleFor(x => x.MaxVolumeM3).GreaterThan(0m).LessThanOrEqualTo(CapacityLimits.MaxVolumeM3)
            .Must(CapacityLimits.HasAtMostScale).WithMessage($"At most {CapacityLimits.MaxDecimalPlaces} decimal places.");
    }
}

public sealed class RegisterVehicleHandler
{
    private readonly IValidator<RegisterVehicleCommand> _validator;
    private readonly ViewerResolver _viewers;
    private readonly ICompanyStore _companies;
    private readonly IFleetStore _fleet;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterVehicleHandler(
        IValidator<RegisterVehicleCommand> validator, ViewerResolver viewers, ICompanyStore companies,
        IFleetStore fleet, IUnitOfWork unitOfWork)
    {
        _validator = validator;
        _viewers = viewers;
        _companies = companies;
        _fleet = fleet;
        _unitOfWork = unitOfWork;
    }

    /// <summary>The company is the caller's. A duplicate plate within the company answers 409 (unique index).</summary>
    public async Task<VehicleDto> HandleAsync(RegisterVehicleCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.IsCompanyAdmin)
        {
            throw new ForbiddenException("Only a company administrator can register vehicles.");
        }

        var company = await _companies.FindAsync(viewer.CompanyId!.Value, cancellationToken)
            ?? throw new NotFoundException("Company not found.");
        var vehicle = company.RegisterVehicle(
            command.PlateNumber, command.Type, Capacity.Of(command.MaxWeightKg, command.MaxVolumeM3));

        await _fleet.AddVehicleAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return vehicle.ToDto();
    }
}

public sealed class ListVehiclesHandler
{
    private readonly IValidator<ListFleetQuery> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IFleetStore _fleet;

    public ListVehiclesHandler(IValidator<ListFleetQuery> validator, ViewerResolver viewers, IFleetStore fleet)
    {
        _validator = validator;
        _viewers = viewers;
        _fleet = fleet;
    }

    public async Task<PagedResult<VehicleDto>> HandleAsync(ListFleetQuery query, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, query, cancellationToken);
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.CanDispatch)
        {
            throw new ForbiddenException("Only a company administrator or dispatcher can list vehicles.");
        }

        var page = await _fleet.ListVehiclesAsync(viewer.CompanyId!.Value, query.IsActive, query.Page, query.PageSize, cancellationToken);
        return new PagedResult<VehicleDto>(page.Items.Select(v => v.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class GetVehicleHandler
{
    private readonly ViewerResolver _viewers;
    private readonly IFleetStore _fleet;

    public GetVehicleHandler(ViewerResolver viewers, IFleetStore fleet)
    {
        _viewers = viewers;
        _fleet = fleet;
    }

    public async Task<VehicleDto> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.CanDispatch)
        {
            throw new ForbiddenException("Only a company administrator or dispatcher can view vehicles.");
        }

        var vehicle = await _fleet.FindVehicleAsync(viewer.CompanyId!.Value, id, cancellationToken)
            ?? throw new NotFoundException("Vehicle not found.");
        return vehicle.ToDto();
    }
}

/// <summary>Update capacity and activate / deactivate a vehicle. All run under the vehicle's fleet lock.</summary>
public sealed class VehicleManagementHandler
{
    private readonly IValidator<UpdateVehicleCapacityCommand> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IFleetStore _fleet;
    private readonly IAssignmentStore _assignments;
    private readonly IFleetTransaction _transaction;
    private readonly IUnitOfWork _unitOfWork;

    public VehicleManagementHandler(
        IValidator<UpdateVehicleCapacityCommand> validator, ViewerResolver viewers, IFleetStore fleet,
        IAssignmentStore assignments, IFleetTransaction transaction, IUnitOfWork unitOfWork)
    {
        _validator = validator;
        _viewers = viewers;
        _fleet = fleet;
        _assignments = assignments;
        _transaction = transaction;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Changes the capacity. A vehicle that holds an active assignment may only grow: shrinking could make an already
    /// accepted booking impossible to carry, so it is refused (409) until the vehicle is unassigned.
    /// </summary>
    public async Task<VehicleDto> UpdateCapacityAsync(Guid id, UpdateVehicleCapacityCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        return await RunAsync(id, async (vehicle, ct) =>
        {
            var next = Capacity.Of(command.MaxWeightKg, command.MaxVolumeM3);
            var shrinks = next.MaxWeightKg < vehicle.Capacity.MaxWeightKg || next.MaxVolumeM3 < vehicle.Capacity.MaxVolumeM3;
            if (shrinks && await _assignments.HasActiveForVehicleAsync(vehicle.Id, ct))
            {
                throw new ConflictException("The vehicle has an active assignment, so its capacity cannot be reduced. Unassign it first.");
            }

            vehicle.UpdateCapacity(next);
        }, cancellationToken);
    }

    public Task<VehicleDto> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        RunAsync(id, (vehicle, _) =>
        {
            vehicle.Activate();
            return Task.CompletedTask;
        }, cancellationToken);

    /// <summary>Deactivates a vehicle. Refused while the vehicle has an active assignment (unassign first).</summary>
    public Task<VehicleDto> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        RunAsync(id, async (vehicle, ct) =>
        {
            if (await _assignments.HasActiveForVehicleAsync(vehicle.Id, ct))
            {
                throw new ConflictException("The vehicle has an active assignment. Unassign it first.");
            }

            vehicle.Deactivate();
        }, cancellationToken);

    private async Task<VehicleDto> RunAsync(
        Guid id, Func<Vehicle, CancellationToken, Task> change, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.IsCompanyAdmin)
        {
            throw new ForbiddenException("Only a company administrator can change vehicles.");
        }

        var companyId = viewer.CompanyId!.Value;
        if (await _fleet.FindVehicleAsync(companyId, id, cancellationToken) is null)
        {
            throw new NotFoundException("Vehicle not found.");
        }

        return await _transaction.RunAsync(FleetLocks.ForVehicle(id), async ct =>
        {
            var vehicle = await _fleet.FindVehicleForUpdateAsync(companyId, id, ct) ?? throw new NotFoundException("Vehicle not found.");
            await change(vehicle, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return vehicle.ToDto();
        }, cancellationToken);
    }
}
