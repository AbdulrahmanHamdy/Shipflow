using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Features.Quotations;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Shipments;

namespace ShipFlow.Application.Tests.Shipments;

public class QuoteHandlerTests
{
    private static SubmitQuoteCommand Offer(World w, decimal amount = 250.5m, int hours = 4) =>
        new(amount, "usd", w.Time.Now.AddHours(hours), "Net 30");

    [Fact]
    public async Task Submit_ByADispatcher_AttributesTheQuoteToTheMembershipCompany()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        w.AsDispatcherA();

        var quote = await w.Submit().HandleAsync(shipment.Id, Offer(w), CancellationToken.None);

        Assert.Equal(w.CompanyA.Id, quote.CompanyId);
        Assert.Equal(250.5m, quote.Amount);
        Assert.Equal("USD", quote.Currency);
        Assert.Equal("Submitted", quote.Status);
        Assert.Single(shipment.Quotes);
        Assert.Equal(1, w.Uow.Saves);
    }

    [Fact]
    public async Task Submit_ByACustomerWithoutAMembership_IsForbidden()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        w.AsCustomer(w.OtherCustomer);

        await Assert.ThrowsAsync<ForbiddenException>(() => w.Submit().HandleAsync(shipment.Id, Offer(w), CancellationToken.None));
        Assert.Empty(shipment.Quotes);
    }

    [Fact]
    public async Task Submit_ByADriverMember_IsForbidden()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        w.User.Set(w.DriverA, ShipFlow.Application.Common.Security.Roles.Driver);

        await Assert.ThrowsAsync<ForbiddenException>(() => w.Submit().HandleAsync(shipment.Id, Offer(w), CancellationToken.None));
    }

    [Fact]
    public async Task Submit_OnAShipmentThatIsNotOpen_IsNotFoundForTheCompany()
    {
        var w = new World();
        var pending = w.AddShipment();
        w.AsDispatcherA();

        await Assert.ThrowsAsync<NotFoundException>(() => w.Submit().HandleAsync(pending.Id, Offer(w), CancellationToken.None));
    }

    [Fact]
    public async Task Submit_TwiceFromTheSameCompany_IsRejectedByTheDomain()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        w.AsDispatcherA();
        await w.Submit().HandleAsync(shipment.Id, Offer(w), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() => w.Submit().HandleAsync(shipment.Id, Offer(w), CancellationToken.None));

        Assert.Equal(DomainErrorCodes.QuoteRule, ex.Code);
        Assert.Single(shipment.Quotes);
    }

    [Fact]
    public async Task Submit_WithAnExpiryInThePast_IsRejectedByTheDomain()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        w.AsDispatcherA();

        await Assert.ThrowsAsync<DomainException>(() => w.Submit().HandleAsync(shipment.Id, Offer(w, hours: -1), CancellationToken.None));
    }

    [Fact]
    public async Task Withdraw_OwnQuote_Works_AndAnotherCompanysQuoteIsNotFound()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        var quoteA = w.AddQuote(shipment, w.CompanyA);

        w.AsDispatcherB();
        await Assert.ThrowsAsync<NotFoundException>(() => w.Withdraw().HandleAsync(shipment.Id, quoteA.Id, CancellationToken.None));
        Assert.Equal(QuoteStatus.Submitted, quoteA.Status);

        w.AsDispatcherA();
        var dto = await w.Withdraw().HandleAsync(shipment.Id, quoteA.Id, CancellationToken.None);
        Assert.Equal("Withdrawn", dto.Status);
    }

    [Fact]
    public async Task Quotes_OwnerSeesAll_CompanySeesOwn_StrangerSeesNothing()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        w.AddQuote(shipment, w.CompanyA);
        w.AddQuote(shipment, w.CompanyB);

        w.AsCustomer();
        Assert.Equal(2, (await w.Quotes().HandleAsync(shipment.Id, CancellationToken.None)).Count);
        w.AsDispatcherB();
        Assert.Equal(w.CompanyB.Id, Assert.Single(await w.Quotes().HandleAsync(shipment.Id, CancellationToken.None)).CompanyId);
        w.AsCustomer(w.OtherCustomer);
        await Assert.ThrowsAsync<NotFoundException>(() => w.Quotes().HandleAsync(shipment.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Accept_FixesCarrierAndPriceFromTheQuote_AndRejectsCompetitors()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        var winner = w.AddQuote(shipment, w.CompanyA, 100m);
        var loser = w.AddQuote(shipment, w.CompanyB, 90m);
        w.AsCustomer();

        var dto = await w.Accept().HandleAsync(shipment.Id, winner.Id, CancellationToken.None);

        Assert.Equal("Accepted", dto.Status);
        Assert.Equal(w.CompanyA.Id, dto.CarrierCompanyId);
        Assert.Equal(100m, dto.AgreedAmount);
        Assert.Equal(QuoteStatus.Accepted, winner.Status);
        Assert.Equal(QuoteStatus.Rejected, loser.Status);
        Assert.Equal(1, w.Uow.Saves);
    }

    [Fact]
    public async Task Accept_ByANonOwner_IsNotFound_AndNothingChanges()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        var quote = w.AddQuote(shipment, w.CompanyA);
        w.AsCustomer(w.OtherCustomer);

        await Assert.ThrowsAsync<NotFoundException>(() => w.Accept().HandleAsync(shipment.Id, quote.Id, CancellationToken.None));
        Assert.Equal(ShipmentStatus.Quoting, shipment.Status);
        Assert.Equal(QuoteStatus.Submitted, quote.Status);
    }

    [Fact]
    public async Task Accept_AnExpiredQuote_IsRejected()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        var quote = w.AddQuote(shipment, w.CompanyA, expiresInHours: 1);
        w.Time.Now = w.Time.Now.AddHours(2);
        w.AsCustomer();

        var ex = await Assert.ThrowsAsync<DomainException>(() => w.Accept().HandleAsync(shipment.Id, quote.Id, CancellationToken.None));

        Assert.Equal(DomainErrorCodes.QuoteRule, ex.Code);
        Assert.Equal(ShipmentStatus.Quoting, shipment.Status);
    }

    [Fact]
    public async Task Accept_AfterAnotherQuoteWasAccepted_IsRejected()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        var first = w.AddQuote(shipment, w.CompanyA);
        var second = w.AddQuote(shipment, w.CompanyB);
        w.AsCustomer();
        await w.Accept().HandleAsync(shipment.Id, first.Id, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() => w.Accept().HandleAsync(shipment.Id, second.Id, CancellationToken.None));

        Assert.Equal(DomainErrorCodes.QuoteRule, ex.Code);
        Assert.Equal(w.CompanyA.Id, shipment.CarrierCompanyId);
    }

    [Fact]
    public async Task Accept_WhenALosingRaceIsDetectedAtCommit_SurfacesAConcurrencyConflict()
    {
        var w = new World();
        var shipment = w.AddShipment(openForQuoting: true);
        var quote = w.AddQuote(shipment, w.CompanyA);
        w.Uow.FailWith = new ConcurrencyConflictException();
        w.AsCustomer();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => w.Accept().HandleAsync(shipment.Id, quote.Id, CancellationToken.None));
    }
}
