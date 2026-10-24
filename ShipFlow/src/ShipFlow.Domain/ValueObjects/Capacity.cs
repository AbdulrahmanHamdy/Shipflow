using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.ValueObjects;

/// <summary>What a vehicle can carry: maximum weight (kg) and volume (m³).</summary>
public sealed record Capacity
{
    private Capacity(decimal maxWeightKg, decimal maxVolumeM3)
    {
        MaxWeightKg = maxWeightKg;
        MaxVolumeM3 = maxVolumeM3;
    }

    public decimal MaxWeightKg { get; }

    public decimal MaxVolumeM3 { get; }

    public static Capacity Of(decimal maxWeightKg, decimal maxVolumeM3)
    {
        Guard.Positive(maxWeightKg, nameof(maxWeightKg));
        Guard.Positive(maxVolumeM3, nameof(maxVolumeM3));
        return new Capacity(maxWeightKg, maxVolumeM3);
    }

    public bool CanCarry(Cargo cargo)
    {
        ArgumentNullException.ThrowIfNull(cargo);
        return cargo.WeightKg <= MaxWeightKg && cargo.VolumeM3 <= MaxVolumeM3;
    }
}
