using Microsoft.Extensions.Logging.Abstractions;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Payments;
using ShipFlow.Application.Tests.Shipments;
using ShipFlow.Application.Tests.Tracking;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Tests.Payments;

public class PaymentHandlerTests
{
    private readonly FakeClock _time = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeShipmentStore _shipments = new();
    private readonly FakePaymentService _paymentService = new();
    private readonly FakeEmailSender _emailSender = new();

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Shipment _shipment;

    public PaymentHandlerTests()
    {
        var stops = new[]
        {
            new StopInfo("Pickup", GeoPoint.Of(30.0, 31.0), TimeWindow.Of(_time.Now.AddHours(1), _time.Now.AddHours(3))),
            new StopInfo("Drop", GeoPoint.Of(30.5, 31.5))
        };
        _shipment = Shipment.Create(_customerId, Cargo.Of("Goods", 500m, 2m), stops, _time.Now, "REF-100");
        _shipment.OpenForQuoting(_customerId, _time.Now);
        var quote = _shipment.SubmitQuote(Guid.NewGuid(), Money.Of(250.50m, "USD"), _time.Now.AddDays(1), "Net 30", _time.Now);
        _shipment.AcceptQuote(_customerId, quote.Id, _time.Now);
        _shipments.AddAsync(_shipment, CancellationToken.None).Wait();

        _currentUser.Set(_customerId, Roles.Customer);
    }

    [Fact]
    public async Task InitiatePayment_OwnerCustomer_ReturnsIframeUrlAndPaymentKey()
    {
        var validator = new InitiatePaymentCommandValidator();
        var handler = new InitiatePaymentHandler(validator, _currentUser, _shipments, _paymentService);

        var result = await handler.HandleAsync(new InitiatePaymentCommand(_shipment.Id), CancellationToken.None);

        Assert.Equal(_shipment.Id, result.ShipmentId);
        Assert.Equal("mock_key", result.PaymentKey);
        Assert.Equal(250.50m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public async Task InitiatePayment_NonOwner_ThrowsNotFound()
    {
        _currentUser.Set(Guid.NewGuid(), Roles.Customer);
        var validator = new InitiatePaymentCommandValidator();
        var handler = new InitiatePaymentHandler(validator, _currentUser, _shipments, _paymentService);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new InitiatePaymentCommand(_shipment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ProcessCallback_ValidHmacAndSuccess_SendsConfirmationEmail()
    {
        var handler = new ProcessPaymentCallbackHandler(
            _paymentService, _emailSender, NullLogger<ProcessPaymentCallbackHandler>.Instance);

        var fields = new Dictionary<string, string> { ["shipping_data.email"] = "buyer@example.com" };
        var callbackData = new Common.Interfaces.PaymentCallbackData("TXN-1", "ORD-1", true, 25050m, "valid_hmac", fields);

        var result = await handler.HandleAsync(callbackData, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(_emailSender.SentMessages);
        Assert.Equal("buyer@example.com", _emailSender.SentMessages[0].To);
    }

    [Fact]
    public async Task ProcessCallback_InvalidHmac_ThrowsForbidden()
    {
        _paymentService.VerificationResult = new Common.Interfaces.PaymentVerificationResult(false, false, "TXN-1");

        var handler = new ProcessPaymentCallbackHandler(
            _paymentService, _emailSender, NullLogger<ProcessPaymentCallbackHandler>.Instance);

        var callbackData = new Common.Interfaces.PaymentCallbackData("TXN-1", "ORD-1", true, 25050m, "bad_hmac", new Dictionary<string, string>());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(callbackData, CancellationToken.None));
    }
}
