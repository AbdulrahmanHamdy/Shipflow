using ShipFlow.Domain.Common;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Shipments;

/// <summary>
/// Aggregate root for a shipment: its stops, quotes and status history. All status changes go through
/// named methods that enforce <see cref="ShipmentLifecycle"/>, ownership rules and record a
/// <see cref="ShipmentEvent"/>. There is intentionally no public way to set <see cref="Status"/> directly.
/// </summary>
public sealed class Shipment : Entity
{
    public const int MinStops = 2;
    public const int MaxStops = 20;
    public const int MaxReferenceLength = 100;
    public const int MaxReasonLength = 500;

    private readonly List<ShipmentStop> _stops = [];
    private readonly List<Quote> _quotes = [];
    private readonly List<ShipmentEvent> _events = [];

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private Shipment()
    {
        Cargo = null!;
    }

    private Shipment(Guid id, Guid customerId, Cargo cargo, string? reference, DateTime nowUtc)
        : base(id)
    {
        CustomerId = Guard.NotEmpty(customerId, nameof(customerId));
        Cargo = Guard.NotNull(cargo, nameof(cargo));
        Reference = Guard.OptionalText(reference, nameof(reference), MaxReferenceLength);
        CreatedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = nowUtc;
        Status = ShipmentStatus.Pending;
    }

    /// <summary>The owning customer (user id).</summary>
    public Guid CustomerId { get; }

    public string? Reference { get; }

    public Cargo Cargo { get; }

    public ShipmentStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>The company whose quote was accepted. Null until a quote is accepted.</summary>
    public Guid? CarrierCompanyId { get; private set; }

    public Guid? AcceptedQuoteId { get; private set; }

    /// <summary>Price fixed by the accepted quote (never taken from client input).</summary>
    public Money? AgreedPrice { get; private set; }

    /// <summary>The active assignment while the shipment is Assigned/PickedUp/InTransit/etc.</summary>
    public Guid? CurrentAssignmentId { get; private set; }

    public IReadOnlyList<ShipmentStop> Stops => _stops;

    public IReadOnlyList<Quote> Quotes => _quotes;

    public IReadOnlyList<ShipmentEvent> Events => _events;

    public ShipmentStop PickupStop => _stops[0];

    public ShipmentStop DeliveryStop => _stops[^1];

    public bool IsOwnedBy(Guid userId) => userId != Guid.Empty && userId == CustomerId;

    // ---------------------------------------------------------------- creation

