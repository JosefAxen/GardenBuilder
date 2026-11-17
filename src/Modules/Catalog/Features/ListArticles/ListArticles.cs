using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Catalog.Features.ListArticles;

/// <summary>
/// The catalog the editor builds from: every article except discontinued ones. Feature folder pattern: endpoint,
/// handler and response live together.
/// </summary>
internal static class ListArticles
{
    /// <param name="Light">A subset of "sun", "part-shade", "shade".</param>
    public sealed record ArticleResponse(
        Guid Id,
        string ArticleNumber,
        string Name,
        string? Variety,
        string Category,
        string Unit,
        decimal Price,
        string Currency,
        string StockStatus,
        string TypeModel,
        string Family,
        string Geometry,
        string? Material,
        double Scale,
        string Color,
        string? AccentColor,
        int? HardinessZone,
        double? Height,
        double? Width,
        double? MatureHeight,
        double? MatureWidth,
        double? ClipHeight,
        double? GrowthCmPerYear,
        string[] Light);

    public static RouteGroupBuilder MapListArticles(this RouteGroupBuilder group)
    {
        group.MapGet("/articles", HandleAsync).WithName("ListArticles");
        return group;
    }

    private static async Task<Ok<List<ArticleResponse>>> HandleAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var articles = await db.Set<Article>()
            .AsNoTracking()
            .Where(a => a.StockStatus != StockStatus.Discontinued)
            .OrderBy(a => a.Category)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(articles.Select(ToResponse).ToList());
    }

    private static ArticleResponse ToResponse(Article a) => new(
        a.Id,
        a.ArticleNumber,
        a.Name,
        a.Variety,
        a.Category,
        ArticleUnits.Key(a.Unit),
        a.Price.Amount,
        a.Price.Currency,
        a.StockStatus == StockStatus.InStock ? "in-stock" : "out-of-stock",
        TypeModels.Key(a.TypeModel),
        TypeModels.Family(a.TypeModel),
        TypeModels.Geometry(a.TypeModel).ToString().ToLowerInvariant(),
        TypeModels.Material(a.TypeModel)?.ToString().ToLowerInvariant(),
        a.Scale,
        a.Color,
        a.AccentColor,
        a.HardinessZone,
        a.Height,
        a.Width,
        a.MatureHeight,
        a.MatureWidth,
        a.ClipHeight,
        a.GrowthCmPerYear,
        LightNames(a.Light));

    private static string[] LightNames(Light light) =>
    [
        .. light.HasFlag(Light.Sun) ? ["sun"] : Array.Empty<string>(),
        .. light.HasFlag(Light.PartShade) ? ["part-shade"] : Array.Empty<string>(),
        .. light.HasFlag(Light.Shade) ? ["shade"] : Array.Empty<string>(),
    ];
}
