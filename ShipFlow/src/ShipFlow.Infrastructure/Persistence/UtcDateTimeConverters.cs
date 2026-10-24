using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ShipFlow.Infrastructure.Persistence;

/// <summary>
/// SQL Server returns <c>datetime2</c> values with <see cref="DateTimeKind.Unspecified"/>. Every persisted time in
/// ShipFlow is UTC (the domain rejects non-UTC values), so reading re-tags the value as UTC.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
