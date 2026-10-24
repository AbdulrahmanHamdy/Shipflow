using ShipFlow.Domain.Common;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Shipments;

public enum QuoteStatus
{
    Submitted = 0,
    Accepted = 1,
    Rejected = 2,
    Withdrawn = 3,
    Expired = 4
}

/// <summary>
/// A transport company's price offer for a shipment. Part of the <see cref="Shipment"/> aggregate:
/// it is created and its status changed only through the shipment.
/// </summary>
public sealed class Quote : Entity
{
    public const int MaxTermsLength = 2000;

    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    private Quote()
    {
        Price = null!;
                Terms = null!;
    }

    internal Quote(
        Guid shipmentId,
        Guid companyId,
        Money price,
        DateTime expiresAtUtc,
        string terms,
        DateTime nowUtc)
        : base(Guid.NewGuid())
    {
        ShipmentId = Guard.NotEmpty(shipmentId, nameof(shipmentId));
        CompanyId = Guard.NotEmpty(companyId, nameof(companyId));
        Price = Guard.NotNull(price, nameof(price));
        if (!price.IsPositive)
        {
            throw new DomainException(DomainErrorCodes.QuoteRule, "Quote price must be greater than zero.");
        }

        CreatedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
        ExpiresAtUtc = Guard.Utc(expiresAtUtc, nameof(expiresAtUtc));
        if (expiresAtUtc <= nowUtc)
        {
            throw new DomainException(DomainErrorCodes.QuoteRule, "Quote expiration must be in the future.");
        }

        Terms = Guard.NotBlank(terms, nameof(terms), MaxTermsLength);
        Status = QuoteStatus.Submitted;
    }

    public Guid ShipmentId { get; }

    public Guid CompanyId { get; }

    public Money Price { get; }

    public DateTime ExpiresAtUtc { get; }

    public string Terms { get; }

    public QuoteStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public bool IsExpiredAt(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    internal void Accept(DateTime nowUtc) => Resolve(QuoteStatus.Accepted, nowUtc);

    internal void Reject(DateTime nowUtc) => Resolve(QuoteStatus.Rejected, nowUtc);

    internal void Withdraw(DateTime nowUtc) => Resolve(QuoteStatus.Withdrawn, nowUtc);

    internal void Expire(DateTime nowUtc) => Resolve(QuoteStatus.Expired, nowUtc);

    private void Resolve(QuoteStatus newStatus, DateTime nowUtc)
    {
        if (Status != QuoteStatus.Submitted)
        {
            throw new DomainException(
                DomainErrorCodes.QuoteRule,
                $"Only a submitted quote can become {newStatus}; this quote is {Status}.");
        }

        Status = newStatus;
        ResolvedAtUtc = Guard.Utc(nowUtc, nameof(nowUtc));
    }
}
