using System.Text.Json;
using System.Text.Json.Nodes;
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

        Assert.Contains("\"formatVersion\":3", json, StringComparison.Ordinal);
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
    public void Rejects_null_element()
    {
        Assert.Throws<JsonException>(() => SceneJson.Deserialize("""{"formatVersion":1,"elements":[null]}"""));
    }

    [Fact]
    public void Rejects_enum_written_as_number()
    {
        const string json = """
            {"formatVersion":1,"elements":[{"kind":"point","id":"019a7d3c-0000-7000-8000-000000000005","status":5,
             "measurementSource":"estimated","articleId":null,"position":{"x":1,"y":2},"rotation":0}]}
            """;

        Assert.Throws<JsonException>(() => SceneJson.Deserialize(json));
    }

    [Fact]
    public void Rejects_duplicate_property()
    {
        const string json = """
            {"formatVersion":1,"elements":[{"kind":"point","id":"019a7d3c-0000-7000-8000-000000000006","status":"new",
             "measurementSource":"estimated","articleId":null,"position":{"x":1,"y":2,"x":999},"rotation":0}]}
            """;

        Assert.Throws<JsonException>(() => SceneJson.Deserialize(json));
    }

    [Fact]
    public void Unsupported_format_version_is_named_in_the_error()
    {
        // A future version may carry fields this version does not know; the error must say "version", not "camera".
        var error = Assert.Throws<JsonException>(() => SceneJson.Deserialize("""{"formatVersion":4,"elements":[],"camera":{}}"""));

        Assert.Contains("format version 4", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_number_too_large_to_be_finite()
    {
        // 1e400 overflows a double and would otherwise be read as Infinity.
        const string json = """
            {"formatVersion":1,"elements":[{"kind":"point","id":"019a7d3c-0000-7000-8000-000000000007","status":"new",
             "measurementSource":"estimated","articleId":null,"position":{"x":1e400,"y":2},"rotation":0}]}
            """;

        Assert.Throws<JsonException>(() => SceneJson.Deserialize(json));
    }

    [Fact]
    public void Two_different_types_with_the_same_name_fail_the_schema_export()
    {
        var root = new JsonObject
        {
            ["a"] = new JsonObject { ["title"] = "Thing", ["type"] = "string" },
            ["b"] = new JsonObject { ["title"] = "Thing", ["type"] = "number" },
        };

        var error = Assert.Throws<InvalidOperationException>(() => SceneJson.HoistNamedTypes(root, []));

        Assert.Contains("Thing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_type_used_twice_is_described_once()
    {
        var root = new JsonObject
        {
            ["a"] = new JsonObject { ["title"] = "Thing", ["type"] = "string" },
            ["b"] = new JsonObject { ["title"] = "Thing", ["type"] = "string" },
        };
        var defs = new JsonObject();

        SceneJson.HoistNamedTypes(root, defs);

        Assert.Single(defs);
    }

    [Fact]
    public void Rejects_missing_elements()
    {
        Assert.Throws<JsonException>(() => SceneJson.Deserialize("""{"formatVersion":1}"""));
    }

    [Fact]
    public void Version_1_is_read_as_version_3()
    {
        const string json = """
            {"formatVersion":1,"elements":[{"kind":"point","id":"019a7d3c-0000-7000-8000-000000000002","status":"existing-keep",
             "measurementSource":"estimated","articleId":null,"position":{"x":1,"y":2},"rotation":0}]}
            """;

        var scene = SceneJson.Deserialize(json);

        Assert.Equal(3, scene.FormatVersion);
        Assert.Null(Assert.IsType<PointElement>(Assert.Single(scene.Elements)).Form);
    }

    [Fact]
    public void New_fields_round_trip()
    {
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion,
        [
            new PointElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, new(1, 2), 0, Form: "tree-large", Width: 6, Height: 9),
            new LineElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, [new(0, 0), new(5, 0)], Form: "hedge", Height: 1.5),
            new BuildingElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, [new(0, 0), new(1, 0), new(1, 1)], 3, RoofShape.Gable, WallColor: "#E3B23C", RoofColor: "#7A3B2E"),
        ]);

        var read = SceneJson.Deserialize(SceneJson.Serialize(scene));

        var point = Assert.IsType<PointElement>(read.Elements[0]);
        Assert.Equal(("tree-large", 6.0, 9.0), (point.Form, point.Width!.Value, point.Height!.Value));
        var line = Assert.IsType<LineElement>(read.Elements[1]);
        Assert.Equal(("hedge", 1.5), (line.Form, line.Height!.Value));
        var building = Assert.IsType<BuildingElement>(read.Elements[2]);
        Assert.Equal(("#E3B23C", "#7A3B2E"), (building.WallColor, building.RoofColor));
    }

    [Fact]
    public void Null_new_fields_are_omitted()
    {
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion,
            [new PointElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, ArticleId, new(1, 2), 0)]);

        var json = SceneJson.Serialize(scene);

        Assert.DoesNotContain("\"form\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"width\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_version_2_and_upgrades_to_3_with_no_views()
    {
        var json = SceneJson.Serialize(EveryKind() with { FormatVersion = 3, Views = [] }).Replace("\"formatVersion\":3", "\"formatVersion\":2", StringComparison.Ordinal).Replace(",\"views\":[]", "", StringComparison.Ordinal);

        var scene = SceneJson.Deserialize(json);

        Assert.Equal(3, scene.FormatVersion);
        Assert.NotNull(scene.Views);
        Assert.Empty(scene.Views);
    }

    [Fact]
    public void Round_trips_a_view()
    {
        var view = new SceneView(Ids.New(), "Från altanen", new Point3(5, -9, 1.7), new Point3(10, 8, 1), 50);
        var scene = EveryKind() with { Views = [view] };

        var read = SceneJson.Deserialize(SceneJson.Serialize(scene));

        Assert.Equal(view, Assert.Single(read.ViewsOrEmpty));
        Assert.Contains("\"views\":[{", SceneJson.Serialize(scene), StringComparison.Ordinal);
    }

    [Fact]
    public void Always_writes_views_even_when_none_were_given()
    {
        Assert.Contains("\"views\":[]", SceneJson.Serialize(EveryKind()), StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_version_4()
    {
        var json = SceneJson.Serialize(EveryKind()).Replace("\"formatVersion\":3", "\"formatVersion\":4", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => SceneJson.Deserialize(json));
    }
}
