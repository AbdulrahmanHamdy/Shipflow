using ShipFlow.Domain.Common;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.Tests.Support;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Tests;

public class QuoteSubmissionTests
{
    [Fact]
    public void OpenForQuoting_ByOwner_MovesToQuotingAndRecordsEvent()
    {
        var f = new Fixture();

        f.Shipment.OpenForQuoting(f.CustomerId, Fixture.T(1));

        Assert.Equal(ShipmentStatus.Quoting, f.Shipment.Status);
        var last = f.Shipment.Events[^1];
        Assert.Equal(ShipmentStatus.Pending, last.PreviousStatus);
        Assert.Equal(ShipmentStatus.Quoting, last.NewStatus);
        Assert.Equal(f.CustomerId, last.ActorId);
        Assert.Equal(Fixture.T(1), last.OccurredAtUtc);
    }

    [Fact]
    public void OpenForQuoting_ByAnotherUser_IsRejected()
    {
        var f = new Fixture();

        DomainAssert.Fails(DomainErrorCodes.NotOwner, () => f.Shipment.OpenForQuoting(Guid.NewGuid(), Fixture.T(1)));
        Assert.Equal(ShipmentStatus.Pending, f.Shipment.Status);
    }

    [Fact]
    public void OpenForQuoting_Twice_IsAnInvalidTransition()
    {
        var f = new Fixture(ShipmentStatus.Quoting);

        DomainAssert.Fails(DomainErrorCodes.InvalidTransition, () => f.Shipment.OpenForQuoting(f.CustomerId, Fixture.T(2)));
    }

    [Theory]
    [InlineData(ShipmentStatus.Pending)]
    [InlineData(ShipmentStatus.Accepted)]
    [InlineData(ShipmentStatus.Assigned)]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void SubmitQuote_OnlyWhileQuoting(ShipmentStatus status)
    {
        var f = new Fixture(status);

        DomainAssert.Fails(DomainErrorCodes.QuoteRule, () => f.SubmitQuote(Guid.NewGuid()));
    }

    [Fact]
    public void SubmitQuote_CreatesSubmittedQuoteWithPriceCurrencyExpiryAndTerms()
    {
        var f = new Fixture(ShipmentStatus.Quoting);

        var quote = f.SubmitQuote(price: 199.99m, expiresInMinutes: 600);

        Assert.Equal(QuoteStatus.Submitted, quote.Status);
        Assert.Equal(f.Company.Id, quote.CompanyId);
        Assert.Equal(Money.Of(199.99m, "USD"), quote.Price);
        Assert.Equal(Fixture.T(600), quote.ExpiresAtUtc);
        Assert.Equal("Net 30", quote.Terms);
        Assert.Single(f.Shipment.Quotes);
    }

    [Fact]
    public void SubmitQuote_RejectsZeroPrice()
    {
        var f = new Fixture(ShipmentStatus.Quoting);

        DomainAssert.Fails(DomainErrorCodes.QuoteRule, () => f.SubmitQuote(price: 0m));
    }

    [Fact]
    public void SubmitQuote_RejectsExpiryNotInTheFuture()
    {
        var f = new Fixture(ShipmentStatus.Quoting);

        DomainAssert.Fails(DomainErrorCodes.QuoteRule, () =>
            f.Shipment.SubmitQuote(f.Company.Id, Money.Of(10m, "USD"), Fixture.T(1), "terms", Fixture.T(1)));
    }

    [Fact]
    public void SubmitQuote_RejectsBlankTermsAndEmptyCompany()
    {
        var f = new Fixture(ShipmentStatus.Quoting);

        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            f.Shipment.SubmitQuote(f.Company.Id, Money.Of(10m, "USD"), Fixture.T(100), " ", Fixture.T(1)));
        DomainAssert.Fails(DomainErrorCodes.Validation, () =>
            f.Shipment.SubmitQuote(Guid.Empty, Money.Of(10m, "USD"), Fixture.T(100), "terms", Fixture.T(1)));
    }

    [Fact]
    public void SubmitQuote_SecondActiveQuoteFromSameCompany_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        f.SubmitQuote();

        DomainAssert.Fails(DomainErrorCodes.QuoteRule, () => f.SubmitQuote());
    }

    [Fact]
    public void SubmitQuote_DifferentCompaniesMayEachQuote()
    {
        var f = new Fixture(ShipmentStatus.Quoting);

        f.SubmitQuote(Guid.NewGuid());
        f.SubmitQuote(Guid.NewGuid());

        Assert.Equal(2, f.Shipment.Quotes.Count);
    }

    [Fact]
    public void SubmitQuote_AfterWithdrawing_IsAllowed()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var first = f.SubmitQuote();
        f.Shipment.WithdrawQuote(f.Company.Id, first.Id, Fixture.T(2));

        var second = f.SubmitQuote(price: 300m);

        Assert.Equal(QuoteStatus.Withdrawn, first.Status);
        Assert.Equal(QuoteStatus.Submitted, second.Status);
    }

    [Fact]
    public void SubmitQuote_AfterPreviousQuoteExpired_IsAllowed()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var first = f.SubmitQuote(expiresInMinutes: 10);

        var second = f.Shipment.SubmitQuote(f.Company.Id, Money.Of(100m, "USD"), Fixture.T(500), "terms", Fixture.T(20));

        Assert.Equal(QuoteStatus.Expired, first.Status);
        Assert.Equal(QuoteStatus.Submitted, second.Status);
    }

    [Fact]
    public void WithdrawQuote_ByAnotherCompany_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var quote = f.SubmitQuote();

        DomainAssert.Fails(DomainErrorCodes.NotOwner, () => f.Shipment.WithdrawQuote(Guid.NewGuid(), quote.Id, Fixture.T(2)));
        Assert.Equal(QuoteStatus.Submitted, quote.Status);
    }

    [Fact]
    public void WithdrawQuote_AlreadyWithdrawn_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var quote = f.SubmitQuote();
        f.Shipment.WithdrawQuote(f.Company.Id, quote.Id, Fixture.T(2));

        DomainAssert.Fails(DomainErrorCodes.QuoteRule, () => f.Shipment.WithdrawQuote(f.Company.Id, quote.Id, Fixture.T(3)));
    }

    [Fact]
    public void ExpireOverdueQuotes_MarksOnlyOverdueSubmittedQuotes()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var soon = f.SubmitQuote(Guid.NewGuid(), expiresInMinutes: 10);
        var later = f.SubmitQuote(Guid.NewGuid(), expiresInMinutes: 1000);

        var count = f.Shipment.ExpireOverdueQuotes(Fixture.T(50));

        Assert.Equal(1, count);
        Assert.Equal(QuoteStatus.Expired, soon.Status);
        Assert.Equal(QuoteStatus.Submitted, later.Status);
    }
}

