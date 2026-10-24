using ShipFlow.Domain.Common;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Tracking;

/// <summary>Evidence of delivery captured by the driver (recipient, optional signature/photo references).</summary>
public sealed class DeliveryProof : Entity
{
    public const int MaxRecipientLength = 150;
    public const int MaxNotesLength = 1000;
    public const int MaxReferenceLength = 500;

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private DeliveryProof()
    {
        RecipientName = null!;
    }

    private DeliveryProof(
        Guid shipmentId,
        Guid capturedByUserId,
        string recipientName,
        DateTime deliveredAtUtc,
        GeoPoint? location,
        string? notes,
        string? signatureReference,
        string? photoReference)
        : base(Guid.NewGuid())
    {
        ShipmentId = shipmentId;
        CapturedByUserId = Guard.NotEmpty(capturedByUserId, nameof(capturedByUserId));
        RecipientName = Guard.NotBlank(recipientName, nameof(recipientName), MaxRecipientLength);
        DeliveredAtUtc = deliveredAtUtc;
        Location = location;
        Notes = Guard.OptionalText(notes, nameof(notes), MaxNotesLength);
        SignatureReference = Guard.OptionalText(signatureReference, nameof(signatureReference), MaxReferenceLength);
        PhotoReference = Guard.OptionalText(photoReference, nameof(photoReference), MaxReferenceLength);
    }

    public Guid ShipmentId { get; }

    public Guid CapturedByUserId { get; }

    public string RecipientName { get; }

    public DateTime DeliveredAtUtc { get; }

    public GeoPoint? Location { get; }

    public string? Notes { get; }

    /// <summary>Opaque storage key (e.g. blob name); the file itself lives outside the database.</summary>
    public string? SignatureReference { get; }

    public string? PhotoReference { get; }

    /// <summary>Allowed while the shipment is InTransit (captured just before Deliver) or already Delivered.</summary>
    public static DeliveryProof Create(
        Shipment shipment,
        Guid capturedByUserId,
        string recipientName,
        DateTime deliveredAtUtc,
        DateTime nowUtc,
        GeoPoint? location = null,
        string? notes = null,
        string? signatureReference = null,
        string? photoReference = null)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        Guard.Utc(deliveredAtUtc, nameof(deliveredAtUtc));
        Guard.Utc(nowUtc, nameof(nowUtc));

        if (shipment.Status is not (ShipmentStatus.InTransit or ShipmentStatus.Delivered))
        {
            throw new DomainException(
                DomainErrorCodes.InvalidState,
                $"Delivery proof can only be captured for an InTransit or Delivered shipment (current: {shipment.Status}).");
        }

        if (deliveredAtUtc > nowUtc + TrackingPoint.MaxClockSkew)
        {
            throw Guard.Invalid("The delivery time is in the future.");
        }

        return new DeliveryProof(
            shipment.Id, capturedByUserId, recipientName, deliveredAtUtc, location, notes, signatureReference, photoReference);
    }
}
