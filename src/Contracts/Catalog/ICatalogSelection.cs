using GardenBuilder.SharedKernel;

namespace GardenBuilder.Contracts.Catalog;

/// <summary>An article a design proposal may use, with what the layout and the plant rules need.</summary>
/// <param name="Unit">"piece", "meter" or "square-meter".</param>
/// <param name="TypeModel">The form key, e.g. "conifer-column".</param>
/// <param name="Family">What the form stands for, e.g. "conifer", "surface-paving", "furniture".</param>
/// <param name="Geometry">"point", "line" or "surface".</param>
/// <param name="Material">The scene's surface material ("grass", "paving", "gravel", "decking", "bed") for surfaces, else null.</param>
/// <param name="HardinessZone">The coldest zone the plant survives, 1–8 ("zon V" = zones I–V). Null for non-plants.</param>
/// <param name="Light">"sun", "part-shade" and/or "shade". Empty for non-plants.</param>
public sealed record CatalogArticle(
    Guid Id,
    string Name,
    string Unit,
    Money Price,
    string TypeModel,
    string Family,
    string Geometry,
    string? Material,
    int? HardinessZone,
    double? Height,
    double? Width,
    double? MatureHeight,
    double? MatureWidth,
    IReadOnlyList<string> Light);

/// <summary>The catalog as design proposals see it: only what can be delivered.</summary>
public interface ICatalogSelection
{
    /// <summary>Every article that is in stock (not sold out, not discontinued).</summary>
    Task<IReadOnlyList<CatalogArticle>> InStockAsync(CancellationToken cancellationToken);
}
