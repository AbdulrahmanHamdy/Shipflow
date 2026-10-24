using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Models;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Tracking;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Dispatching;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tracking;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Tests.Tracking;

internal sealed class FakeTrackingStore : ITrackingStore
{
    public List<TrackingPoint> Points { get; } = [];
    public Dictionary<Guid, DeliveryProof> Proofs { get; } = [];

    public Task AddPointAsync(TrackingPoint point, CancellationToken cancellationToken)
    {
        Points.Add(point);
        return Task.CompletedTask;
    }

    public Task<TrackingPoint?> FindLatestAsync(Guid shipmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Points.Where(p => p.ShipmentId == shipmentId).OrderByDescending(p => p.RecordedAtUtc).FirstOrDefault());

    public Task<PagedResult<TrackingPointDto>> ListAsync(TrackingListFilter filter, CancellationToken cancellationToken)
    {
        var query = Points.Where(p => p.ShipmentId == filter.ShipmentId);
        if (filter.FromUtc.HasValue) query = query.Where(p => p.RecordedAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) query = query.Where(p => p.RecordedAtUtc <= filter.ToUtc.Value);

        var list = query.OrderByDescending(p => p.RecordedAtUtc).ToList();
        var total = list.Count;
        var paged = list.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(p => new TrackingPointDto(p.Id, p.ShipmentId, p.Location.Latitude, p.Location.Longitude, p.RecordedAtUtc, p.SpeedKph))
            .ToList();

        return Task.FromResult(new PagedResult<TrackingPointDto>(paged, filter.Page, filter.PageSize, total));
    }

    public Task<bool> ExistsAsync(Guid shipmentId, DateTime recordedAtUtc, Guid recordedByUserId, CancellationToken cancellationToken) =>
        Task.FromResult(Points.Any(p => p.ShipmentId == shipmentId && p.RecordedAtUtc == recordedAtUtc && p.RecordedByUserId == recordedByUserId));

    public Task AddProofAsync(DeliveryProof proof, CancellationToken cancellationToken)
    {
        Proofs[proof.ShipmentId] = proof;
        return Task.CompletedTask;
    }

    public Task<DeliveryProof?> FindProofAsync(Guid shipmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Proofs.GetValueOrDefault(shipmentId));
}

internal sealed class FakeDriverAssignmentStore : IDriverAssignmentStore
{
    public List<Assignment> Assignments { get; } = [];

    public Task<Assignment?> FindCurrentForDriverAsync(Guid driverId, CancellationToken cancellationToken) =>
        Task.FromResult(Assignments.FirstOrDefault(a => a.DriverId == driverId && a.Status == AssignmentStatus.Active));

    public Task<PagedResult<AssignmentDto>> ListForDriverAsync(Guid driverId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var forDriver = Assignments.Where(a => a.DriverId == driverId).OrderByDescending(a => a.CreatedAtUtc).ToList();
        var total = forDriver.Count;
        var items = forDriver.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => a.ToDto(ShipmentStatus.Assigned))
            .ToList();
        return Task.FromResult(new PagedResult<AssignmentDto>(items, page, pageSize, total));
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> SentMessages { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        SentMessages.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FakePaymentService : IPaymentService
{
    public PaymentInitiationResult InitiationResult { get; set; } = new("mock_key", "https://example.com/iframe", "ORD-123");
    public PaymentVerificationResult VerificationResult { get; set; } = new(true, true, "TXN-123");

    public Task<PaymentInitiationResult> InitiatePaymentAsync(
        Guid shipmentId, decimal amountCents, string currency, string customerEmail, string customerName, CancellationToken cancellationToken) =>
        Task.FromResult(InitiationResult);

    public PaymentVerificationResult VerifyCallback(PaymentCallbackData data) => VerificationResult;
}
