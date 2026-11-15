using System.Net.Http.Json;
using System.Text.Json;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>Has its own database (its own fixture), so the empty-catalog test in <see cref="CatalogTests"/> stays empty.</summary>
public sealed class CatalogListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static ArticleData Thuja() => new(
        "TU-SM-080", "Tuja Smaragd 80–100 cm", "Tuja Smaragd", "Träd", ArticleUnit.Piece, Money.InSek(495m),
        StockStatus.InStock, TypeModel.Conifer, 1, "#3f7a3a", null, HardinessZone: 5, Height: 0.9, Width: null,
        MatureHeight: 4, MatureWidth: 1.2, ClipHeight: null, GrowthCmPerYear: 20, Light.Sun | Light.PartShade, LaborHours: 0.5m);

    [Fact]
    public async Task List_articles_returns_fields_and_hides_discontinued()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var hedge = Thuja() with
            {
                ArticleNumber = "TU-HA-060", Name = "Thujahäck Smaragd 60–80 cm", Unit = ArticleUnit.Meter,
                TypeModel = TypeModel.Hedge, StockStatus = StockStatus.OutOfStock, Height = 0.7, Width = 0.5, MatureWidth = null,
            };
            var pot = Thuja() with
            {
                ArticleNumber = "KR-TE-040", Name = "Terrakottakruka 40 cm", Variety = null, Category = "Krukor och möbler",
                TypeModel = TypeModel.Pot, HardinessZone = null, Height = null, MatureHeight = null, MatureWidth = null,
                GrowthCmPerYear = null, Light = Light.None,
            };
            var discontinued = Article.Create(pot);
            discontinued.Discontinue();
            db.AddRange(Article.Create(Thuja()), Article.Create(hedge), discontinued);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = factory.CreateClient();
        var articles = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/catalog/articles", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(2, articles.GetArrayLength());
        var thuja = articles.EnumerateArray().Single(a => a.GetProperty("articleNumber").GetString() == "TU-SM-080");
        Assert.Equal("conifer", thuja.GetProperty("typeModel").GetString());
        Assert.Equal("point", thuja.GetProperty("geometry").GetString());
        Assert.Equal("Tuja Smaragd", thuja.GetProperty("variety").GetString());
        Assert.Equal(0.9, thuja.GetProperty("height").GetDouble());
        Assert.Equal(495m, thuja.GetProperty("price").GetDecimal());
        Assert.Equal("piece", thuja.GetProperty("unit").GetString());
        Assert.Equal(["sun", "part-shade"], thuja.GetProperty("light").EnumerateArray().Select(l => l.GetString()));
        var hedgeJson = articles.EnumerateArray().Single(a => a.GetProperty("articleNumber").GetString() == "TU-HA-060");
        Assert.Equal("out-of-stock", hedgeJson.GetProperty("stockStatus").GetString());
        Assert.Equal("line", hedgeJson.GetProperty("geometry").GetString());
    }
}