public class QuoteAcceptanceTests
{
    [Fact]
    public void AcceptQuote_FixesCarrierAndPriceFromTheQuote_AndRejectsCompetitors()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var competitor = f.SubmitQuote(Guid.NewGuid(), 150m);
        var chosen = f.SubmitQuote(f.Company.Id, 250m);

        f.Shipment.AcceptQuote(f.CustomerId, chosen.Id, Fixture.T(5));

        Assert.Equal(ShipmentStatus.Accepted, f.Shipment.Status);
        Assert.Equal(f.Company.Id, f.Shipment.CarrierCompanyId);
        Assert.Equal(chosen.Id, f.Shipment.AcceptedQuoteId);
        Assert.Equal(Money.Of(250m, "USD"), f.Shipment.AgreedPrice);
        Assert.Equal(QuoteStatus.Accepted, chosen.Status);
        Assert.Equal(QuoteStatus.Rejected, competitor.Status);
        Assert.Equal(1, f.Shipment.Quotes.Count(q => q.Status == QuoteStatus.Accepted));
    }

    [Fact]
    public void AcceptQuote_RecordsEventWithQuoteMetadata()
    {
        var f = new Fixture(ShipmentStatus.Accepted);

        var evt = f.Shipment.Events[^1];

        Assert.Equal(ShipmentStatus.Quoting, evt.PreviousStatus);
        Assert.Equal(ShipmentStatus.Accepted, evt.NewStatus);
        Assert.Equal(f.CustomerId, evt.ActorId);
        Assert.Equal(f.AcceptedQuote!.Id.ToString(), evt.Metadata["quoteId"]);
        Assert.Equal(f.Company.Id.ToString(), evt.Metadata["carrierCompanyId"]);
    }

    [Fact]
    public void AcceptQuote_ByNonOwner_IsRejected_AndNothingChanges()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var quote = f.SubmitQuote();

        DomainAssert.Fails(DomainErrorCodes.NotOwner, () => f.Shipment.AcceptQuote(Guid.NewGuid(), quote.Id, Fixture.T(5)));

        Assert.Equal(ShipmentStatus.Quoting, f.Shipment.Status);
        Assert.Equal(QuoteStatus.Submitted, quote.Status);
        Assert.Null(f.Shipment.CarrierCompanyId);
    }

    [Fact]
    public void AcceptQuote_SecondAcceptanceOnSameShipment_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var a = f.SubmitQuote(Guid.NewGuid(), 100m);
        var b = f.SubmitQuote(Guid.NewGuid(), 90m);
        f.Shipment.AcceptQuote(f.CustomerId, a.Id, Fixture.T(5));

        DomainAssert.Fails(DomainErrorCodes.QuoteRule, () => f.Shipment.AcceptQuote(f.CustomerId, b.Id, Fixture.T(6)));

        Assert.Equal(a.Id, f.Shipment.AcceptedQuoteId);
        Assert.Equal(1, f.Shipment.Quotes.Count(q => q.Status == QuoteStatus.Accepted));
    }

    [Fact]
    public void AcceptQuote_ExpiredQuote_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var quote = f.SubmitQuote(expiresInMinutes: 10);

        DomainAssert.Fails(DomainErrorCodes.QuoteRule, () => f.Shipment.AcceptQuote(f.CustomerId, quote.Id, Fixture.T(10)));
        Assert.Equal(ShipmentStatus.Quoting, f.Shipment.Status);
    }

    [Fact]
    public void AcceptQuote_WithdrawnQuote_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        var quote = f.SubmitQuote();
        f.Shipment.WithdrawQuote(f.Company.Id, quote.Id, Fixture.T(2));

        DomainAssert.Fails(DomainErrorCodes.QuoteRule, () => f.Shipment.AcceptQuote(f.CustomerId, quote.Id, Fixture.T(5)));
    }

    [Fact]
    public void AcceptQuote_UnknownQuote_IsNotFound()
    {
        var f = new Fixture(ShipmentStatus.Quoting);
        f.SubmitQuote();

        DomainAssert.Fails(DomainErrorCodes.NotFound, () => f.Shipment.AcceptQuote(f.CustomerId, Guid.NewGuid(), Fixture.T(5)));
    }

    [Fact]
    public void AcceptQuote_WhileNotQuoting_IsRejected()
    {
        var f = new Fixture(ShipmentStatus.Pending);

        DomainAssert.Fails(DomainErrorCodes.QuoteRule, () => f.Shipment.AcceptQuote(f.CustomerId, Guid.NewGuid(), Fixture.T(5)));
    }
}
