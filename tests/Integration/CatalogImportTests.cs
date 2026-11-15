using System.Net.Http.Json;
using System.Text.Json;
using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Features.ImportArticles;
using GardenBuilder.Modules.Catalog.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

public sealed class CatalogImportTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Every test starts from an empty catalog; they share one database through the fixture.</summary>
    public async ValueTask InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Set<Article>().ExecuteDeleteAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static MemoryStream File(IEnumerable<ArticleData> articles)
    {
        var stream = new MemoryStream();
        ArticleSheet.Write(articles, stream);
        stream.Position = 0;
        return stream;
    }

    private async Task<CatalogImportResult> ImportAsync(IEnumerable<ArticleData> articles)
    {
        using var scope = factory.Services.CreateScope();
        using var file = File(articles);
        return await scope.ServiceProvider.GetRequiredService<ICatalogImport>().ImportAsync(file, Ct);
    }

    private async Task<JsonElement[]> ListAsync()
    {
        using var client = factory.CreateClient();
        var json = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/catalog/articles", UriKind.Relative), Ct);
        return [.. json.EnumerateArray()];
    }

    private async Task<int> CountRowsAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Set<Article>().CountAsync(Ct);
    }

    [Fact]
    public async Task Importing_the_example_fills_the_catalog()
    {
        var result = await ImportAsync(ExampleCatalog.Articles);

        Assert.True(result.Succeeded);
        Assert.Equal(ExampleCatalog.Articles.Count, result.Created);
        Assert.Equal(ExampleCatalog.Articles.Count, (await ListAsync()).Length);
    }

    [Fact]
    public async Task Importing_the_same_file_twice_changes_nothing()
    {
        await ImportAsync(ExampleCatalog.Articles);
        var idsBefore = (await ListAsync()).Select(a => a.GetProperty("id").GetGuid()).Order().ToList();

        var second = await ImportAsync(ExampleCatalog.Articles);

        Assert.Equal((0, 0, 0), (second.Created, second.Updated, second.Discontinued));
        Assert.Equal(idsBefore, (await ListAsync()).Select(a => a.GetProperty("id").GetGuid()).Order().ToList());
    }

    [Fact]
    public async Task Article_missing_from_the_file_is_discontinued()
    {
        await ImportAsync(ExampleCatalog.Articles);
        var removed = ExampleCatalog.Articles[0];

        var result = await ImportAsync(ExampleCatalog.Articles.Skip(1));

        Assert.Equal(1, result.Discontinued);
        Assert.DoesNotContain(await ListAsync(), a => a.GetProperty("articleNumber").GetString() == removed.ArticleNumber);
        Assert.Equal(ExampleCatalog.Articles.Count, await CountRowsAsync());
    }

    [Fact]
    public async Task File_with_an_error_writes_nothing()
    {
        var broken = ExampleCatalog.Articles[0] with { ArticleNumber = "BROKEN-1", HardinessZone = null };

        var result = await ImportAsync([.. ExampleCatalog.Articles, broken]);

        Assert.False(result.Succeeded);
        Assert.Equal((0, 0, 0), (result.Created, result.Updated, result.Discontinued));
        Assert.Contains(result.Errors, e => e.Message == "Växtzon krävs för växter.");
        Assert.Equal(0, await CountRowsAsync());
    }

    [Fact]
    public async Task Changed_price_updates_the_article()
    {
        await ImportAsync(ExampleCatalog.Articles);
        var thuja = ExampleCatalog.Articles.First(a => a.Variety == "Tuja Smaragd");
        var idBefore = (await ListAsync()).Single(a => a.GetProperty("articleNumber").GetString() == thuja.ArticleNumber).GetProperty("id").GetGuid();

        var result = await ImportAsync(ExampleCatalog.Articles.Select(a => a == thuja ? a with { Price = Money.InSek(219m) } : a));

        Assert.Equal(1, result.Updated);
        var after = (await ListAsync()).Single(a => a.GetProperty("articleNumber").GetString() == thuja.ArticleNumber);
        Assert.Equal(219m, after.GetProperty("price").GetDecimal());
        Assert.Equal(idBefore, after.GetProperty("id").GetGuid());
    }
}
