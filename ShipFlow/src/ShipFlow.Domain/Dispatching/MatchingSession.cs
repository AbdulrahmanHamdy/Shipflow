using System;
using ShipFlow.Domain.Shipments;

namespace ShipFlow.Domain.Dispatching
{
    public enum MatchingSessionStatus
    {
        Active,
        Skipped,
        TimedOut,
        Completed
    }

    public class MatchingSession
    {
        public Guid Id { get; set; }
        public Guid ShipmentId { get; set; }
        public Shipment Shipment { get; set; } = null!;
        public DateTime StartedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public MatchingSessionStatus Status { get; set; } = MatchingSessionStatus.Active;
    }
}
