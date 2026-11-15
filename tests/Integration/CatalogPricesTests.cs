using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>Has its own database (its own fixture), so the articles here do not show up in other tests.</summary>
public sealed class CatalogPricesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static ArticleData Thuja() => new(
        "TU-SM-080", "Tuja Smaragd 80–100 cm", "Tuja Smaragd", "Träd", ArticleUnit.Piece, Money.InSek(495m),
        StockStatus.InStock, TypeModel.Conifer, 1, "#3f7a3a", null, HardinessZone: 5, Height: 0.9, Width: null,
        MatureHeight: 4, MatureWidth: 1.2, ClipHeight: null, GrowthCmPerYear: 20, Light.Sun | Light.PartShade, LaborHours: 0.74m);

    [Fact]
    public async Task Returns_facts_for_known_ids_including_discontinued()
    {
        var thuja = Article.Create(Thuja());
        var hedge = Article.Create(Thuja() with
        {
            ArticleNumber = "TU-HA-060", Name = "Thujahäck Smaragd 60–80 cm", Unit = ArticleUnit.Meter,
            TypeModel = TypeModel.Hedge, Height = 0.7, Width = 0.5, MatureWidth = null, LaborHours = 0.5m,
        });
        var pot = Article.Create(Thuja() with
        {
            ArticleNumber = "KR-TE-040", Name = "Terrakottakruka 40 cm", Variety = null, Category = "Krukor och möbler",
            TypeModel = TypeModel.Pot, HardinessZone = null, Height = null, MatureHeight = null, MatureWidth = null,
            GrowthCmPerYear = null, Light = Light.None, LaborHours = 0.1m,
        });
        pot.Discontinue();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.AddRange(thuja, hedge, pot);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var lookup = factory.Services.CreateScope();
        var prices = lookup.ServiceProvider.GetRequiredService<ICatalogPrices>();
        var facts = await prices.GetAsync([thuja.Id, hedge.Id, pot.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        Assert.Equal(3, facts.Count);
        var thujaFacts = facts.Single(f => f.Id == thuja.Id);
        Assert.Equal(new ArticlePriceFacts(thuja.Id, "Tuja Smaragd 80–100 cm", "piece", Money.InSek(495m), 0.74m, "conifer", Discontinued: false), thujaFacts);
        Assert.Equal("meter", facts.Single(f => f.Id == hedge.Id).Unit);
        Assert.True(facts.Single(f => f.Id == pot.Id).Discontinued);
    }

    [Fact]
    public async Task Empty_list_gives_empty_answer()
    {
        using var scope = factory.Services.CreateScope();
        var prices = scope.ServiceProvider.GetRequiredService<ICatalogPrices>();

        Assert.Empty(await prices.GetAsync([], TestContext.Current.CancellationToken));
    }
}
