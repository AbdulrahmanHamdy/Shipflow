using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Tracking;

namespace ShipFlow.Application.Features.Tracking;

// ─── Commands and Queries ────────────────────────────────────────────────────

public sealed record SubmitTrackingPointCommand(
    Guid ShipmentId,
    double Latitude,
    double Longitude,
    DateTime RecordedAtUtc,
    decimal? SpeedKph);

public sealed record DeliverShipmentCommand(
    DateTime DeliveredAtUtc,
    string RecipientName,
    string? Notes,
    string? SignatureReference,
    string? PhotoReference);

public sealed record FailDeliveryCommand(string Reason);

public sealed record TrackingHistoryQuery(
    Guid ShipmentId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 50);

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed record TrackingPointDto(
    Guid Id,
    Guid ShipmentId,
    double Latitude,
    double Longitude,
    DateTime RecordedAtUtc,
    decimal? SpeedKph);

public sealed record DeliveryProofDto(
    Guid Id,
    Guid ShipmentId,
    string RecipientName,
    DateTime DeliveredAtUtc,
    string? Notes,
    string? SignatureReference,
    string? PhotoReference);

// ─── Mapping ─────────────────────────────────────────────────────────────────

internal static class TrackingMapping
{
    public static TrackingPointDto ToDto(this TrackingPoint p) =>
        new(p.Id, p.ShipmentId, p.Location.Latitude, p.Location.Longitude, p.RecordedAtUtc, p.SpeedKph);

    public static DeliveryProofDto ToDto(this DeliveryProof p) =>
        new(p.Id, p.ShipmentId, p.RecipientName, p.DeliveredAtUtc, p.Notes, p.SignatureReference, p.PhotoReference);
}
