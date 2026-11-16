using GardenBuilder.Modules.Pricing.Domain;
using GardenBuilder.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Pricing.Infrastructure;

/// <summary>Reads the company's pricing rules from the pricing schema.</summary>
internal sealed class PriceSettingsStore(PricingDbContext db)
{
    /// <summary>
    /// Throws when the settings row or a removal class is missing: a price built on guessed values is worse than an
    /// error.
    /// </summary>
    public async Task<PriceSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await db.Set<PriceSettingsRecord>().AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == PriceSettingsRecord.SingleId, cancellationToken)
            ?? throw new InvalidOperationException("The pricing settings row is missing.");
        var norms = await db.Set<RemovalNormRecord>().AsNoTracking().ToDictionaryAsync(n => n.RemovalClass, n => n.Hours, cancellationToken);

        var missing = Enum.GetValues<RemovalClass>().Where(c => !norms.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Removal hours are missing for {string.Join(", ", missing)}.");
        }

        return new PriceSettings(
            Money.InSek(settings.HourlyRate),
            settings.VatPercent,
            settings.EstimatedRangePercent,
            settings.CustomerRangePercent,
            settings.VerifiedRangePercent,
            Money.InSek(settings.Establishment),
            Money.InSek(settings.Transport),
            Money.InSek(settings.Disposal),
            norms);
    }
}
