using ShipFlow.Domain.CargoAnalysis;
using ShipFlow.Domain.Fleet;

namespace ShipFlow.Application.Features.CargoAnalysis;

public sealed record CargoImageUpload(
    string ContentType,
    byte[] Bytes,
    string FileName);

public sealed record CargoItemEstimateDto(
    string Name,
    CargoCategory Category,
    int Quantity,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal WeightKg,
    decimal OriginalVolumeM3,
    bool CanBeDisassembled,
    string? DisassemblyNotes,
    decimal PackedLengthCm,
    decimal PackedWidthCm,
    decimal PackedHeightCm,
    decimal PackedWeightKg,
    decimal PackedVolumeM3,
    PackingComplexity PackingComplexity,
    FragilityLevel Fragility,
    string? SpecialHandling,
    decimal ConfidenceScore,
    string? EstimationNotes);

public sealed record CargoItemDto(
    Guid Id,
    string Name,
    CargoCategory Category,
    int Quantity,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal WeightKg,
    decimal OriginalVolumeM3,
    bool CanBeDisassembled,
    string? DisassemblyNotes,
    decimal PackedLengthCm,
    decimal PackedWidthCm,
    decimal PackedHeightCm,
    decimal PackedWeightKg,
    decimal PackedVolumeM3,
    PackingComplexity PackingComplexity,
    FragilityLevel Fragility,
    string? SpecialHandling,
    decimal ConfidenceScore,
    string? EstimationNotes);

public sealed record UpdateCargoItemInputDto(
    Guid? Id,
    string Name,
    CargoCategory Category,
    int Quantity,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal WeightKg,
    bool CanBeDisassembled,
    string? DisassemblyNotes,
    decimal PackedLengthCm,
    decimal PackedWidthCm,
    decimal PackedHeightCm,
    decimal PackedWeightKg,
    PackingComplexity PackingComplexity,
    FragilityLevel Fragility,
    string? SpecialHandling,
    decimal? ConfidenceScore,
    string? EstimationNotes);

public sealed record CargoCalculationResult(
    decimal TotalWeightKg,
    decimal TotalOriginalVolumeM3,
    decimal TotalPackedVolumeM3,
    decimal MaxLengthCm,
    decimal MaxWidthCm,
    decimal MaxHeightCm,
    decimal VolumeSavingsPercentage,
    decimal PackingAllowanceM3,
    int TotalItemsCount,
    bool HasFragileItems,
    IReadOnlyList<string> SpecialHandlingRequirements);

public sealed record VehicleRecommendationDto(
    Guid VehicleId,
    string PlateNumber,
    VehicleType Type,
    decimal MaxWeightKg,
    decimal MaxVolumeM3,
    decimal WeightUtilizationPercentage,
    decimal VolumeUtilizationPercentage,
    bool IsSuitable,
    string SuitabilityRank,
    string Reason,
    IReadOnlyList<string> Warnings);

public sealed record VehicleRecommendationResultDto(
    CargoCalculationResult CargoSummary,
    VehicleRecommendationDto? BestMatch,
    IReadOnlyList<VehicleRecommendationDto> Recommendations);

public sealed record CargoAnalysisResponseDto(
    Guid SessionId,
    CargoAnalysisStatus Status,
    DateTime CreatedAtUtc,
    CargoCalculationResult Calculation,
    IReadOnlyList<CargoItemDto> Items,
    VehicleRecommendationResultDto? VehicleRecommendations);

public sealed record UpdateCargoAnalysisItemsCommand(
    IReadOnlyList<UpdateCargoItemInputDto> Items);

public sealed record RecommendVehiclesCommand(
    IReadOnlyList<UpdateCargoItemInputDto> Items);
