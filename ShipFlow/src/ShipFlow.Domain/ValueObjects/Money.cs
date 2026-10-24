using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.ValueObjects;

/// <summary>A non-negative monetary amount (decimal, max 4 decimal places) in an ISO 4217 currency.</summary>
public sealed record Money
{
    public const int MaxDecimalPlaces = 4;

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Of(decimal amount, string currency)
    {
        if (amount < 0m)
        {
            throw Guard.Invalid("Amount must not be negative.");
        }

        if (Math.Round(amount, MaxDecimalPlaces) != amount)
        {
            throw Guard.Invalid($"Amount must have at most {MaxDecimalPlaces} decimal places.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw Guard.Invalid("Currency is required.");
        }

        var code = currency.Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(c => c is >= 'A' and <= 'Z'))
        {
            throw Guard.Invalid("Currency must be a three-letter ISO 4217 code.");
        }

        return new Money(amount, code);
    }

    public bool IsPositive => Amount > 0m;

    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);
        return Of(Amount + other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw Guard.Invalid("Cannot combine amounts in different currencies.");
        }
    }

    public override string ToString() => $"{Amount} {Currency}";
}
