using GardenBuilder.Contracts.Catalog;

namespace GardenBuilder.Modules.Ai.Features.DesignProposals;

/// <summary>
/// The articles the model may choose from: everything in stock that is not a plant, and the plants that survive the
/// customer's zone, those that suit the most zones' light first, at most <see cref="Max"/> in all.
/// </summary>
internal static class ArticleSelection
{
    public const int Max = 150;

    private static readonly HashSet<string> PlantFamilies = new(StringComparer.Ordinal) { "deciduous-tree", "conifer", "shrub", "perennial", "hedge" };

    public static bool IsPlant(CatalogArticle article) => PlantFamilies.Contains(article.Family);

    /// <summary>The catalog's light key for a zone's light.</summary>
    public static string LightKey(ZoneLight light) => light switch
    {
        ZoneLight.Sun => "sun",
        ZoneLight.PartShade => "part-shade",
        _ => "shade",
    };

    /// <summary>A plant fits when its zone (the coldest it survives) is at least the customer's.</summary>
    public static bool Survives(CatalogArticle plant, int customerZone) => plant.HardinessZone is { } zone && zone >= customerZone;

    public static IReadOnlyList<CatalogArticle> Select(IReadOnlyList<CatalogArticle> inStock, int customerZone, IReadOnlyList<Zone> zones)
    {
        ArgumentNullException.ThrowIfNull(inStock);
        var others = inStock.Where(a => !IsPlant(a)).Take(Max).ToList();
        var lights = zones.Select(z => LightKey(z.Light)).ToList();
        var plants = inStock
            .Where(a => IsPlant(a) && Survives(a, customerZone))
            .OrderByDescending(a => lights.Count(a.Light.Contains))
            .ThenBy(a => a.Name, StringComparer.Ordinal)
            .Take(Max - others.Count);
        return [.. plants, .. others];
    }
}
