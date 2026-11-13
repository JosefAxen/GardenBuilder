using System.Globalization;

namespace GardenBuilder.SharedKernel;

/// <summary>
/// An amount of money. Always decimal, never floating point.
/// Prices shown to customers are in SEK including VAT.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public const string Sek = "SEK";

    public static Money Zero(string currency = Sek) => new(0m, currency);

    public static Money InSek(decimal amount) => new(amount, Sek);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left with { Amount = left.Amount + right.Amount };
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left with { Amount = left.Amount - right.Amount };
    }

    public static Money operator *(Money money, decimal factor) => money with { Amount = money.Amount * factor };

    public static Money operator *(decimal factor, Money money) => money * factor;

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public static Money Multiply(Money money, decimal factor) => money * factor;

    /// <summary>Rounds to whole units. Pricing rounds once, on the grand total.</summary>
    public Money RoundToWhole() => this with { Amount = Math.Round(Amount, 0, MidpointRounding.AwayFromZero) };

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot combine {left.Currency} with {right.Currency}.");
        }
    }
}
