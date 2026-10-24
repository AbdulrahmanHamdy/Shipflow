using ShipFlow.Domain.Shipments;

namespace ShipFlow.Domain.Tests;

public class ShipmentLifecycleTests
{
    // Independent statement of the documented table (docs/ARCHITECTURE.md). Kept separate from the implementation on purpose.
    private static readonly (ShipmentStatus From, ShipmentStatus To)[] Expected =
    [
        (ShipmentStatus.Pending, ShipmentStatus.Quoting),
        (ShipmentStatus.Pending, ShipmentStatus.Cancelled),
        (ShipmentStatus.Quoting, ShipmentStatus.Accepted),
        (ShipmentStatus.Quoting, ShipmentStatus.Cancelled),
        (ShipmentStatus.Accepted, ShipmentStatus.Assigned),
        (ShipmentStatus.Accepted, ShipmentStatus.Cancelled),
        (ShipmentStatus.Assigned, ShipmentStatus.Accepted),
        (ShipmentStatus.Assigned, ShipmentStatus.PickedUp),
        (ShipmentStatus.Assigned, ShipmentStatus.Cancelled),
        (ShipmentStatus.PickedUp, ShipmentStatus.InTransit),
        (ShipmentStatus.PickedUp, ShipmentStatus.DeliveryFailed),
        (ShipmentStatus.InTransit, ShipmentStatus.Delivered),
        (ShipmentStatus.InTransit, ShipmentStatus.DeliveryFailed),
        (ShipmentStatus.DeliveryFailed, ShipmentStatus.InTransit),
        (ShipmentStatus.DeliveryFailed, ShipmentStatus.Cancelled)
    ];

    private static readonly ShipmentStatus[] All = Enum.GetValues<ShipmentStatus>();

    [Fact]
    public void EveryStatusPair_IsAllowedExactlyWhenDocumented()
    {
        foreach (var from in All)
        {
            foreach (var to in All)
            {
                var documented = Expected.Contains((from, to));
                Assert.True(
                    documented == ShipmentLifecycle.CanTransition(from, to),
                    $"{from} -> {to}: documented={documented}");
            }
        }
    }

    [Fact]
    public void TerminalStates_HaveNoOutgoingTransitions()
    {
        Assert.True(ShipmentLifecycle.IsTerminal(ShipmentStatus.Delivered));
        Assert.True(ShipmentLifecycle.IsTerminal(ShipmentStatus.Cancelled));
        Assert.Empty(ShipmentLifecycle.NextStates(ShipmentStatus.Delivered));
        Assert.Empty(ShipmentLifecycle.NextStates(ShipmentStatus.Cancelled));
    }

    [Fact]
    public void OnlyDeliveredAndCancelled_AreTerminal()
    {
        var terminal = All.Where(ShipmentLifecycle.IsTerminal).OrderBy(s => s).ToArray();

        Assert.Equal(new[] { ShipmentStatus.Delivered, ShipmentStatus.Cancelled }.OrderBy(s => s).ToArray(), terminal);
    }

    [Fact]
    public void NoStatus_CanTransitionToItself()
    {
        foreach (var status in All)
        {
            Assert.False(ShipmentLifecycle.CanTransition(status, status));
        }
    }

    [Fact]
    public void EveryNonTerminalState_CanReachATerminalState()
    {
        foreach (var start in All.Where(s => !ShipmentLifecycle.IsTerminal(s)))
        {
            var seen = new HashSet<ShipmentStatus> { start };
            var queue = new Queue<ShipmentStatus>([start]);
            var reachedTerminal = false;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in ShipmentLifecycle.NextStates(current))
                {
                    reachedTerminal |= ShipmentLifecycle.IsTerminal(next);
                    if (seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            Assert.True(reachedTerminal, $"{start} cannot reach a terminal state");
        }
    }

    [Fact]
    public void CustomerCannotCancelAfterPickup_ButFailedDeliveryHasAnExit()
    {
        Assert.False(ShipmentLifecycle.CanTransition(ShipmentStatus.PickedUp, ShipmentStatus.Cancelled));
        Assert.False(ShipmentLifecycle.CanTransition(ShipmentStatus.InTransit, ShipmentStatus.Cancelled));
        Assert.True(ShipmentLifecycle.CanTransition(ShipmentStatus.DeliveryFailed, ShipmentStatus.InTransit));
        Assert.True(ShipmentLifecycle.CanTransition(ShipmentStatus.DeliveryFailed, ShipmentStatus.Cancelled));
    }
}
