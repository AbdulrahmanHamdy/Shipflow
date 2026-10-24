using System.Reflection;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tests.Support;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Tests;

public class ShipmentCreationTests
{
    [Fact]
    public void Create_StartsPending_OwnedByCustomer_WithCreationEvent()
    {
        var f = new Fixture();
        var s = f.Shipment;

        Assert.Equal(ShipmentStatus.Pending, s.Status);
        Assert.Equal(f.CustomerId, s.CustomerId);
        Assert.True(s.IsOwnedBy(f.CustomerId));
        Assert.False(s.IsOwnedBy(Guid.NewGuid()));
        Assert.Null(s.CarrierCompanyId);
        Assert.Null(s.AgreedPrice);

        var created = Assert.Single(s.Events);
        Assert.Null(created.PreviousStatus);
        Assert.Equal(ShipmentStatus.Pending, created.NewStatus);
        Assert.Equal(f.CustomerId, created.ActorId);
        Assert.Equal(Fixture.Now, created.OccurredAtUtc);
    }

    [Fact]
    public void Create_AssignsStopTypesAndSequenceByPosition()
    {
        var stops = new List<StopInfo>
        {
            new("A", GeoPoint.Of(1, 1), TimeWindow.Of(Fixture.T(60), Fixture.T(120))),
            new("B", GeoPoint.Of(2, 2)),
            new("C", GeoPoint.Of(3, 3))
        };

        var shipment = Shipment.Create(Guid.NewGuid(), Cargo.Of("Goods", 10m, 1m), stops, Fixture.Now);

        Assert.Equal(new[] { StopType.Pickup, StopType.Waypoint, StopType.Delivery }, shipment.Stops.Select(x => x.Type).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, shipment.Stops.Select(x => x.Sequence).ToArray());
        Assert.Equal("A", shipment.PickupStop.Address);
        Assert.Equal("C", shipment.DeliveryStop.Address);
    }

    [Fact]
    public void Create_RequiresAtLeastTwoStops() =>
        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            Shipment.Create(Guid.NewGuid(), Cargo.Of("Goods", 1m, 1m), Fixture.Stops().Take(1).ToList(), Fixture.Now));

    [Fact]
    public void Create_RejectsMoreThanMaxStops()
    {
        var stops = Enumerable.Range(0, Shipment.MaxStops + 1)
            .Select(i => new StopInfo($"Stop {i}", GeoPoint.Of(1, 1), i == 0 ? TimeWindow.Of(Fixture.T(60), Fixture.T(120)) : null))
            .ToList();

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            Shipment.Create(Guid.NewGuid(), Cargo.Of("Goods", 1m, 1m), stops, Fixture.Now));
    }

    [Fact]
    public void Create_RequiresPickupTimeWindow()
    {
        var stops = new List<StopInfo> { new("A", GeoPoint.Of(1, 1)), new("B", GeoPoint.Of(2, 2)) };

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            Shipment.Create(Guid.NewGuid(), Cargo.Of("Goods", 1m, 1m), stops, Fixture.Now));
    }

    [Fact]
    public void Create_RejectsEmptyCustomerAndNonUtcTime()
    {
        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            Shipment.Create(Guid.Empty, Cargo.Of("Goods", 1m, 1m), Fixture.Stops(), Fixture.Now));
        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            Shipment.Create(Guid.NewGuid(), Cargo.Of("Goods", 1m, 1m), Fixture.Stops(), DateTime.SpecifyKind(Fixture.Now, DateTimeKind.Local)));
    }

    [Fact]
    public void Create_RejectsBlankStopAddress()
    {
        var stops = new List<StopInfo>
        {
            new(" ", GeoPoint.Of(1, 1), TimeWindow.Of(Fixture.T(60), Fixture.T(120))),
            new("B", GeoPoint.Of(2, 2))
        };

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            Shipment.Create(Guid.NewGuid(), Cargo.Of("Goods", 1m, 1m), stops, Fixture.Now));
    }

    [Fact]
    public void Status_HasNoPublicSetter_AndNoGenericStatusMutator()
    {
        var status = typeof(Shipment).GetProperty(nameof(Shipment.Status))!;
        Assert.True(status.SetMethod is null || !status.SetMethod.IsPublic);

        var publicMutators = typeof(Shipment)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Where(n => n.Contains("SetStatus", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("ChangeStatus", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("UpdateStatus", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("TransitionTo", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(publicMutators);
    }

    [Fact]
    public void CollectionsAndCarrierFields_CannotBeSetPublicly()
    {
        foreach (var name in new[]
                 {
                     nameof(Shipment.CarrierCompanyId), nameof(Shipment.AcceptedQuoteId), nameof(Shipment.AgreedPrice),
                     nameof(Shipment.CurrentAssignmentId), nameof(Shipment.CustomerId), nameof(Shipment.Stops),
                     nameof(Shipment.Quotes), nameof(Shipment.Events)
                 })
        {
            var property = typeof(Shipment).GetProperty(name)!;
            Assert.True(property.SetMethod is null || !property.SetMethod.IsPublic, $"{name} must not have a public setter");
        }
    }
}
