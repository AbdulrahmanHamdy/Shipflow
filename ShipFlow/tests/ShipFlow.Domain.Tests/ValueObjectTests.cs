using ShipFlow.Domain.Common;
using ShipFlow.Domain.Tests.Support;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Of_NormalizesCurrencyToUpperCase()
    {
        var money = Money.Of(10.5m, " usd ");

        Assert.Equal("USD", money.Currency);
        Assert.Equal(10.5m, money.Amount);
    }

    [Fact]
    public void Of_RejectsNegativeAmount() =>
        DomainAssert.Fails(DomainErrorCodes.Validation, () => Money.Of(-0.01m, "USD"));

    [Fact]
    public void Of_RejectsMoreThanFourDecimalPlaces() =>
        DomainAssert.Fails(DomainErrorCodes.Validation, () => Money.Of(1.00001m, "USD"));

    [Fact]
    public void Of_AcceptsFourDecimalPlaces() => Assert.Equal(1.2345m, Money.Of(1.2345m, "USD").Amount);

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDX")]
    [InlineData("U5D")]
    public void Of_RejectsInvalidCurrency(string currency) =>
        DomainAssert.Fails(DomainErrorCodes.Validation, () => Money.Of(1m, currency));

    [Fact]
    public void Add_SameCurrency_Sums() => Assert.Equal(Money.Of(7m, "EUR"), Money.Of(3m, "EUR").Add(Money.Of(4m, "EUR")));

    [Fact]
    public void Add_DifferentCurrency_IsRejected() =>
        DomainAssert.Fails(DomainErrorCodes.Validation, () => Money.Of(3m, "EUR").Add(Money.Of(4m, "USD")));

    [Fact]
    public void IsPositive_IsFalseForZero()
    {
        Assert.False(Money.Of(0m, "USD").IsPositive);
        Assert.True(Money.Of(0.01m, "USD").IsPositive);
    }

    [Fact]
    public void Equality_IsByValue() => Assert.Equal(Money.Of(1m, "USD"), Money.Of(1m, "usd"));
}

public class GeoPointTests
{
    [Fact]
    public void Of_AcceptsBoundaries()
    {
        Assert.NotNull(GeoPoint.Of(90, 180));
        Assert.NotNull(GeoPoint.Of(-90, -180));
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    [InlineData(double.NaN, 0)]
    public void Of_RejectsOutOfRange(double lat, double lon) =>
        DomainAssert.Fails(DomainErrorCodes.Validation, () => GeoPoint.Of(lat, lon));
}

public class TimeWindowTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static TimeWindow W(int fromHour, int toHour) =>
        TimeWindow.Of(Start.AddHours(fromHour), Start.AddHours(toHour));

    [Fact]
    public void Of_RejectsEndNotAfterStart()
    {
        DomainAssert.Fails(DomainErrorCodes.Validation, () => TimeWindow.Of(Start, Start));
        DomainAssert.Fails(DomainErrorCodes.Validation, () => TimeWindow.Of(Start, Start.AddMinutes(-1)));
    }

    [Fact]
    public void Of_RejectsNonUtcTimes()
    {
        var local = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
        var unspecified = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);

        DomainAssert.Fails(DomainErrorCodes.Validation, () => TimeWindow.Of(local, Start.AddHours(5)));
        DomainAssert.Fails(DomainErrorCodes.Validation, () => TimeWindow.Of(Start, unspecified.AddHours(5)));
    }

    [Theory]
    [InlineData(0, 4, 2, 6, true)]   // partial overlap
    [InlineData(0, 4, 4, 8, false)]  // back-to-back: half-open, no overlap
    [InlineData(4, 8, 0, 4, false)]  // back-to-back, reversed
    [InlineData(0, 10, 3, 5, true)]  // containment
    [InlineData(3, 5, 0, 10, true)]  // contained
    [InlineData(0, 4, 0, 4, true)]   // identical
    [InlineData(0, 2, 5, 8, false)]  // disjoint
    public void Overlaps_FollowsHalfOpenSemantics(int a1, int a2, int b1, int b2, bool expected)
    {
        Assert.Equal(expected, W(a1, a2).Overlaps(W(b1, b2)));
        Assert.Equal(expected, W(b1, b2).Overlaps(W(a1, a2)));
    }

    [Fact]
    public void Contains_IncludesStartExcludesEnd()
    {
        var window = W(0, 4);

        Assert.True(window.Contains(Start));
        Assert.True(window.Contains(Start.AddHours(3)));
        Assert.False(window.Contains(Start.AddHours(4)));
        Assert.False(window.Contains(Start.AddMinutes(-1)));
    }
}

public class CapacityAndCargoTests
{
    [Fact]
    public void CanCarry_AllowsCargoExactlyAtLimits() =>
        Assert.True(Capacity.Of(1000m, 10m).CanCarry(Cargo.Of("Boxes", 1000m, 10m)));

    [Fact]
    public void CanCarry_RejectsOverweight() =>
        Assert.False(Capacity.Of(1000m, 10m).CanCarry(Cargo.Of("Boxes", 1000.01m, 1m)));

    [Fact]
    public void CanCarry_RejectsOverVolume() =>
        Assert.False(Capacity.Of(1000m, 10m).CanCarry(Cargo.Of("Boxes", 10m, 10.01m)));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Capacity_RejectsNonPositiveValues(int weight, int volume) =>
        DomainAssert.Fails(DomainErrorCodes.Validation, () => Capacity.Of(weight, volume));

    [Fact]
    public void Cargo_RequiresDescriptionAndPositiveWeight()
    {
        DomainAssert.Fails(DomainErrorCodes.Validation, () => Cargo.Of(" ", 1m, 1m));
        DomainAssert.Fails(DomainErrorCodes.Validation, () => Cargo.Of("Boxes", 0m, 1m));
        DomainAssert.Fails(DomainErrorCodes.Validation, () => Cargo.Of("Boxes", 1m, -1m));
    }

    [Fact]
    public void Cargo_AllowsZeroVolume() => Assert.Equal(0m, Cargo.Of("Documents", 1m, 0m).VolumeM3);

    [Fact]
    public void Cargo_RejectsTooLongDescription() =>
        DomainAssert.Fails(DomainErrorCodes.Validation, () => Cargo.Of(new string('x', Cargo.MaxDescriptionLength + 1), 1m, 1m));
}
