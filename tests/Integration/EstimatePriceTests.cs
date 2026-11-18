using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>Has its own database (its own fixture), so the thuja here does not show up in other tests.</summary>
public sealed class EstimatePriceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/pricing/estimate";

    private static readonly PlotElement Plot = new(
        Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated,
        [new Point2(0, 0), new Point2(20, 0), new Point2(20, 15), new Point2(0, 15)], Sloped: false);

    [Fact]
    public async Task Estimates_a_scene_with_one_thuja()
    {
        var thuja = await SaveThujaAsync();
        var scene = Scene(new PointElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, thuja, new Point2(5, 5), 0));

        using var response = await PostAsync(SceneJson.Serialize(scene));

        // 395 kr + 0,5 h × 650 kr + etablering 2 500 + transport 1 500 = 4 720, rounded outwards to whole hundreds.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(4700m, body.GetProperty("total").GetProperty("low").GetDecimal());
        Assert.Equal(4800m, body.GetProperty("total").GetProperty("high").GetDecimal());
        Assert.Equal(940m, body.GetProperty("vat").GetProperty("low").GetDecimal());
        Assert.Equal(960m, body.GetProperty("vat").GetProperty("high").GetDecimal());
        Assert.Equal("high", body.GetProperty("confidence").GetString());
        Assert.Equal(0, body.GetProperty("missingArticles").GetInt32());
        var lines = body.GetProperty("lines");
        Assert.Equal("material", lines[0].GetProperty("kind").GetString());
        Assert.Equal("hour", lines[1].GetProperty("unit").GetString());
        Assert.Equal(395m, body.GetProperty("subtotals").GetProperty("material").GetProperty("low").GetDecimal());
    }

    [Fact]
    public async Task Unknown_article_is_counted_as_missing()
    {
        var scene = Scene(new PointElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, Ids.New(), new Point2(5, 5), 0));

        using var response = await PostAsync(SceneJson.Serialize(scene));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(1, body.GetProperty("missingArticles").GetInt32());
        Assert.Equal(0m, body.GetProperty("total").GetProperty("low").GetDecimal());
        Assert.Equal(0m, body.GetProperty("total").GetProperty("high").GetDecimal());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"formatVersion\":3,\"elements\":[]}")]
    [InlineData("{\"formatVersion\":1,\"elements\":[],\"extra\":1}")]
    public async Task Unreadable_body_is_400(string body)
    {
        using var response = await PostAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unreadable-scene", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scene_without_plot_is_400_with_problems()
    {
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion, []);

        using var response = await PostAsync(SceneJson.Serialize(scene));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("invalid-scene", body.GetProperty("code").GetString());
        Assert.Equal("plot-count", body.GetProperty("problems")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Huge_polygon_is_400_without_costly_checks()
    {
        // About 20 000 corners still fits in one megabyte; checking every pair of edges would take seconds.
        const int count = 20_000;
        var circle = Enumerable.Range(0, count).Select(i => new Point2(5 + (4 * Math.Cos(2 * Math.PI * i / count)), 5 + (4 * Math.Sin(2 * Math.PI * i / count)))).ToList();
        var scene = Scene(new SurfaceElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, circle, SurfaceMaterial.Grass));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        using var response = await PostAsync(SceneJson.Serialize(scene));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("too-many-points", (await ReadAsync(response)).GetProperty("problems")[0].GetProperty("code").GetString());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"Took {watch.Elapsed}.");
    }

    [Fact]
    public async Task Body_over_one_megabyte_is_413()
    {
        var padding = new string(' ', (1024 * 1024) + 1);

        using var response = await PostAsync("{\"formatVersion\":1,\"elements\":[]}" + padding);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private static SceneDocument Scene(params SceneElement[] elements) =>
        new(SceneDocument.CurrentFormatVersion, [Plot, .. elements]);

    private async Task<Guid> SaveThujaAsync()
    {
        var thuja = Article.Create(new ArticleData(
            "TU-SM-080", "Tuja Smaragd 80–100 cm", "Tuja Smaragd", "Träd", ArticleUnit.Piece, Money.InSek(395m),
            StockStatus.InStock, TypeModel.ConiferColumn, 1, "#3f7a3a", null, HardinessZone: 5, Height: 0.9, Width: null,
            MatureHeight: 4, MatureWidth: 1.2, ClipHeight: null, GrowthCmPerYear: 20, Light.Sun, LaborHours: 0.5m));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Add(thuja);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return thuja.Id;
    }

    private async Task<HttpResponseMessage> PostAsync(string body)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri(Url, UriKind.Relative), content, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
}
