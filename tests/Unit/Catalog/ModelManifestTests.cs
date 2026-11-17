using System.Text.Json;
using GardenBuilder.Modules.Catalog.Domain;

namespace GardenBuilder.UnitTests.Catalog;

/// <summary>The model manifest (assets/models/manifest.json) and the catalog's forms must name the same forms.</summary>
public sealed class ModelManifestTests
{
    private sealed record Entry(string Key, string Family);

    private static List<Entry> Manifest()
    {
        var path = Path.Combine(RepoRoot.Path, "assets", "models", "manifest.json");
        return JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path), JsonSerializerOptions.Web)!;
    }

    [Fact]
    public void Manifest_matches_the_catalog_forms()
    {
        var pointForms = Enum.GetValues<TypeModel>()
            .Where(m => TypeModels.Geometry(m) == ArticleGeometry.Point)
            .ToDictionary(TypeModels.Key, TypeModels.Family);

        var manifest = Manifest();

        Assert.Equal(pointForms.Keys.Order(), manifest.Select(e => e.Key).Order());
        Assert.All(manifest, e => Assert.Equal(pointForms[e.Key], e.Family));
    }
}
