using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.CargoAnalysis;

public sealed class CargoItem : Entity
{
    public const int MaxNameLength = 150;
    public const int MaxNotesLength = 1000;

    private CargoItem()
    {
        Name = null!;
    }

    public CargoItem(
        Guid id,
        Guid sessionId,
        string name,
        CargoCategory category,
        int quantity,
        decimal lengthCm,
        decimal widthCm,
        decimal heightCm,
        decimal weightKg,
        decimal originalVolumeM3,
        bool canBeDisassembled,
        string? disassemblyNotes,
        decimal packedLengthCm,
        decimal packedWidthCm,
        decimal packedHeightCm,
        decimal packedWeightKg,
        decimal packedVolumeM3,
        PackingComplexity packingComplexity,
        FragilityLevel fragility,
        string? specialHandling,
        decimal confidenceScore,
        string? estimationNotes)
        : base(id)
    {
        SessionId = Guard.NotEmpty(sessionId, nameof(sessionId));
        Name = Guard.NotBlank(name, nameof(name), MaxNameLength);
        Category = category;
        Quantity = Guard.Positive(quantity, nameof(quantity));
        LengthCm = Guard.Positive(lengthCm, nameof(lengthCm));
        WidthCm = Guard.Positive(widthCm, nameof(widthCm));
        HeightCm = Guard.Positive(heightCm, nameof(heightCm));
        WeightKg = Guard.Positive(weightKg, nameof(weightKg));
        OriginalVolumeM3 = originalVolumeM3 >= 0 ? originalVolumeM3 : throw Guard.Invalid("Original volume must not be negative.");
        CanBeDisassembled = canBeDisassembled;
        DisassemblyNotes = Guard.OptionalText(disassemblyNotes, nameof(disassemblyNotes), MaxNotesLength);
        PackedLengthCm = Guard.Positive(packedLengthCm, nameof(packedLengthCm));
        PackedWidthCm = Guard.Positive(packedWidthCm, nameof(packedWidthCm));
        PackedHeightCm = Guard.Positive(packedHeightCm, nameof(packedHeightCm));
        PackedWeightKg = Guard.Positive(packedWeightKg, nameof(packedWeightKg));
        PackedVolumeM3 = packedVolumeM3 >= 0 ? packedVolumeM3 : throw Guard.Invalid("Packed volume must not be negative.");
        PackingComplexity = packingComplexity;
        Fragility = fragility;
        SpecialHandling = Guard.OptionalText(specialHandling, nameof(specialHandling), MaxNotesLength);
        ConfidenceScore = confidenceScore >= 0m && confidenceScore <= 1m ? confidenceScore : 0.85m;
        EstimationNotes = Guard.OptionalText(estimationNotes, nameof(estimationNotes), MaxNotesLength);
    }

    public Guid SessionId { get; }
    public string Name { get; private set; }
    public CargoCategory Category { get; private set; }
    public int Quantity { get; private set; }
    public decimal LengthCm { get; private set; }
    public decimal WidthCm { get; private set; }
    public decimal HeightCm { get; private set; }
    public decimal WeightKg { get; private set; }
    public decimal OriginalVolumeM3 { get; private set; }
    public bool CanBeDisassembled { get; private set; }
    public string? DisassemblyNotes { get; private set; }
    public decimal PackedLengthCm { get; private set; }
    public decimal PackedWidthCm { get; private set; }
    public decimal PackedHeightCm { get; private set; }
    public decimal PackedWeightKg { get; private set; }
    public decimal PackedVolumeM3 { get; private set; }
    public PackingComplexity PackingComplexity { get; private set; }
    public FragilityLevel Fragility { get; private set; }
    public string? SpecialHandling { get; private set; }
    public decimal ConfidenceScore { get; private set; }
    public string? EstimationNotes { get; private set; }

    public void UpdateDetails(
        string name,
        CargoCategory category,
        int quantity,
        decimal lengthCm,
        decimal widthCm,
        decimal heightCm,
        decimal weightKg,
        bool canBeDisassembled,
        string? disassemblyNotes,
        decimal packedLengthCm,
        decimal packedWidthCm,
        decimal packedHeightCm,
        decimal packedWeightKg,
        PackingComplexity packingComplexity,
        FragilityLevel fragility,
        string? specialHandling,
        string? estimationNotes)
    {
        Name = Guard.NotBlank(name, nameof(name), MaxNameLength);
        Category = category;
        Quantity = Guard.Positive(quantity, nameof(quantity));
        LengthCm = Guard.Positive(lengthCm, nameof(lengthCm));
        WidthCm = Guard.Positive(widthCm, nameof(widthCm));
        HeightCm = Guard.Positive(heightCm, nameof(heightCm));
        WeightKg = Guard.Positive(weightKg, nameof(weightKg));
        OriginalVolumeM3 = Math.Round((LengthCm * WidthCm * HeightCm) / 1_000_000m, 4);
        CanBeDisassembled = canBeDisassembled;
        DisassemblyNotes = Guard.OptionalText(disassemblyNotes, nameof(disassemblyNotes), MaxNotesLength);
        PackedLengthCm = Guard.Positive(packedLengthCm, nameof(packedLengthCm));
        PackedWidthCm = Guard.Positive(packedWidthCm, nameof(packedWidthCm));
        PackedHeightCm = Guard.Positive(packedHeightCm, nameof(packedHeightCm));
        PackedWeightKg = Guard.Positive(packedWeightKg, nameof(packedWeightKg));
        PackedVolumeM3 = Math.Round((PackedLengthCm * PackedWidthCm * PackedHeightCm) / 1_000_000m, 4);
        PackingComplexity = packingComplexity;
        Fragility = fragility;
        SpecialHandling = Guard.OptionalText(specialHandling, nameof(specialHandling), MaxNotesLength);
        EstimationNotes = Guard.OptionalText(estimationNotes, nameof(estimationNotes), MaxNotesLength);
    }
}
