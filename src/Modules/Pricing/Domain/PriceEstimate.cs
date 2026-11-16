using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Pricing.Domain;

/// <summary>What a price line is for (docs/design.md, "Beräkning").</summary>
internal enum LineKind
{
    Material,
    Labor,
    Removal,
    Fixed,
}

/// <summary>How much the price can be trusted, from how the measurements were made.</summary>
internal enum Confidence
{
    Low,
    Medium,
    High,
}

/// <summary>A low and a high amount. Equal when nothing about the amount is uncertain.</summary>
internal sealed record PriceRange(Money Low, Money High)
{
    public static PriceRange Zero { get; } = new(Money.Zero(), Money.Zero());

    public static PriceRange operator +(PriceRange left, PriceRange right) => new(left.Low + right.Low, left.High + right.High);

    public static PriceRange Add(PriceRange left, PriceRange right) => left + right;
}

/// <summary>One row of the estimate, e.g. all thujas of one size, or the work to remove a lawn.</summary>
/// <param name="Label">Swedish text shown to the customer.</param>
/// <param name="Unit">"piece", "meter", "square-meter" or "hour".</param>
internal sealed record PriceLine(LineKind Kind, string Label, decimal Quantity, string Unit, PriceRange Amount);

/// <summary>
/// The approximate price of a design. Lines are exact decimals; only <see cref="Total"/> is rounded, once.
/// <see cref="Vat"/> is the VAT included in the rounded total, for information.
/// </summary>
/// <param name="MissingArticles">New elements whose article is unknown, discontinued or of the wrong unit.</param>
internal sealed record PriceEstimate(
    IReadOnlyList<PriceLine> Lines,
    IReadOnlyDictionary<LineKind, PriceRange> Subtotals,
    PriceRange Total,
    PriceRange Vat,
    Confidence Confidence,
    int MissingArticles);
