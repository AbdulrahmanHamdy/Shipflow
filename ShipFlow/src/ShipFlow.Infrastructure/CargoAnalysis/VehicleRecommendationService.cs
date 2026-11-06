using Microsoft.EntityFrameworkCore;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Features.CargoAnalysis;
using ShipFlow.Domain.Fleet;
using ShipFlow.Infrastructure.Persistence;

namespace ShipFlow.Infrastructure.CargoAnalysis;

public sealed class VehicleRecommendationService : IVehicleRecommendationService
{
    private readonly AppDbContext _dbContext;

    public VehicleRecommendationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<VehicleRecommendationResultDto> RecommendVehiclesAsync(
        CargoCalculationResult calculation,
        IReadOnlyList<UpdateCargoItemInputDto> items,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calculation);

        // Fetch active registered vehicles from database
        var vehicles = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.IsActive)
            .Select(v => new VehicleCandidate(
                v.Id,
                v.PlateNumber,
                v.Type,
                v.Capacity.MaxWeightKg,
                v.Capacity.MaxVolumeM3))
            .ToListAsync(cancellationToken);

        // If no active vehicles in DB (e.g. fresh DB before seeding), provide default fleet vehicle specs
        if (vehicles.Count == 0)
        {
            vehicles = GetDefaultFleetCandidates();
        }

        var evaluated = new List<VehicleRecommendationDto>();

        foreach (var v in vehicles)
        {
            var dims = GetVehicleDimensions(v.Type);
            var maxWeight = v.MaxWeightKg;
            var maxVolume = v.MaxVolumeM3;

            var weightUtil = maxWeight > 0
                ? Math.Round((calculation.TotalWeightKg / maxWeight) * 100m, 1)
                : 999m;

            var effectivePackedVolume = calculation.TotalPackedVolumeM3 + calculation.PackingAllowanceM3;
            var volumeUtil = maxVolume > 0
                ? Math.Round((effectivePackedVolume / maxVolume) * 100m, 1)
                : 999m;

            var warnings = new List<string>();
            var isWeightOk = calculation.TotalWeightKg <= maxWeight;
            var isVolumeOk = effectivePackedVolume <= maxVolume;

            if (!isWeightOk)
            {
                warnings.Add($"Exceeds maximum payload capacity by {(calculation.TotalWeightKg - maxWeight):N0} kg.");
            }

            if (!isVolumeOk)
            {
                warnings.Add($"Exceeds available cargo volume by {(effectivePackedVolume - maxVolume):N2} m³.");
            }

            // Dimension check: Can the largest packed items physically fit through door and into the cargo bay?
            var doesFitDimensions = true;
            foreach (var item in items)
            {
                var l = item.PackedLengthCm > 0 ? item.PackedLengthCm : item.LengthCm;
                var w = item.PackedWidthCm > 0 ? item.PackedWidthCm : item.WidthCm;
                var h = item.PackedHeightCm > 0 ? item.PackedHeightCm : item.HeightCm;

                // Sort dimensions to test orientation fit (smallest 2 dimensions must pass through opening)
                var sorted = new[] { l, w, h }.OrderBy(x => x).ToArray();
                var smallest = sorted[0];
                var middle = sorted[1];
                var largest = sorted[2];

                if (dims.HasDoorOpening)
                {
                    // Can it pass through the door opening? (smallest & middle must be <= door width & height)
                    var fitsDoorNormal = smallest <= dims.DoorWidthCm && middle <= dims.DoorHeightCm;
                    var fitsDoorRotated = smallest <= dims.DoorHeightCm && middle <= dims.DoorWidthCm;

                    if (!fitsDoorNormal && !fitsDoorRotated)
                    {
                        doesFitDimensions = false;
                        warnings.Add($"Item '{item.Name}' ({largest:N0}x{middle:N0}x{smallest:N0} cm) cannot fit through the {v.Type} rear door opening ({dims.DoorWidthCm}x{dims.DoorHeightCm} cm).");
                    }
                }

                // Can it fit inside the interior cargo length, width, and height?
                if (largest > dims.CargoLengthCm || middle > dims.CargoWidthCm || smallest > dims.CargoHeightCm)
                {
                    doesFitDimensions = false;
                    warnings.Add($"Item '{item.Name}' length/dimension ({largest:N0} cm) exceeds internal cargo bay dimension ({dims.CargoLengthCm:N0} cm).");
                }
            }

            if (calculation.HasFragileItems && v.Type == VehicleType.Pickup)
            {
                warnings.Add("Open bed pickup is not recommended for fragile cargo without enclosed weatherproof shelter.");
            }

            var isSuitable = isWeightOk && isVolumeOk && doesFitDimensions;

            string rank;
            string reason;

            if (!isSuitable)
            {
                rank = !isWeightOk ? "Overweight" : (!isVolumeOk ? "TooSmall" : "DimensionMismatch");
                reason = $"Not suitable: {string.Join(" ", warnings)}";
            }
            else
            {
                // Suitable
                if (weightUtil >= 40m && weightUtil <= 85m && volumeUtil >= 40m && volumeUtil <= 85m)
                {
                    rank = "BestMatch";
                    reason = $"Optimal match: {v.Type} capacity ({maxWeight:N0} kg, {maxVolume:N1} m³) comfortably accommodates all items with {weightUtil:N0}% weight and {volumeUtil:N0}% volume utilization.";
                }
                else if (weightUtil < 40m && volumeUtil < 40m)
                {
                    rank = "Alternative";
                    reason = $"Spacious option: {v.Type} offers substantial spare capacity ({weightUtil:N0}% weight, {volumeUtil:N0}% volume utilization).";
                }
                else
                {
                    rank = "Suitable";
                    reason = $"Capable: {v.Type} can transport this shipment with {weightUtil:N0}% weight and {volumeUtil:N0}% volume utilization.";
                    if (weightUtil > 85m || volumeUtil > 85m)
                    {
                        warnings.Add("Close to vehicle capacity limits (>85%). Ensure efficient professional loading.");
                    }
                }
            }

            evaluated.Add(new VehicleRecommendationDto(
                VehicleId: v.Id,
                PlateNumber: v.PlateNumber,
                Type: v.Type,
                MaxWeightKg: maxWeight,
                MaxVolumeM3: maxVolume,
                WeightUtilizationPercentage: Math.Min(999m, weightUtil),
                VolumeUtilizationPercentage: Math.Min(999m, volumeUtil),
                IsSuitable: isSuitable,
                SuitabilityRank: rank,
                Reason: reason,
                Warnings: warnings
            ));
        }

        // Sorting: BestMatch first, then Suitable, then Alternative, then others
        var sortedList = evaluated
            .OrderByDescending(r => r.IsSuitable)
            .ThenBy(r => GetRankSortOrder(r.SuitabilityRank))
            .ThenBy(r => Math.Abs(r.VolumeUtilizationPercentage - 65m))
            .ToList();

        var bestMatch = sortedList.FirstOrDefault(r => r.IsSuitable);

        return new VehicleRecommendationResultDto(
            CargoSummary: calculation,
            BestMatch: bestMatch,
            Recommendations: sortedList
        );
    }

    private static int GetRankSortOrder(string rank) => rank switch
    {
        "BestMatch" => 0,
        "Suitable" => 1,
        "Alternative" => 2,
        _ => 3
    };

    private static (decimal CargoLengthCm, decimal CargoWidthCm, decimal CargoHeightCm, bool HasDoorOpening, decimal DoorWidthCm, decimal DoorHeightCm) GetVehicleDimensions(VehicleType type) => type switch
    {
        VehicleType.Van => (280m, 175m, 145m, true, 155m, 130m),
        VehicleType.Pickup => (210m, 145m, 60m, false, 0m, 0m),
        VehicleType.Truck => (480m, 230m, 230m, true, 220m, 220m),
        VehicleType.Trailer => (1360m, 245m, 265m, true, 240m, 255m),
        _ => (300m, 180m, 150m, false, 0m, 0m)
    };

    private sealed record VehicleCandidate(
        Guid Id,
        string PlateNumber,
        VehicleType Type,
        decimal MaxWeightKg,
        decimal MaxVolumeM3);

    private static List<VehicleCandidate> GetDefaultFleetCandidates() =>
    [
        new(Guid.NewGuid(), "VAN-101", VehicleType.Van, 1200m, 8.5m),
        new(Guid.NewGuid(), "TRK-202", VehicleType.Truck, 4500m, 24.0m),
        new(Guid.NewGuid(), "PKP-303", VehicleType.Pickup, 950m, 4.0m),
        new(Guid.NewGuid(), "TRL-404", VehicleType.Trailer, 22000m, 85.0m)
    ];
}
