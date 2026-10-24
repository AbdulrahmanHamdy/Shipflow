using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.ValueObjects;

/// <summary>A WGS84 coordinate.</summary>
public sealed record GeoPoint
{
    private GeoPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static GeoPoint Of(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90d or > 90d)
        {
            throw Guard.Invalid("Latitude must be between -90 and 90.");
        }

        if (double.IsNaN(longitude) || longitude is < -180d or > 180d)
        {
            throw Guard.Invalid("Longitude must be between -180 and 180.");
        }

        return new GeoPoint(latitude, longitude);
    }
}
