using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Shipments;

/// <summary>Input describing one stop when creating a shipment. Stop type is derived from position.</summary>
public sealed record StopInfo(string Address, GeoPoint Location, TimeWindow? Window = null);