    public static Shipment Create(
        Guid customerId,
        Cargo cargo,
        IReadOnlyList<StopInfo> stops,
        DateTime nowUtc,
        string? reference = null)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count < MinStops || stops.Count > MaxStops)
        {
            throw Guard.Invalid($"A shipment needs between {MinStops} and {MaxStops} stops.");
        }

        var shipment = new Shipment(Guid.NewGuid(), customerId, cargo, reference, nowUtc);

        for (var i = 0; i < stops.Count; i++)
        {
            var info = Guard.NotNull(stops[i], $"stops[{i}]");
            var type = i == 0 ? StopType.Pickup : i == stops.Count - 1 ? StopType.Delivery : StopType.Waypoint;

            if (type == StopType.Pickup && info.Window is null)
            {
                throw Guard.Invalid("The pickup stop must have a time window.");
            }

            shipment._stops.Add(new ShipmentStop(i + 1, type, info.Address, info.Location, info.Window));
        }

        shipment.AddEvent(customerId, previous: null, ShipmentStatus.Pending, nowUtc, null);
        return shipment;
    }

    // ---------------------------------------------------------------- quoting

    /// <summary>Customer opens the shipment so transport companies can quote. Pending → Quoting.</summary>
    public void OpenForQuoting(Guid actorUserId, DateTime nowUtc)
    {
        EnsureOwner(actorUserId);
        TransitionTo(ShipmentStatus.Quoting, actorUserId, nowUtc);
    }

    /// <summary>
    /// A transport company submits a quote. <paramref name="companyId"/> must come from the authenticated
    /// company membership, never from request data.
    /// </summary>
    public Quote SubmitQuote(
        Guid companyId,
        Money price,
        DateTime expiresAtUtc,
        string terms,
        DateTime nowUtc)
    {
        Guard.NotEmpty(companyId, nameof(companyId));
        if (Status != ShipmentStatus.Quoting)
        {
            throw new DomainException(
                DomainErrorCodes.QuoteRule,
                $"Quotes can only be submitted while the shipment is Quoting (current: {Status}).");
        }

        ExpireOverdueQuotes(nowUtc);

        if (_quotes.Any(q => q.CompanyId == companyId && q.Status == QuoteStatus.Submitted))
        {
            throw new DomainException(
                DomainErrorCodes.QuoteRule,
                "This company already has an active quote on the shipment. Withdraw it before submitting a new one.");
        }

        var quote = new Quote(Id, companyId, price, expiresAtUtc, terms, nowUtc);
        _quotes.Add(quote);
        UpdatedAtUtc = nowUtc;
        return quote;
    }

    /// <summary>The quoting company withdraws its own submitted quote.</summary>
    public void WithdrawQuote(Guid companyId, Guid quoteId, DateTime nowUtc)
    {
        var quote = GetQuote(quoteId);
        if (quote.CompanyId != companyId)
        {
            throw new DomainException(DomainErrorCodes.NotOwner, "The quote belongs to another company.");
        }

        quote.Withdraw(nowUtc);
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Marks every submitted quote whose expiry has passed as Expired. Returns how many changed.</summary>
    public int ExpireOverdueQuotes(DateTime nowUtc)
    {
        var overdue = _quotes.Where(q => q.Status == QuoteStatus.Submitted && q.IsExpiredAt(nowUtc)).ToList();
        foreach (var quote in overdue)
        {
            quote.Expire(nowUtc);
        }

        if (overdue.Count > 0)
        {
            UpdatedAtUtc = nowUtc;
        }

        return overdue.Count;
    }

    /// <summary>
    /// The owning customer accepts one submitted, unexpired quote. All competing submitted quotes are rejected,
    /// the carrier and agreed price are fixed from the quote, and the shipment becomes Accepted. This single
    /// aggregate method is what guarantees one accepted quote per shipment (persistence adds a concurrency token).
    /// </summary>
    public void AcceptQuote(Guid actorUserId, Guid quoteId, DateTime nowUtc)
    {
        EnsureOwner(actorUserId);
        if (Status != ShipmentStatus.Quoting)
        {
            throw new DomainException(
                DomainErrorCodes.QuoteRule,
                $"A quote can only be accepted while the shipment is Quoting (current: {Status}).");
        }

        var quote = GetQuote(quoteId);
        if (quote.Status == QuoteStatus.Submitted && quote.IsExpiredAt(nowUtc))
        {
            throw new DomainException(DomainErrorCodes.QuoteRule, "The quote has expired.");
        }

        if (quote.Status != QuoteStatus.Submitted)
        {
            throw new DomainException(
                DomainErrorCodes.QuoteRule,
                $"Only a submitted quote can be accepted; this quote is {quote.Status}.");
        }

        quote.Accept(nowUtc);
        foreach (var other in _quotes.Where(q => q.Id != quote.Id && q.Status == QuoteStatus.Submitted))
        {
            other.Reject(nowUtc);
        }

        CarrierCompanyId = quote.CompanyId;
        AcceptedQuoteId = quote.Id;
        AgreedPrice = quote.Price;

        TransitionTo(
            ShipmentStatus.Accepted,
            actorUserId,
            nowUtc,
            new Dictionary<string, string>
            {
                ["quoteId"] = quote.Id.ToString(),
                ["carrierCompanyId"] = quote.CompanyId.ToString(),
                ["price"] = quote.Price.ToString()
            });
    }

    // ---------------------------------------------------------------- assignment (via Assignment aggregate)

    /// <summary>Accepted → Assigned. Internal: callers must go through <c>Assignment.Create</c>.</summary>
    internal void MarkAssigned(Guid assignmentId, Guid dispatcherUserId, DateTime nowUtc)
    {
        Guard.NotEmpty(assignmentId, nameof(assignmentId));
        TransitionTo(
            ShipmentStatus.Assigned,
            dispatcherUserId,
            nowUtc,
            new Dictionary<string, string> { ["assignmentId"] = assignmentId.ToString() });
        CurrentAssignmentId = assignmentId;
    }

    /// <summary>Assigned → Accepted so a different driver/vehicle can be assigned. Internal: use <c>Assignment.UnassignFrom</c>.</summary>
    internal void Unassign(Guid actorUserId, string reason, DateTime nowUtc)
    {
        var text = Guard.NotBlank(reason, nameof(reason), MaxReasonLength);
        var previousAssignment = CurrentAssignmentId;
        TransitionTo(
            ShipmentStatus.Accepted,
            actorUserId,
            nowUtc,
            new Dictionary<string, string>
            {
                ["reason"] = text,
                ["assignmentId"] = previousAssignment?.ToString() ?? string.Empty
            });
        CurrentAssignmentId = null;
    }

    // ---------------------------------------------------------------- delivery progress

    // Note: the domain records who acted but does not decide whether that user is the assigned driver;
    // the Application layer authorizes this using the Assignment (Phase 5).

    /// <summary>Assigned → PickedUp.</summary>
    public void PickUp(Guid actorUserId, DateTime nowUtc) =>
        TransitionTo(ShipmentStatus.PickedUp, actorUserId, nowUtc);

    /// <summary>PickedUp → InTransit.</summary>
    public void StartTransit(Guid actorUserId, DateTime nowUtc)
    {
        // InTransit is reachable from PickedUp and DeliveryFailed; each has its own named method so the
        // audit trail says what actually happened.
        RequireCurrentStatus(ShipmentStatus.PickedUp, ShipmentStatus.InTransit);
        TransitionTo(ShipmentStatus.InTransit, actorUserId, nowUtc);
    }

    /// <summary>InTransit → Delivered.</summary>
    public void Deliver(Guid actorUserId, DateTime nowUtc) =>
        TransitionTo(ShipmentStatus.Delivered, actorUserId, nowUtc);

    /// <summary>PickedUp | InTransit → DeliveryFailed. A reason is required.</summary>
    public void FailDelivery(Guid actorUserId, string reason, DateTime nowUtc)
    {
        var text = Guard.NotBlank(reason, nameof(reason), MaxReasonLength);
        TransitionTo(
            ShipmentStatus.DeliveryFailed,
            actorUserId,
            nowUtc,
            new Dictionary<string, string> { ["reason"] = text });
    }

    /// <summary>DeliveryFailed → InTransit (another delivery attempt).</summary>
    public void RetryDelivery(Guid actorUserId, DateTime nowUtc)
    {
        RequireCurrentStatus(ShipmentStatus.DeliveryFailed, ShipmentStatus.InTransit);
        TransitionTo(ShipmentStatus.InTransit, actorUserId, nowUtc);
    }

    // ---------------------------------------------------------------- cancellation

    /// <summary>
    /// Cancels the shipment. <paramref name="initiatorId"/> is the customer user id for
    /// <see cref="CancellationInitiator.Customer"/>, the company id for <see cref="CancellationInitiator.Carrier"/>,
    /// and the admin user id for <see cref="CancellationInitiator.Platform"/> (authorized by the Application layer).
    /// </summary>
    public void Cancel(CancellationInitiator initiator, Guid initiatorId, string reason, DateTime nowUtc)
    {
        var text = Guard.NotBlank(reason, nameof(reason), MaxReasonLength);
        Guard.NotEmpty(initiatorId, nameof(initiatorId));

        switch (initiator)
        {
            case CancellationInitiator.Customer:
                EnsureOwner(initiatorId);
                RequireStatusIn(initiator, ShipmentStatus.Pending, ShipmentStatus.Quoting, ShipmentStatus.Accepted, ShipmentStatus.Assigned);
                break;
            case CancellationInitiator.Carrier:
                if (CarrierCompanyId is null || CarrierCompanyId != initiatorId)
                {
                    throw new DomainException(DomainErrorCodes.NotOwner, "Only the accepted carrier can cancel as a carrier.");
                }

                RequireStatusIn(initiator, ShipmentStatus.Accepted, ShipmentStatus.Assigned, ShipmentStatus.DeliveryFailed);
                break;
            case CancellationInitiator.Platform:
                break;
            default:
                throw Guard.Invalid("Unknown cancellation initiator.");
        }

        var metadata = new Dictionary<string, string>
        {
            ["reason"] = text,
            ["initiator"] = initiator.ToString()
        };
        if (CurrentAssignmentId is { } assignmentId)
        {
            metadata["assignmentId"] = assignmentId.ToString();
        }

        // Validate the transition before touching quote state so a rejected cancel leaves the aggregate unchanged.
        EnsureTransitionAllowed(ShipmentStatus.Cancelled);

        foreach (var quote in _quotes.Where(q => q.Status == QuoteStatus.Submitted))
        {
            quote.Reject(nowUtc);
        }

        TransitionTo(ShipmentStatus.Cancelled, initiatorId, nowUtc, metadata);
        CurrentAssignmentId = null;
    }

    // ---------------------------------------------------------------- helpers

    private void RequireStatusIn(CancellationInitiator initiator, params ShipmentStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException(
                DomainErrorCodes.InvalidState,
                $"A {initiator} cannot cancel a shipment that is {Status}.");
        }
    }

    private void EnsureOwner(Guid actorUserId)
    {
        if (!IsOwnedBy(actorUserId))
        {
            throw new DomainException(DomainErrorCodes.NotOwner, "Only the shipment's customer can perform this action.");
        }
    }

    private void RequireCurrentStatus(ShipmentStatus required, ShipmentStatus to)
    {
        if (Status != required)
        {
            throw new DomainException(
                DomainErrorCodes.InvalidTransition,
                $"A shipment cannot move from {Status} to {to} this way; it must be {required}.");
        }
    }

    private void EnsureTransitionAllowed(ShipmentStatus to)
    {
        if (!ShipmentLifecycle.CanTransition(Status, to))
        {
            throw new DomainException(
                DomainErrorCodes.InvalidTransition,
                $"A shipment cannot move from {Status} to {to}.");
        }
    }

    private void TransitionTo(
        ShipmentStatus to,
        Guid actorId,
        DateTime nowUtc,
        IDictionary<string, string>? metadata = null)
    {
        EnsureTransitionAllowed(to);
        var previous = Status;
        Status = to;
        UpdatedAtUtc = nowUtc;
        AddEvent(actorId, previous, to, nowUtc, metadata);
    }

    private void AddEvent(
        Guid actorId,
        ShipmentStatus? previous,
        ShipmentStatus next,
        DateTime nowUtc,
        IDictionary<string, string>? metadata) =>
        _events.Add(new ShipmentEvent(Id, nowUtc, actorId, previous, next, metadata));

    private Quote GetQuote(Guid quoteId) =>
        _quotes.FirstOrDefault(q => q.Id == quoteId)
        ?? throw new DomainException(DomainErrorCodes.NotFound, "Quote not found on this shipment.");
}
