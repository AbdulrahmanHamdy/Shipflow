using ShipFlow.Domain.Shipments;

namespace ShipFlow.Application.Features.Shipments;

public sealed record CargoDto(string Description, decimal WeightKg, decimal VolumeM3);

public sealed record StopDto(
    Guid Id, int Sequence, string Type, string Address, double Latitude, double Longitude,
    DateTime? WindowStartUtc, DateTime? WindowEndUtc);

public sealed record QuoteDto(
    Guid Id, Guid ShipmentId, Guid CompanyId, decimal Amount, string Currency,
    DateTime ExpiresAtUtc, string Terms, string Status, DateTime CreatedAtUtc, DateTime? ResolvedAtUtc);

public sealed record ShipmentSummaryDto(
    Guid Id, string? Reference, ShipmentStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    string CargoDescription, decimal CargoWeightKg, decimal CargoVolumeM3,
    string PickupAddress, string DeliveryAddress,
    decimal? AgreedAmount, string? AgreedCurrency, Guid? CarrierCompanyId);

public sealed record ShipmentDetailsDto(
    Guid Id, string? Reference, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    CargoDto Cargo, IReadOnlyList<StopDto> Stops, decimal? AgreedAmount, string? AgreedCurrency,
    Guid? CarrierCompanyId, IReadOnlyList<QuoteDto> Quotes);

/// <summary>Who may see what. Built by the handler from the authenticated user, never from the request.</summary>
public sealed record ShipmentListFilter(
    Guid? CustomerId,
    Guid? CarrierCompanyId,
    bool OpenForQuotingOnly,
    IReadOnlyList<ShipmentStatus> Statuses,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    string? Search,
    int Page,
    int PageSize);

internal static class ShipmentMapping
{
    public static QuoteDto ToDto(this Quote q) =>
        new(q.Id, q.ShipmentId, q.CompanyId, q.Price.Amount, q.Price.Currency, q.ExpiresAtUtc, q.Terms,
            q.Status.ToString(), q.CreatedAtUtc, q.ResolvedAtUtc);

    public static ShipmentSummaryDto ToSummary(this Shipment s) =>
        new(s.Id, s.Reference, s.Status, s.CreatedAtUtc, s.UpdatedAtUtc,
            s.Cargo.Description, s.Cargo.WeightKg, s.Cargo.VolumeM3,
            s.Stops.OrderBy(x => x.Sequence).Select(x => x.Address).First(),
            s.Stops.OrderByDescending(x => x.Sequence).Select(x => x.Address).First(),
            s.AgreedPrice?.Amount, s.AgreedPrice?.Currency, s.CarrierCompanyId);

    public static ShipmentDetailsDto ToDetails(this Shipment s, Viewer viewer) =>
        new(s.Id, s.Reference, s.Status.ToString(), s.CreatedAtUtc, s.UpdatedAtUtc,
            new CargoDto(s.Cargo.Description, s.Cargo.WeightKg, s.Cargo.VolumeM3),
            s.Stops.OrderBy(x => x.Sequence)
                .Select(x => new StopDto(x.Id, x.Sequence, x.Type.ToString(), x.Address, x.Location.Latitude,
                    x.Location.Longitude, x.Window?.StartUtc, x.Window?.EndUtc))
                .ToList(),
            s.AgreedPrice?.Amount, s.AgreedPrice?.Currency, s.CarrierCompanyId,
            VisibleQuotes(s, viewer).Select(q => q.ToDto()).ToList());

    /// <summary>The shipment owner and platform admins see every quote; a company sees only its own.</summary>
    public static IEnumerable<Quote> VisibleQuotes(Shipment s, Viewer viewer) =>
        viewer.SeesAllQuotes(s) ? s.Quotes : s.Quotes.Where(q => q.CompanyId == viewer.CompanyId);
}
