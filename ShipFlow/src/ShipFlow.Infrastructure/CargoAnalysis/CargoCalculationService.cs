using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Features.CargoAnalysis;
using ShipFlow.Domain.CargoAnalysis;

namespace ShipFlow.Infrastructure.CargoAnalysis;

public sealed class CargoCalculationService : ICargoCalculationService
{
    private const decimal DefaultPackingMargin = 0.12m; // 12% space packing allowance

    public CargoCalculationResult Calculate(IEnumerable<CargoItemEstimateDto> items)
    {
        var list = items?.ToList() ?? [];
        if (list.Count == 0)
        {
            return EmptyResult();
        }

        var totalWeight = list.Sum(i => i.PackedWeightKg * i.Quantity);
        var totalOrigVol = list.Sum(i => i.OriginalVolumeM3 * i.Quantity);
        var totalPackedVol = list.Sum(i => i.PackedVolumeM3 * i.Quantity);

        var maxL = list.Max(i => Math.Max(i.LengthCm, i.PackedLengthCm));
        var maxW = list.Max(i => Math.Max(i.WidthCm, i.PackedWidthCm));
        var maxH = list.Max(i => Math.Max(i.HeightCm, i.PackedHeightCm));

        var savings = totalOrigVol > 0 && totalOrigVol > totalPackedVol
            ? Math.Round(((totalOrigVol - totalPackedVol) / totalOrigVol) * 100m, 1)
            : 0m;

        var allowance = Math.Round(totalPackedVol * DefaultPackingMargin, 3);
        var totalCount = list.Sum(i => i.Quantity);
        var hasFragile = list.Any(i => i.Fragility != FragilityLevel.Low);

        var special = list
            .Where(i => !string.IsNullOrWhiteSpace(i.SpecialHandling))
            .Select(i => $"{i.Name}: {i.SpecialHandling!.Trim()}")
            .Distinct()
            .ToList();

        return new CargoCalculationResult(
            TotalWeightKg: Math.Round(totalWeight, 1),
            TotalOriginalVolumeM3: Math.Round(totalOrigVol, 3),
            TotalPackedVolumeM3: Math.Round(totalPackedVol, 3),
            MaxLengthCm: Math.Round(maxL, 1),
            MaxWidthCm: Math.Round(maxW, 1),
            MaxHeightCm: Math.Round(maxH, 1),
            VolumeSavingsPercentage: savings,
            PackingAllowanceM3: allowance,
            TotalItemsCount: totalCount,
            HasFragileItems: hasFragile,
            SpecialHandlingRequirements: special
        );
    }

    public CargoCalculationResult Calculate(IEnumerable<UpdateCargoItemInputDto> items)
    {
        var list = items?.ToList() ?? [];
        if (list.Count == 0)
        {
            return EmptyResult();
        }

        var totalWeight = list.Sum(i => i.PackedWeightKg * i.Quantity);
        var totalOrigVol = list.Sum(i => ((i.LengthCm * i.WidthCm * i.HeightCm) / 1_000_000m) * i.Quantity);
        var totalPackedVol = list.Sum(i => ((i.PackedLengthCm * i.PackedWidthCm * i.PackedHeightCm) / 1_000_000m) * i.Quantity);

        var maxL = list.Max(i => Math.Max(i.LengthCm, i.PackedLengthCm));
        var maxW = list.Max(i => Math.Max(i.WidthCm, i.PackedWidthCm));
        var maxH = list.Max(i => Math.Max(i.HeightCm, i.PackedHeightCm));

        var savings = totalOrigVol > 0 && totalOrigVol > totalPackedVol
            ? Math.Round(((totalOrigVol - totalPackedVol) / totalOrigVol) * 100m, 1)
            : 0m;

        var allowance = Math.Round(totalPackedVol * DefaultPackingMargin, 3);
        var totalCount = list.Sum(i => i.Quantity);
        var hasFragile = list.Any(i => i.Fragility != FragilityLevel.Low);

        var special = list
            .Where(i => !string.IsNullOrWhiteSpace(i.SpecialHandling))
            .Select(i => $"{i.Name}: {i.SpecialHandling!.Trim()}")
            .Distinct()
            .ToList();

        return new CargoCalculationResult(
            TotalWeightKg: Math.Round(totalWeight, 1),
            TotalOriginalVolumeM3: Math.Round(totalOrigVol, 3),
            TotalPackedVolumeM3: Math.Round(totalPackedVol, 3),
            MaxLengthCm: Math.Round(maxL, 1),
            MaxWidthCm: Math.Round(maxW, 1),
            MaxHeightCm: Math.Round(maxH, 1),
            VolumeSavingsPercentage: savings,
            PackingAllowanceM3: allowance,
            TotalItemsCount: totalCount,
            HasFragileItems: hasFragile,
            SpecialHandlingRequirements: special
        );
    }

    public CargoCalculationResult Calculate(IEnumerable<CargoItem> items)
    {
        var list = items?.ToList() ?? [];
        if (list.Count == 0)
        {
            return EmptyResult();
        }

        var totalWeight = list.Sum(i => i.PackedWeightKg * i.Quantity);
        var totalOrigVol = list.Sum(i => i.OriginalVolumeM3 * i.Quantity);
        var totalPackedVol = list.Sum(i => i.PackedVolumeM3 * i.Quantity);

        var maxL = list.Max(i => Math.Max(i.LengthCm, i.PackedLengthCm));
        var maxW = list.Max(i => Math.Max(i.WidthCm, i.PackedWidthCm));
        var maxH = list.Max(i => Math.Max(i.HeightCm, i.PackedHeightCm));

        var savings = totalOrigVol > 0 && totalOrigVol > totalPackedVol
            ? Math.Round(((totalOrigVol - totalPackedVol) / totalOrigVol) * 100m, 1)
            : 0m;

        var allowance = Math.Round(totalPackedVol * DefaultPackingMargin, 3);
        var totalCount = list.Sum(i => i.Quantity);
        var hasFragile = list.Any(i => i.Fragility != FragilityLevel.Low);

        var special = list
            .Where(i => !string.IsNullOrWhiteSpace(i.SpecialHandling))
            .Select(i => $"{i.Name}: {i.SpecialHandling!.Trim()}")
            .Distinct()
            .ToList();

        return new CargoCalculationResult(
            TotalWeightKg: Math.Round(totalWeight, 1),
            TotalOriginalVolumeM3: Math.Round(totalOrigVol, 3),
            TotalPackedVolumeM3: Math.Round(totalPackedVol, 3),
            MaxLengthCm: Math.Round(maxL, 1),
            MaxWidthCm: Math.Round(maxW, 1),
            MaxHeightCm: Math.Round(maxH, 1),
            VolumeSavingsPercentage: savings,
            PackingAllowanceM3: allowance,
            TotalItemsCount: totalCount,
            HasFragileItems: hasFragile,
            SpecialHandlingRequirements: special
        );
    }

    private static CargoCalculationResult EmptyResult() => new(
        TotalWeightKg: 0m,
        TotalOriginalVolumeM3: 0m,
        TotalPackedVolumeM3: 0m,
        MaxLengthCm: 0m,
        MaxWidthCm: 0m,
        MaxHeightCm: 0m,
        VolumeSavingsPercentage: 0m,
        PackingAllowanceM3: 0m,
        TotalItemsCount: 0,
        HasFragileItems: false,
        SpecialHandlingRequirements: []
    );
}
