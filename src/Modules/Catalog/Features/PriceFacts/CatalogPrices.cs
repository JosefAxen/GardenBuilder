using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Catalog.Features.PriceFacts;

/// <summary>
/// Price, unit, labor hours and type model for the articles a scene points at. Discontinued articles are included,
/// because a saved design may still point at them; pricing decides what to do with them.
/// </summary>
internal sealed class CatalogPrices(CatalogDbContext db) : ICatalogPrices
{
    public async Task<IReadOnlyList<ArticlePriceFacts>> GetAsync(IReadOnlyCollection<Guid> articleIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(articleIds);
        if (articleIds.Count == 0)
        {
            return [];
        }

        var articles = await db.Set<Article>()
            .AsNoTracking()
            .Where(a => articleIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        return [.. articles.Select(a => new ArticlePriceFacts(
            a.Id, a.Name, ArticleUnits.Key(a.Unit), a.Price, a.LaborHours, TypeModels.Key(a.TypeModel), TypeModels.Family(a.TypeModel),
            a.StockStatus == StockStatus.Discontinued))];
    }
}
