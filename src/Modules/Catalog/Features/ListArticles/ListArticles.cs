using GardenBuilder.Modules.Catalog.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Catalog.Features.ListArticles;

/// <summary>
/// Example feature folder: endpoint, handler and response live together.
/// Add a validator in the same folder when the feature takes input.
/// </summary>
internal static class ListArticles
{
    public sealed record ArticleResponse(
        Guid Id,
        string ArticleNumber,
        string Name,
        string Unit,
        decimal Price,
        string Currency,
        string StockStatus);

    public static RouteGroupBuilder MapListArticles(this RouteGroupBuilder group)
    {
        group.MapGet("/articles", HandleAsync).WithName("ListArticles");
        return group;
    }

    private static async Task<Ok<List<ArticleResponse>>> HandleAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var articles = await db.Set<Domain.Article>()
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new ArticleResponse(
                a.Id,
                a.ArticleNumber,
                a.Name,
                a.Unit.ToString(),
                a.Price.Amount,
                a.Price.Currency,
                a.StockStatus.ToString()))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(articles);
    }
}
