namespace ShipFlow.Application.Features.Dispatching;

public sealed record EligibleDriverDto(
    Guid DriverId,
    Guid UserId,
    string FullName,
    string LicenseNumber,
    bool IsAvailable,
    Guid? VehicleId,
    string? VehiclePlate,
    string? VehicleType,
    decimal? VehicleMaxWeightKg,
    decimal? VehicleMaxVolumeM3,
    bool CanCarryDimensions,
    string EligibilityStatus,
    string? VehicleModel = null);

public sealed record MatchingStatusDto(
    Guid ShipmentId,
    string? Reference,
    string Status,
    string PickupAddress,
    DateTime? PickupWindowStartUtc,
    DateTime? PickupWindowEndUtc,
    string DeliveryAddress,
    DateTime? DeliveryWindowStartUtc,
    DateTime? DeliveryWindowEndUtc,
    string CargoDescription,
    decimal CargoWeightKg,
    decimal CargoVolumeM3,
    string RequiredVehicleType,
    int EligibleDriversCount,
    int NotifiedDriversCount,
    IReadOnlyList<EligibleDriverDto> EligibleDrivers,
    EligibleDriverDto? AssignedDriver,
    DateTime? DispatchedAtUtc);

public sealed record AvailableShipmentDto(
    Guid ShipmentId,
    string? Reference,
    string Status,
    string PickupAddress,
    DateTime? PickupWindowStartUtc,
    DateTime? PickupWindowEndUtc,
    string DeliveryAddress,
    DateTime? DeliveryWindowStartUtc,
    DateTime? DeliveryWindowEndUtc,
    string CargoDescription,
    decimal CargoWeightKg,
    decimal CargoVolumeM3,
    string RequiredVehicleType,
    double? EstimatedDistanceKm,
    decimal? PriceAmount,
    string? PriceCurrency,
    DateTime CreatedAtUtc,
    string CarrierCompanyName);

public sealed record DriverProfileDto(
    Guid DriverId,
    Guid UserId,
    string FullName,
    string LicenseNumber,
    Guid CompanyId,
    string CompanyName,
    Guid? AssignedVehicleId,
    string? AssignedVehiclePlate,
    string? AssignedVehicleType,
    bool IsAvailable,
    bool HasActiveAssignment,
    string? AssignedVehicleModel = null);

public sealed record UpdateDriverAvailabilityCommand(bool IsAvailable);

public sealed record AssignDriverVehicleCommand(Guid? VehicleId);
