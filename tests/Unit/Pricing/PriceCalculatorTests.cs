using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Pricing.Domain;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Pricing;

public sealed class PriceCalculatorTests
{
    private static readonly ArticlePriceFacts Thuja = new(
        Ids.New(), "Tuja Smaragd 80–100 cm", "piece", Money.InSek(395m), 0.5m, "conifer", Discontinued: false);

    private static readonly ArticlePriceFacts Paving = new(
        Ids.New(), "Betongplattor 40 × 40 cm", "square-meter", Money.InSek(395m), 0.6m, "surface-paving", Discontinued: false);

    /// <summary>A 10 × 5 m rectangle: 50 m².</summary>
    private static readonly Point2[] Rectangle = [new(0, 0), new(10, 0), new(10, 5), new(0, 5)];

    private static PlotElement Plot(bool sloped = false) => new(
        Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, [new(-20, -20), new(20, -20), new(20, 20), new(-20, 20)], sloped);

    private static PointElement Point(Guid? articleId, ElementStatus status = ElementStatus.New) =>
        new(Ids.New(), status, MeasurementSource.Estimated, articleId, new(1, 1), 0);

    private static SurfaceElement Surface(
        Guid? articleId, MeasurementSource source = MeasurementSource.Estimated, ElementStatus status = ElementStatus.New,
        SurfaceMaterial material = SurfaceMaterial.Paving) =>
        new(Ids.New(), status, source, articleId, Rectangle, material);

    private static PriceEstimate Estimate(IEnumerable<ArticlePriceFacts> articles, params SceneElement[] elements) =>
        PriceCalculator.Estimate(
            new SceneDocument(SceneDocument.CurrentFormatVersion, elements),
            articles.ToDictionary(a => a.Id),
            PriceSettings.Defaults);

    private static PriceEstimate Estimate(params SceneElement[] elements) => Estimate([Thuja, Paving], elements);

    private static PriceRange Sek(decimal low, decimal high) => new(Money.InSek(low), Money.InSek(high));

    private static PriceLine Line(PriceEstimate estimate, LineKind kind) => Assert.Single(estimate.Lines, l => l.Kind == kind);

    [Fact]
    public void One_new_thuja()
    {
        var estimate = Estimate(Plot(), Point(Thuja.Id));

        Assert.Equal(Sek(395, 395), Line(estimate, LineKind.Material).Amount);
        var labor = Line(estimate, LineKind.Labor);
        Assert.Equal(Sek(325, 325), labor.Amount);
        Assert.Equal(0.5m, labor.Quantity);
        Assert.Equal("hour", labor.Unit);
        Assert.Equal(Sek(4000, 4000), estimate.Subtotals[LineKind.Fixed]);
        Assert.Equal(Sek(4700, 4800), estimate.Total);
        Assert.Equal(Sek(940, 960), estimate.Vat);
        Assert.Equal(Confidence.High, estimate.Confidence);
    }

    [Fact]
    public void Estimated_paving_gets_thirty_percent_range()
    {
        var estimate = Estimate(Plot(), Surface(Paving.Id));

        Assert.Equal(Sek(13825, 25675), Line(estimate, LineKind.Material).Amount);
        Assert.Equal(50m, Line(estimate, LineKind.Material).Quantity);
        Assert.Equal(Sek(13650, 25350), Line(estimate, LineKind.Labor).Amount);
        Assert.Equal(Sek(31400, 55100), estimate.Total);
        Assert.Equal(Confidence.Low, estimate.Confidence);
    }

    [Fact]
    public void Sloped_plot_moves_verified_to_customer()
    {
        var estimate = Estimate(Plot(sloped: true), Surface(Paving.Id, MeasurementSource.Verified));

        Assert.Equal(Sek(16787.5m, 22712.5m), Line(estimate, LineKind.Material).Amount);
        Assert.Equal(Confidence.Medium, estimate.Confidence);
    }

