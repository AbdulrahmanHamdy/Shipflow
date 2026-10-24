using ShipFlow.Domain.Common;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tests.Support;
using ShipFlow.Domain.Tracking;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Tests;

public class TrackingPointTests
{
    private static readonly GeoPoint Here = GeoPoint.Of(30, 31);

    [Theory]
    [InlineData(ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.InTransit)]
    public void Create_IsAllowedWhileTheShipmentIsMoving(ShipmentStatus status)
    {
        var f = new Fixture(status);

        var point = TrackingPoint.Create(f.Shipment, f.DriverUserId, Here, Fixture.T(150), Fixture.T(151), 62.5m);

        Assert.Equal(f.Shipment.Id, point.ShipmentId);
        Assert.Equal(f.DriverUserId, point.RecordedByUserId);
        Assert.Equal(62.5m, point.SpeedKph);
    }

    [Theory]
    [InlineData(ShipmentStatus.Pending)]
    [InlineData(ShipmentStatus.Accepted)]
    [InlineData(ShipmentStatus.Assigned)]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void Create_IsRejectedWhenTheShipmentIsNotMoving(ShipmentStatus status)
    {
        var f = new Fixture(status);

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () =>
            TrackingPoint.Create(f.Shipment, f.DriverUserId, Here, Fixture.T(150), Fixture.T(151)));
    }

    [Fact]
    public void Create_RejectsTimestampBeyondClockSkew()
    {
        var f = new Fixture(ShipmentStatus.InTransit);

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            TrackingPoint.Create(f.Shipment, f.DriverUserId, Here, Fixture.T(151).AddMinutes(6), Fixture.T(151)));
        Assert.NotNull(TrackingPoint.Create(f.Shipment, f.DriverUserId, Here, Fixture.T(151).AddMinutes(4), Fixture.T(151)));
    }

    [Fact]
    public void Create_RejectsNegativeSpeedAndEmptyRecorder()
    {
        var f = new Fixture(ShipmentStatus.InTransit);

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            TrackingPoint.Create(f.Shipment, f.DriverUserId, Here, Fixture.T(150), Fixture.T(151), -1m));
        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            TrackingPoint.Create(f.Shipment, Guid.Empty, Here, Fixture.T(150), Fixture.T(151)));
    }
}

public class DeliveryProofTests
{
    [Theory]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.Delivered)]
    public void Create_IsAllowedAroundDelivery(ShipmentStatus status)
    {
        var f = new Fixture(status);

        var proof = DeliveryProof.Create(
            f.Shipment, f.DriverUserId, "  Rita Recipient ", Fixture.T(205), Fixture.T(206),
            GeoPoint.Of(31.2, 29.9), "left at door", "sig/abc.png", "photo/abc.jpg");

        Assert.Equal(f.Shipment.Id, proof.ShipmentId);
        Assert.Equal("Rita Recipient", proof.RecipientName);
        Assert.Equal("sig/abc.png", proof.SignatureReference);
        Assert.Equal("photo/abc.jpg", proof.PhotoReference);
    }

    [Theory]
    [InlineData(ShipmentStatus.Assigned)]
    [InlineData(ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.DeliveryFailed)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void Create_IsRejectedForOtherStates(ShipmentStatus status)
    {
        var f = new Fixture(status);

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () =>
            DeliveryProof.Create(f.Shipment, f.DriverUserId, "Rita", Fixture.T(205), Fixture.T(206)));
    }

    [Fact]
    public void Create_RequiresRecipientName()
    {
        var f = new Fixture(ShipmentStatus.InTransit);

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            DeliveryProof.Create(f.Shipment, f.DriverUserId, " ", Fixture.T(205), Fixture.T(206)));
    }

    [Fact]
    public void Create_RejectsFutureDeliveryTime()
    {
        var f = new Fixture(ShipmentStatus.InTransit);

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            DeliveryProof.Create(f.Shipment, f.DriverUserId, "Rita", Fixture.T(300), Fixture.T(206)));
    }
}
