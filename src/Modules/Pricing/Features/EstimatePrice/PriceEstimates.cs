using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Pricing;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Pricing.Domain;
using GardenBuilder.Modules.Pricing.Infrastructure;

namespace GardenBuilder.Modules.Pricing.Features.EstimatePrice;

/// <summary>The editor's price calculation for other modules: same facts, same settings, same calculator.</summary>
internal sealed class PriceEstimates(ICatalogPrices catalogPrices, PriceSettingsStore settingsStore) : IPriceEstimates
{
    public async Task<PriceSpan> EstimateAsync(SceneDocument scene, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var ids = EstimatePrice.ArticleIds(scene);
        var facts = await catalogPrices.GetAsync(ids, cancellationToken);
        var settings = await settingsStore.LoadAsync(cancellationToken);
        var total = PriceCalculator.Estimate(scene, facts.ToDictionary(f => f.Id), settings).Total;
        return new PriceSpan(total.Low, total.High);
    }
}
