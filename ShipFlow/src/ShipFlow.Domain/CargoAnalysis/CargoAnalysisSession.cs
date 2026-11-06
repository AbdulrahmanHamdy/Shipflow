using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.CargoAnalysis;

public sealed class CargoAnalysisSession : Entity
{
    private readonly List<CargoItem> _items = [];

    private CargoAnalysisSession()
    {
    }

    private CargoAnalysisSession(Guid id, Guid customerId, DateTime nowUtc)
        : base(id)
    {
        CustomerId = Guard.NotEmpty(customerId, nameof(customerId));
        CreatedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = nowUtc;
        Status = CargoAnalysisStatus.Analyzed;
    }

    public Guid CustomerId { get; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }
    public CargoAnalysisStatus Status { get; private set; }
    public decimal TotalWeightKg { get; private set; }
    public decimal TotalOriginalVolumeM3 { get; private set; }
    public decimal TotalPackedVolumeM3 { get; private set; }
    public IReadOnlyList<CargoItem> Items => _items;

    public static CargoAnalysisSession Create(
        Guid customerId,
        IEnumerable<CargoItem> items,
        DateTime nowUtc)
    {
        var session = new CargoAnalysisSession(Guid.NewGuid(), customerId, nowUtc);
        foreach (var item in items)
        {
            session._items.Add(item);
        }
        session.RecalculateTotals(nowUtc);
        return session;
    }

    public void ReplaceItems(IEnumerable<CargoItem> newItems, DateTime nowUtc)
    {
        _items.Clear();
        foreach (var item in newItems)
        {
            _items.Add(item);
        }
        RecalculateTotals(nowUtc);
    }

    public void Confirm(DateTime nowUtc)
    {
        Status = CargoAnalysisStatus.Confirmed;
        UpdatedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
    }

    public void RecalculateTotals(DateTime nowUtc)
    {
        TotalWeightKg = _items.Sum(i => i.PackedWeightKg * i.Quantity);
        TotalOriginalVolumeM3 = _items.Sum(i => i.OriginalVolumeM3 * i.Quantity);
        TotalPackedVolumeM3 = _items.Sum(i => i.PackedVolumeM3 * i.Quantity);
        UpdatedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
    }
}
