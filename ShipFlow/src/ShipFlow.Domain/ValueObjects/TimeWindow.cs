using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.ValueObjects;

/// <summary>
/// A half-open UTC interval <c>[Start, End)</c>. Back-to-back windows (one ends exactly when the next
/// starts) do not overlap.
/// </summary>
public sealed record TimeWindow
{
    private TimeWindow(DateTime startUtc, DateTime endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public DateTime StartUtc { get; }

    public DateTime EndUtc { get; }

    public static TimeWindow Of(DateTime startUtc, DateTime endUtc)
    {
        Guard.Utc(startUtc, nameof(startUtc));
        Guard.Utc(endUtc, nameof(endUtc));

        if (endUtc <= startUtc)
        {
            throw Guard.Invalid("Window end must be after its start.");
        }

        return new TimeWindow(startUtc, endUtc);
    }

    public bool Overlaps(TimeWindow other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return StartUtc < other.EndUtc && other.StartUtc < EndUtc;
    }

    public bool Contains(DateTime instantUtc) => instantUtc >= StartUtc && instantUtc < EndUtc;
}
