using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.ValueObjects;

/// <summary>The goods to be moved: description, weight (kg) and volume (m³).</summary>
public sealed record Cargo
{
    public const int MaxDescriptionLength = 500;

    private Cargo(string description, decimal weightKg, decimal volumeM3)
    {
        Description = description;
        WeightKg = weightKg;
        VolumeM3 = volumeM3;
    }

    public string Description { get; }

    public decimal WeightKg { get; }

    public decimal VolumeM3 { get; }

    public static Cargo Of(string description, decimal weightKg, decimal volumeM3)
    {
        var text = Guard.NotBlank(description, nameof(description), MaxDescriptionLength);
        Guard.Positive(weightKg, nameof(weightKg));
        if (volumeM3 < 0m)
        {
            throw Guard.Invalid("Volume must not be negative.");
        }

        return new Cargo(text, weightKg, volumeM3);
    }
}
