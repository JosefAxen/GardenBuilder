using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>What design proposals may choose from: articles in stock, with the plant fields. Own database (own fixture).</summary>
public sealed class CatalogSelectionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ArticleData Thuja() => new(
        "TU-SM-080", "Tuja Smaragd 80–100 cm", "Tuja Smaragd", "Träd", ArticleUnit.Piece, Money.InSek(495m),
        StockStatus.InStock, TypeModel.ConiferColumn, 1, "#3f7a3a", null, HardinessZone: 5, Height: 0.9, Width: null,
        MatureHeight: 4, MatureWidth: 1.2, ClipHeight: null, GrowthCmPerYear: 20, Light.Sun | Light.PartShade, LaborHours: 0.74m);

    [Fact]
    public async Task In_stock_lists_only_in_stock_articles_with_plant_fields()
    {
        var thuja = Article.Create(Thuja());
        var soldOut = Article.Create(Thuja() with { ArticleNumber = "TU-SM-100", StockStatus = StockStatus.OutOfStock });
        var gone = Article.Create(Thuja() with { ArticleNumber = "TU-SM-120" });
        gone.Discontinue();
        var slabs = Article.Create(Thuja() with
        {
            ArticleNumber = "PL-GR-40", Name = "Betongplatta grå 40×40", Variety = null, Category = "Ytor", Unit = ArticleUnit.SquareMeter,
            TypeModel = TypeModel.SurfacePaving, HardinessZone = null, Height = null, MatureHeight = null, MatureWidth = null,
            GrowthCmPerYear = null, Light = Light.None,
        });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.AddRange(thuja, soldOut, gone, slabs);
            await db.SaveChangesAsync(Ct);
        }

        using var lookup = factory.Services.CreateScope();
        var listed = await lookup.ServiceProvider.GetRequiredService<ICatalogSelection>().InStockAsync(Ct);

        var ours = listed.Where(a => a.Id == thuja.Id || a.Id == soldOut.Id || a.Id == gone.Id || a.Id == slabs.Id).ToList();
        Assert.Equal(2, ours.Count);
        var plant = ours.Single(a => a.Id == thuja.Id);
        Assert.Equal(("conifer-column", "conifer", "point", "piece", 5, 1.2), (plant.TypeModel, plant.Family, plant.Geometry, plant.Unit, plant.HardinessZone!.Value, plant.MatureWidth!.Value));
        Assert.Equal(["sun", "part-shade"], plant.Light);
        Assert.Null(plant.Material);
        var surface = ours.Single(a => a.Id == slabs.Id);
        Assert.Equal(("surface", "paving", "square-meter"), (surface.Geometry, surface.Material, surface.Unit));
        Assert.Empty(surface.Light);
    }
}
