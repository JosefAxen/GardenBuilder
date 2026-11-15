using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Catalog.Features.ImportArticles;

/// <summary>
/// Updates the catalog from an import file. Articles are matched on article number. An article missing from the file
/// is discontinued, not deleted, because saved designs may point at it.
/// </summary>
internal sealed class CatalogImport(CatalogDbContext db) : ICatalogImport
{
    public async Task<CatalogImportResult> ImportAsync(Stream xlsx, CancellationToken cancellationToken)
    {
        var sheet = ArticleSheet.Read(xlsx);
        if (sheet.Errors.Count > 0)
        {
            return new CatalogImportResult(0, 0, 0, [.. sheet.Errors.Select(e => new CatalogImportError(e.Row, e.Column, e.Message))]);
        }

        // Aspire turns on retries for transient database errors; a transaction must then run as one retriable unit.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(ct => ApplyAsync(sheet.Articles, ct), cancellationToken);
    }

    public void WriteExample(Stream xlsx) => ArticleSheet.Write(ExampleCatalog.Articles, xlsx);

    private async Task<CatalogImportResult> ApplyAsync(IReadOnlyList<ArticleData> articles, CancellationToken cancellationToken)
    {
        // A retry starts over, so forget anything a failed attempt had loaded or changed.
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var existing = await db.Set<Article>().ToDictionaryAsync(a => a.ArticleNumber, StringComparer.Ordinal, cancellationToken);
        int created = 0, updated = 0, discontinued = 0;

        foreach (var data in articles)
        {
            if (!existing.Remove(data.ArticleNumber, out var article))
            {
                db.Add(Article.Create(data));
                created++;
            }
            else if (article.ToData() != data)
            {
                article.Update(data);
                updated++;
            }
        }

        // Whatever is left was not in the file.
        foreach (var article in existing.Values.Where(a => a.StockStatus != StockStatus.Discontinued))
        {
            article.Discontinue();
            discontinued++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CatalogImportResult(created, updated, discontinued, []);
    }
}
