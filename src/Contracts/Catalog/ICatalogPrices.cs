using GardenBuilder.SharedKernel;

namespace GardenBuilder.Contracts.Catalog;

/// <summary>What pricing needs to know about one article.</summary>
/// <param name="Unit">"piece", "meter" or "square-meter".</param>
/// <param name="Price">Price per unit in SEK including VAT.</param>
/// <param name="LaborHours">Hours of work per unit.</param>
/// <param name="TypeModel">The type model key, e.g. "deciduous-tree".</param>
public sealed record ArticlePriceFacts(
    Guid Id, string Name, string Unit, Money Price, decimal LaborHours, string TypeModel, bool Discontinued);

/// <summary>Prices and labor hours from the catalog, for the pricing module.</summary>
public interface ICatalogPrices
{
    /// <summary>Facts for each id that exists, discontinued articles included. Unknown ids are left out.</summary>
    Task<IReadOnlyList<ArticlePriceFacts>> GetAsync(IReadOnlyCollection<Guid> articleIds, CancellationToken cancellationToken);
}
