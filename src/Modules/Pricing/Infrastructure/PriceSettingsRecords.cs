using GardenBuilder.Modules.Pricing.Domain;

namespace GardenBuilder.Modules.Pricing.Infrastructure;

/// <summary>The one row in pricing.price_settings. Amounts are SEK including VAT.</summary>
internal sealed class PriceSettingsRecord
{
    /// <summary>Always <see cref="SingleId"/>: there is one set of settings.</summary>
    public const int SingleId = 1;

    public int Id { get; init; }

    public decimal HourlyRate { get; init; }

    public decimal VatPercent { get; init; }

    public decimal EstimatedRangePercent { get; init; }

    public decimal CustomerRangePercent { get; init; }

    public decimal VerifiedRangePercent { get; init; }

    public decimal Establishment { get; init; }

    public decimal Transport { get; init; }

    public decimal Disposal { get; init; }
}

/// <summary>One row in pricing.removal_norms: hours per piece, meter or square meter to remove one kind of thing.</summary>
internal sealed class RemovalNormRecord
{
    public RemovalClass RemovalClass { get; init; }

    public decimal Hours { get; init; }
}
