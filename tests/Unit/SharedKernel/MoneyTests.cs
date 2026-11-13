using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.SharedKernel;

public sealed class MoneyTests
{
    [Fact]
    public void Adding_amounts_in_same_currency_sums_them()
    {
        var total = Money.InSek(100.50m) + Money.InSek(49.50m);

        Assert.Equal(Money.InSek(150m), total);
    }

    [Fact]
    public void Adding_amounts_in_different_currencies_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Money.InSek(1m) + new Money(1m, "EUR"));
    }

    [Fact]
    public void Multiplying_by_quantity_keeps_decimal_precision()
    {
        var price = Money.InSek(0.10m) * 3m;

        Assert.Equal(0.30m, price.Amount);
    }

    [Theory]
    [InlineData(1234.49, 1234)]
    [InlineData(1234.50, 1235)]
    public void RoundToWhole_rounds_half_away_from_zero(decimal amount, decimal expected)
    {
        Assert.Equal(expected, Money.InSek(amount).RoundToWhole().Amount);
    }
}
