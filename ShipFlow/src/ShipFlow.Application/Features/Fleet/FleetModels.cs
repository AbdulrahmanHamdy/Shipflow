using ShipFlow.Domain.Fleet;

namespace ShipFlow.Application.Features.Fleet;

public sealed record DriverDto(Guid Id, Guid UserId, string FullName, string LicenseNumber, bool IsActive);

public sealed record VehicleDto(
    Guid Id, string PlateNumber, string Type, decimal MaxWeightKg, decimal MaxVolumeM3, bool IsActive);

/// <summary>Shared paging/filter query for the fleet lists. The company is the caller's, never a parameter.</summary>
public sealed record ListFleetQuery(bool? IsActive = null, int Page = 1, int PageSize = 20);

internal static class FleetMapping
{
    public static DriverDto ToDto(this Driver d) => new(d.Id, d.UserId, d.FullName, d.LicenseNumber, d.IsActive);

    public static VehicleDto ToDto(this Vehicle v) =>
        new(v.Id, v.PlateNumber, v.Type.ToString(), v.Capacity.MaxWeightKg, v.Capacity.MaxVolumeM3, v.IsActive);
}
