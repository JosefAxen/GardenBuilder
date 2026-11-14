using System.Text.Json;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Scenes;

public sealed class SceneJsonTests
{
    private static readonly Guid ArticleId = Guid.Parse("019a7d3c-0000-7000-8000-000000000001");

    private static SceneDocument EveryKind() => new(
        SceneDocument.CurrentFormatVersion,
        [
            new PlotElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, [new(-10, -10), new(10, -10), new(10, 10), new(-10, 10)], Sloped: false),
            new BuildingElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer, [new(-4, 2), new(4, 2), new(4, 8), new(-4, 8)], Height: 5, Roof: RoofShape.Gable),
            new SurfaceElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, ArticleId, [new(-9, -9), new(9, -9), new(9, 1), new(-9, 1)], SurfaceMaterial.Grass),
            new LineElement(Ids.New(), ElementStatus.New, MeasurementSource.Verified, ArticleId, [new(-9.5, -9.5), new(9.5, -9.5)]),
            new PointElement(Ids.New(), ElementStatus.ExistingRemove, MeasurementSource.Estimated, null, new(2, -5), Rotation: 0.5),
        ]);

    [Fact]
    public void Round_trips_every_element_kind()
    {
        var first = SceneJson.Serialize(EveryKind());

        var second = SceneJson.Serialize(SceneJson.Deserialize(first));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Writes_kind_and_kebab_case_enums()
    {
        var scene = new SceneDocument(
            SceneDocument.CurrentFormatVersion,
            [
                new PointElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, ArticleId, new(1, 2), 0),
                new BuildingElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, [new(0, 0), new(1, 0), new(1, 1)], 3, RoofShape.Gable),
            ]);

        var json = SceneJson.Serialize(scene);

        Assert.Contains("\"formatVersion\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"point\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"existing-keep\"", json, StringComparison.Ordinal);
        Assert.Contains("\"measurementSource\":\"estimated\"", json, StringComparison.Ordinal);
        Assert.Contains("\"roof\":\"gable\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_kind_even_when_it_is_not_the_first_property()
    {
        const string json = """
            {"formatVersion":1,"elements":[{"id":"019a7d3c-0000-7000-8000-000000000002","status":"new",
             "measurementSource":"estimated","articleId":null,"position":{"x":1,"y":2},"rotation":0,"kind":"point"}]}
            """;

        var scene = SceneJson.Deserialize(json);

        Assert.IsType<PointElement>(Assert.Single(scene.Elements));
    }

    [Fact]
    public void Rejects_unknown_property()
    {
        Assert.Throws<JsonException>(() => SceneJson.Deserialize("""{"formatVersion":1,"elements":[],"extra":1}"""));
    }

    [Fact]
    public void Rejects_unknown_kind()
    {
        const string json = """
            {"formatVersion":1,"elements":[{"kind":"pond","id":"019a7d3c-0000-7000-8000-000000000003",
             "status":"new","measurementSource":"estimated"}]}
            """;

        Assert.Throws<JsonException>(() => SceneJson.Deserialize(json));
    }

    [Fact]
    public void Rejects_point_without_coordinate()
    {
        const string json = """
            {"formatVersion":1,"elements":[{"kind":"point","id":"019a7d3c-0000-7000-8000-000000000004","status":"new",
             "measurementSource":"estimated","articleId":null,"position":{"x":1},"rotation":0}]}
            """;

        Assert.Throws<JsonException>(() => SceneJson.Deserialize(json));
    }

    [Fact]
    public void Rejects_missing_elements()
    {
        Assert.Throws<JsonException>(() => SceneJson.Deserialize("""{"formatVersion":1}"""));
    }
}
