using GardenBuilder.Contracts.Pricing;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>The price of a scene for other modules, with the same rules as the editor's price. Own database (own fixture).</summary>
public sealed class PriceEstimatesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Estimate_matches_the_editors_price()
    {
        var thuja = Article.Create(new ArticleData(
            "TU-SM-080", "Tuja Smaragd 80–100 cm", "Tuja Smaragd", "Träd", ArticleUnit.Piece, Money.InSek(395m),
            StockStatus.InStock, TypeModel.ConiferColumn, 1, "#3f7a3a", null, HardinessZone: 5, Height: 0.9, Width: null,
            MatureHeight: 4, MatureWidth: 1.2, ClipHeight: null, GrowthCmPerYear: 20, Light.Sun, LaborHours: 0.5m));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.Add(thuja);
            await db.SaveChangesAsync(Ct);
        }

        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion,
        [
            new PlotElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated,
                [new Point2(0, 0), new Point2(20, 0), new Point2(20, 15), new Point2(0, 15)], Sloped: false),
            new PointElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, thuja.Id, new Point2(5, 5), 0),
        ]);

        using var lookup = factory.Services.CreateScope();
        var price = await lookup.ServiceProvider.GetRequiredService<IPriceEstimates>().EstimateAsync(scene, Ct);

        // The same scene as EstimatePriceTests.Estimates_a_scene_with_one_thuja: 4 720 kr rounded outwards.
        Assert.Equal(Money.InSek(4700m), price.Low);
        Assert.Equal(Money.InSek(4800m), price.High);
        Assert.Equal(Money.InSek(4750m), price.Middle);
    }
}
