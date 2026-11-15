using System.Text.Json;
using System.Text.Json.Nodes;
using GardenBuilder.Contracts.Scenes;

namespace GardenBuilder.UnitTests.Scenes;

public sealed class SceneSchemaTests
{
    private static JsonObject Defs => SceneJson.ExportSchema()["$defs"]!.AsObject();

    [Theory]
    [InlineData("SceneElement")]
    [InlineData("PlotElement")]
    [InlineData("BuildingElement")]
    [InlineData("SurfaceElement")]
    [InlineData("LineElement")]
    [InlineData("PointElement")]
    [InlineData("Point2")]
    [InlineData("ElementStatus")]
    [InlineData("MeasurementSource")]
    [InlineData("RoofShape")]
    [InlineData("SurfaceMaterial")]
    public void Schema_names_every_scene_type_once_in_defs(string typeName)
    {
        Assert.True(Defs.ContainsKey(typeName), $"$defs is missing {typeName}");
    }

    [Fact]
    public void Schema_pins_kind_per_element_type()
    {
        var point = Defs["PointElement"]!;

        Assert.Equal("point", point["properties"]!["kind"]!["const"]!.GetValue<string>());
        Assert.Contains("kind", point["required"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void Schema_disallows_additional_properties()
    {
        Assert.False(Defs["PlotElement"]!["additionalProperties"]!.GetValue<bool>());
    }

    [Fact]
    public void Committed_schema_is_up_to_date()
    {
        var path = Path.Combine(RepoRoot.Path, "src", "web", "src", "api", "scene.schema.json");
        var committed = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

        var current = SceneJson.ExportSchema().ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";

        Assert.True(
            Normalize(committed) == Normalize(current),
            "src/web/src/api/scene.schema.json is out of date. Run npm --prefix src/web run gen:scene");
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
