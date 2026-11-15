using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Pricing.Domain;

/// <summary>
/// The company's pricing rules. All amounts are SEK including VAT, like everything the customer sees. Stored in the
/// pricing schema; the calculator gets them as a plain object so tests can use their own values.
/// </summary>
/// <param name="VatPercent">The VAT rate, 25 in Sweden. The VAT share of a price including VAT is 25 / 125 = 20 %.</param>
/// <param name="RemovalHours">Hours per piece, meter or square meter to remove each kind of thing.</param>
internal sealed record PriceSettings(
    Money HourlyRate,
    decimal VatPercent,
    decimal EstimatedRangePercent,
    decimal CustomerRangePercent,
    decimal VerifiedRangePercent,
    Money Establishment,
    Money Transport,
    Money Disposal,
    IReadOnlyDictionary<RemovalClass, decimal> RemovalHours)
{
    /// <summary>Made-up starting values until the company sets real ones in admin.</summary>
    public static PriceSettings Defaults { get; } = new(
        HourlyRate: Money.InSek(650m),
        VatPercent: 25m,
        EstimatedRangePercent: 30m,
        CustomerRangePercent: 15m,
        VerifiedRangePercent: 5m,
        Establishment: Money.InSek(2500m),
        Transport: Money.InSek(1500m),
        Disposal: Money.InSek(2000m),
        RemovalHours: new Dictionary<RemovalClass, decimal>
        {
            [RemovalClass.DeciduousTree] = 3m,
            [RemovalClass.Conifer] = 2m,
            [RemovalClass.ShrubOrOther] = 0.5m,
            [RemovalClass.Hedge] = 0.8m,
            [RemovalClass.Fence] = 0.4m,
            [RemovalClass.Grass] = 0.2m,
            [RemovalClass.Paving] = 0.6m,
            [RemovalClass.Gravel] = 0.3m,
            [RemovalClass.Decking] = 0.5m,
            [RemovalClass.Bed] = 0.3m,
        });

    /// <summary>How far, in percent, an amount that depends on a measured area or length can be off.</summary>
    public decimal RangePercent(MeasurementSource source) => source switch
    {
        MeasurementSource.Estimated => EstimatedRangePercent,
        MeasurementSource.Customer => CustomerRangePercent,
        _ => VerifiedRangePercent,
    };
}
