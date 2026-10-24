using FluentValidation;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Validation;
using ShipFlow.Application.Features.Shipments;
using ShipFlow.Domain.Shipments;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Application.Features.Quotations;

/// <summary>The price is whatever the company states here; the company itself is never in the request.</summary>
public sealed record SubmitQuoteCommand(decimal Amount, string Currency, DateTime ExpiresAtUtc, string Terms);

public sealed class SubmitQuoteCommandValidator : AbstractValidator<SubmitQuoteCommand>
{
    public SubmitQuoteCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m)
            .Must(a => decimal.Round(a, Money.MaxDecimalPlaces) == a)
            .WithMessage($"Amount must have at most {Money.MaxDecimalPlaces} decimal places.");
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.ExpiresAtUtc).Must(d => d.Kind == DateTimeKind.Utc)
            .WithMessage("ExpiresAtUtc must be a UTC time (end with 'Z').");
        RuleFor(x => x.Terms).NotEmpty().MaximumLength(Quote.MaxTermsLength);
    }
}

public sealed class SubmitQuoteHandler
{
    private readonly IValidator<SubmitQuoteCommand> _validator;
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public SubmitQuoteHandler(
        IValidator<SubmitQuoteCommand> validator, ViewerResolver viewers, IShipmentStore shipments,
        IUnitOfWork unitOfWork, TimeProvider time)
    {
        _validator = validator;
        _viewers = viewers;
        _shipments = shipments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<QuoteDto> HandleAsync(Guid shipmentId, SubmitQuoteCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.EnsureValidAsync(_validator, command, cancellationToken);
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.CanManageQuotes)
        {
            throw new ForbiddenException("Only an administrator or dispatcher of an active transport company can submit quotes.");
        }

        var shipment = await _shipments.FindForUpdateAsync(shipmentId, cancellationToken);
        if (shipment is null || !viewer.CanView(shipment))
        {
            throw new NotFoundException("Shipment not found.");
        }

        // Eligibility (the shipment must be Quoting, one live quote per company, positive price, future expiry)
        // is enforced by the aggregate.
        var quote = shipment.SubmitQuote(
            viewer.CompanyId!.Value,
            Money.Of(command.Amount, command.Currency),
            command.ExpiresAtUtc,
            command.Terms,
            _time.GetUtcNow().UtcDateTime);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return quote.ToDto();
    }
}

public sealed class WithdrawQuoteHandler
{
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public WithdrawQuoteHandler(ViewerResolver viewers, IShipmentStore shipments, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _viewers = viewers;
        _shipments = shipments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<QuoteDto> HandleAsync(Guid shipmentId, Guid quoteId, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        if (!viewer.CanManageQuotes)
        {
            throw new ForbiddenException("Only an administrator or dispatcher of an active transport company can withdraw quotes.");
        }

        var shipment = await _shipments.FindForUpdateAsync(shipmentId, cancellationToken);
        var quote = shipment?.Quotes.FirstOrDefault(q => q.Id == quoteId && q.CompanyId == viewer.CompanyId);
        if (shipment is null || quote is null)
        {
            // Another company's quote looks exactly like a missing one.
            throw new NotFoundException("Quote not found.");
        }

        shipment.WithdrawQuote(viewer.CompanyId!.Value, quoteId, _time.GetUtcNow().UtcDateTime);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return quote.ToDto();
    }
}

public sealed class GetQuotesHandler
{
    private readonly ViewerResolver _viewers;
    private readonly IShipmentStore _shipments;

    public GetQuotesHandler(ViewerResolver viewers, IShipmentStore shipments)
    {
        _viewers = viewers;
        _shipments = shipments;
    }

    public async Task<IReadOnlyList<QuoteDto>> HandleAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);
        var shipment = await _shipments.FindAsync(shipmentId, cancellationToken);
        if (shipment is null || !viewer.CanView(shipment))
        {
            throw new NotFoundException("Shipment not found.");
        }

        return ShipmentMapping.VisibleQuotes(shipment, viewer).OrderBy(q => q.CreatedAtUtc).Select(q => q.ToDto()).ToList();
    }
}

public sealed class AcceptQuoteHandler
{
    private readonly ICurrentUser _user;
    private readonly IShipmentStore _shipments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public AcceptQuoteHandler(ICurrentUser user, IShipmentStore shipments, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _user = user;
        _shipments = shipments;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    /// <summary>
    /// Accepts one quote. Safe against two competing acceptances: the aggregate allows only one while it is Quoting,
    /// the rowversion on the shipment makes the second concurrent writer fail, and a filtered unique index allows at most one
    /// accepted quote per shipment. The loser gets 409 (<see cref="ConcurrencyConflictException"/>).
    /// </summary>
    public async Task<ShipmentDetailsDto> HandleAsync(Guid shipmentId, Guid quoteId, CancellationToken cancellationToken)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Authentication is required.");
        var shipment = await _shipments.FindForUpdateAsync(shipmentId, cancellationToken);
        if (shipment is null || !shipment.IsOwnedBy(userId))
        {
            throw new NotFoundException("Shipment not found.");
        }

        shipment.AcceptQuote(userId, quoteId, _time.GetUtcNow().UtcDateTime);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment.ToDetails(new Viewer(userId, false, null, null));
    }
}
