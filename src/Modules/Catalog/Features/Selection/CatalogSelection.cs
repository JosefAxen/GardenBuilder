using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Catalog.Features.Selection;

/// <summary>Articles in stock with their plant fields, for design proposals. Sold-out and discontinued articles are left out.</summary>
internal sealed class CatalogSelection(CatalogDbContext db) : ICatalogSelection
{
    public async Task<IReadOnlyList<CatalogArticle>> InStockAsync(CancellationToken cancellationToken)
    {
        var articles = await db.Set<Article>()
            .AsNoTracking()
            .Where(a => a.StockStatus == StockStatus.InStock)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        return [.. articles.Select(ToContract)];
    }

    private static CatalogArticle ToContract(Article a) => new(
        a.Id,
        a.Name,
        ArticleUnits.Key(a.Unit),
        a.Price,
        TypeModels.Key(a.TypeModel),
        TypeModels.Family(a.TypeModel),
        TypeModels.Geometry(a.TypeModel).ToString().ToLowerInvariant(),
        TypeModels.Material(a.TypeModel) is { } material ? MaterialKey(material) : null,
        a.HardinessZone,
        a.Height,
        a.Width,
        a.MatureHeight,
        a.MatureWidth,
        LightKeys(a.Light));

    private static string MaterialKey(SurfaceMaterial material) => material switch
    {
        SurfaceMaterial.Grass => "grass",
        SurfaceMaterial.Paving => "paving",
        SurfaceMaterial.Gravel => "gravel",
        SurfaceMaterial.Decking => "decking",
        _ => "bed",
    };

    private static List<string> LightKeys(Light light)
    {
        var keys = new List<string>();
        if (light.HasFlag(Light.Sun))
        {
            keys.Add("sun");
        }

        if (light.HasFlag(Light.PartShade))
        {
            keys.Add("part-shade");
        }

        if (light.HasFlag(Light.Shade))
        {
            keys.Add("shade");
        }

        return keys;
    }
}