    [Fact]
    public void Removed_lawn_without_article()
    {
        var estimate = Estimate(Plot(), Surface(null, status: ElementStatus.ExistingRemove, material: SurfaceMaterial.Grass));

        var removal = Line(estimate, LineKind.Removal);
        Assert.Equal("Borttagning: gräsmatta", removal.Label);
        Assert.Equal(Sek(4550, 8450), removal.Amount);
        Assert.Contains(estimate.Lines, l => l is { Kind: LineKind.Fixed, Label: "Bortforsling" } && l.Amount == Sek(2000, 2000));
        Assert.Equal(Sek(10500, 14500), estimate.Total);
    }

    [Fact]
    public void Removed_point_without_article_counts_as_deciduous_tree()
    {
        var estimate = Estimate(Plot(), Point(null, ElementStatus.ExistingRemove));

        var removal = Line(estimate, LineKind.Removal);
        Assert.Equal("Borttagning: lövträd", removal.Label);
        Assert.Equal(Sek(1950, 1950), removal.Amount);
    }

    [Fact]
    public void Removed_discontinued_conifer_uses_its_type_model()
    {
        var old = Thuja with { Id = Ids.New(), Discontinued = true };

        var estimate = Estimate([old], Plot(), Point(old.Id, ElementStatus.ExistingRemove));

        var removal = Line(estimate, LineKind.Removal);
        Assert.Equal("Borttagning: barrträd", removal.Label);
        Assert.Equal(Sek(1300, 1300), removal.Amount);
    }

    [Fact]
    public void Kept_elements_cost_nothing()
    {
        var estimate = Estimate(Plot(), Point(Thuja.Id, ElementStatus.ExistingKeep), Surface(null, status: ElementStatus.ExistingKeep));

        Assert.Empty(estimate.Lines);
        Assert.Equal(Sek(0, 0), estimate.Total);
        Assert.Equal(Confidence.High, estimate.Confidence);
    }

    [Fact]
    public void Unknown_article_is_missing_and_adds_no_fixed_items()
    {
        var estimate = Estimate(Plot(), Point(Ids.New()));

        Assert.Equal(1, estimate.MissingArticles);
        Assert.Empty(estimate.Lines);
        Assert.Equal(Sek(0, 0), estimate.Total);
    }

    [Fact]
    public void Discontinued_article_on_new_element_is_missing()
    {
        var old = Thuja with { Id = Ids.New(), Discontinued = true };

        var estimate = Estimate([old], Plot(), Point(old.Id));

        Assert.Equal(1, estimate.MissingArticles);
        Assert.Empty(estimate.Lines);
    }

    [Fact]
    public void Point_on_square_meter_article_is_missing()
    {
        var estimate = Estimate(Plot(), Point(Paving.Id));

        Assert.Equal(1, estimate.MissingArticles);
        Assert.Empty(estimate.Lines);
    }

    [Fact]
    public void Two_thujas_make_one_line()
    {
        var estimate = Estimate(Plot(), Point(Thuja.Id), Point(Thuja.Id));

        var material = Line(estimate, LineKind.Material);
        Assert.Equal(2m, material.Quantity);
        Assert.Equal(Sek(790, 790), material.Amount);
    }

    [Fact]
    public void Zero_labor_gives_no_labor_line()
    {
        var noLabor = Thuja with { Id = Ids.New(), LaborHours = 0 };

        var estimate = Estimate([noLabor], Plot(), Point(noLabor.Id));

        Assert.DoesNotContain(estimate.Lines, l => l.Kind == LineKind.Labor);
        Assert.Equal(Sek(0, 0), estimate.Subtotals[LineKind.Labor]);
    }

    [Fact]
    public void Kept_estimated_lawn_does_not_lower_confidence()
    {
        var estimate = Estimate(Plot(), Surface(null, status: ElementStatus.ExistingKeep, material: SurfaceMaterial.Grass), Point(Thuja.Id));

        Assert.Equal(Confidence.High, estimate.Confidence);
    }

    [Fact]
    public void Same_article_with_different_sources_sums_each_range()
    {
        var estimate = Estimate(Plot(), Surface(Paving.Id, MeasurementSource.Estimated), Surface(Paving.Id, MeasurementSource.Verified));

        var material = Line(estimate, LineKind.Material);
        Assert.Equal(100m, material.Quantity);
        Assert.Equal(Sek(32587.5m, 46412.5m), material.Amount);
        Assert.Equal(Confidence.Low, estimate.Confidence);
    }
}
