using ShipFlow.Domain.Common;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Dispatching;

public enum AssignmentStatus
{
    Active = 0,
    Released = 1,
    Completed = 2
}

public enum ConflictResource
{
    Driver = 0,
    Vehicle = 1
}

/// <summary>An existing active assignment that overlaps a requested one for the same driver or vehicle.</summary>
public sealed record AssignmentConflict(Guid AssignmentId, ConflictResource Resource);

/// <summary>Raised when a requested assignment overlaps an existing active assignment.</summary>
public sealed class AssignmentConflictException : DomainException
{
    public AssignmentConflictException(IReadOnlyList<AssignmentConflict> conflicts)
        : base(DomainErrorCodes.Conflict, BuildMessage(conflicts))
    {
        Conflicts = conflicts;
    }

    public IReadOnlyList<AssignmentConflict> Conflicts { get; }

    private static string BuildMessage(IReadOnlyList<AssignmentConflict> conflicts)
    {
        var resources = conflicts.Select(c => c.Resource).Distinct().OrderBy(r => r);
        return $"The requested window conflicts with an existing assignment for: {string.Join(", ", resources)}.";
    }
}

/// <summary>
/// Books one driver and one vehicle of the carrier company for a shipment during a time window.
/// Availability is derived from assignment windows (there is no IsAvailable flag).
/// </summary>
public sealed class Assignment : Entity
{
    public const int MaxReasonLength = 500;

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private Assignment()
    {
        Window = null!;
    }

    private Assignment(
        Guid shipmentId,
        Guid companyId,
        Guid driverId,
        Guid vehicleId,
        TimeWindow window,
        Guid assignedByUserId,
        DateTime nowUtc)
        : base(Guid.NewGuid())
    {
        ShipmentId = shipmentId;
        CompanyId = companyId;
        DriverId = driverId;
        VehicleId = vehicleId;
        Window = window;
        AssignedByUserId = Guard.NotEmpty(assignedByUserId, nameof(assignedByUserId));
        CreatedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
        Status = AssignmentStatus.Active;
    }

    public Guid ShipmentId { get; }

    public Guid CompanyId { get; }

    public Guid DriverId { get; }

    public Guid VehicleId { get; }

    public TimeWindow Window { get; }

    public AssignmentStatus Status { get; private set; }

    public Guid AssignedByUserId { get; }

    public DateTime CreatedAtUtc { get; }

    public DateTime? EndedAtUtc { get; private set; }

    public string? ReleaseReason { get; private set; }

    /// <summary>
    /// Creates an active assignment and moves the shipment Accepted → Assigned, atomically in memory.
    /// <paramref name="existingAssignments"/> must contain the carrier's other assignments overlapping the window;
    /// the caller (Application/Infrastructure) is responsible for loading them and for database-level protection
    /// against concurrent bookings. Dispatcher membership is verified by the Application layer.
    /// </summary>
    public static Assignment Create(
        Shipment shipment,
        Driver driver,
        Vehicle vehicle,
        TimeWindow window,
        Guid dispatcherUserId,
        IEnumerable<Assignment> existingAssignments,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(existingAssignments);
        Guard.Utc(nowUtc, nameof(nowUtc));

        if (shipment.Status != ShipmentStatus.Accepted || shipment.CarrierCompanyId is null)
        {
            throw new DomainException(
                DomainErrorCodes.InvalidState,
                $"Only an Accepted shipment can be assigned (current: {shipment.Status}).");
        }

        var companyId = shipment.CarrierCompanyId.Value;
        if (driver.CompanyId != companyId || vehicle.CompanyId != companyId)
        {
            throw new DomainException(
                DomainErrorCodes.CrossCompany,
                "The driver and vehicle must belong to the company that accepted the shipment.");
        }

        if (!driver.IsActive)
        {
            throw new DomainException(DomainErrorCodes.InvalidState, "The driver is not active.");
        }

        if (!vehicle.IsActive)
        {
            throw new DomainException(DomainErrorCodes.InvalidState, "The vehicle is not active.");
        }

        if (!vehicle.Capacity.CanCarry(shipment.Cargo))
        {
            throw new DomainException(
                DomainErrorCodes.CapacityExceeded,
                "The vehicle capacity is not sufficient for the shipment cargo.");
        }

        if (window.EndUtc <= nowUtc)
        {
            throw Guard.Invalid("The assignment window must end in the future.");
        }

        var conflicts = FindConflicts(existingAssignments, driver.Id, vehicle.Id, window);
        if (conflicts.Count > 0)
        {
            throw new AssignmentConflictException(conflicts);
        }

        var assignment = new Assignment(shipment.Id, companyId, driver.Id, vehicle.Id, window, dispatcherUserId, nowUtc);
        shipment.MarkAssigned(assignment.Id, dispatcherUserId, nowUtc);
        return assignment;
    }

    /// <summary>
    /// Pure overlap detection: active assignments for the same driver or vehicle whose window overlaps
    /// <paramref name="window"/>. Windows are half-open, so back-to-back windows do not conflict.
    /// </summary>
    public static IReadOnlyList<AssignmentConflict> FindConflicts(
        IEnumerable<Assignment> existingAssignments,
        Guid driverId,
        Guid vehicleId,
        TimeWindow window)
    {
        ArgumentNullException.ThrowIfNull(existingAssignments);
        ArgumentNullException.ThrowIfNull(window);

        var conflicts = new List<AssignmentConflict>();
        foreach (var existing in existingAssignments.Where(a => a.Status == AssignmentStatus.Active && a.Window.Overlaps(window)))
        {
            if (existing.DriverId == driverId)
            {
                conflicts.Add(new AssignmentConflict(existing.Id, ConflictResource.Driver));
            }

            if (existing.VehicleId == vehicleId)
            {
                conflicts.Add(new AssignmentConflict(existing.Id, ConflictResource.Vehicle));
            }
        }

        return conflicts;
    }

    /// <summary>Releases this assignment and returns the shipment to Accepted so it can be re-assigned.</summary>
    public void UnassignFrom(Shipment shipment, Guid actorUserId, string reason, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        EnsureActive();
        if (shipment.Id != ShipmentId || shipment.CurrentAssignmentId != Id)
        {
            throw new DomainException(DomainErrorCodes.InvalidState, "This is not the shipment's current assignment.");
        }

        shipment.Unassign(actorUserId, reason, nowUtc);
        Release(reason, nowUtc);
    }

    /// <summary>
    /// Releases the assignment without touching the shipment. Use after the shipment itself was cancelled
    /// (the cancel event metadata carries the assignment id to release).
    /// </summary>
    public void Release(string reason, DateTime nowUtc)
    {
        EnsureActive();
        ReleaseReason = Guard.NotBlank(reason, nameof(reason), MaxReasonLength);
        EndedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
        Status = AssignmentStatus.Released;
    }

    /// <summary>Completes the assignment once its shipment has been delivered.</summary>
    public void Complete(Shipment shipment, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        EnsureActive();
        if (shipment.Id != ShipmentId || shipment.Status != ShipmentStatus.Delivered)
        {
            throw new DomainException(DomainErrorCodes.InvalidState, "The assignment can only be completed for a delivered shipment.");
        }

        EndedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
        Status = AssignmentStatus.Completed;
    }

    private void EnsureActive()
    {
        if (Status != AssignmentStatus.Active)
        {
            throw new DomainException(DomainErrorCodes.InvalidState, $"The assignment is {Status}, not Active.");
        }
    }
}
