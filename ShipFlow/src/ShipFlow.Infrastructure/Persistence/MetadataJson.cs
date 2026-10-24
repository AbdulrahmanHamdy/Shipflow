using System.Collections.ObjectModel;
using System.Text.Json;

namespace ShipFlow.Infrastructure.Persistence;

/// <summary>Stores <c>ShipmentEvent.Metadata</c> as a JSON object in one nvarchar(max) column.</summary>
public static class MetadataJson
{
    private static readonly JsonSerializerOptions Options = new();

    public static string Serialize(IReadOnlyDictionary<string, string> value) =>
        JsonSerializer.Serialize(value, Options);

    public static IReadOnlyDictionary<string, string> Deserialize(string json)
    {
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options) ?? [];
        return new ReadOnlyDictionary<string, string>(map);
    }

    public static bool AreEqual(IReadOnlyDictionary<string, string>? left, IReadOnlyDictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var other) || !string.Equals(pair.Value, other, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public static int Hash(IReadOnlyDictionary<string, string> value)
    {
        var hash = new HashCode();
        foreach (var pair in value.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
