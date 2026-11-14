using GardenBuilder.Contracts.Scenes;

namespace GardenBuilder.Modules.Catalog.Domain;

/// <summary>
/// The 3D model an article is drawn with. A fixed list until the model library (GLB files) exists; the type model also
/// decides what kind of scene element the article becomes and which unit it is sold in.
/// </summary>
internal enum TypeModel
{
    DeciduousTree,
    Conifer,
    Shrub,
    Perennial,
    Pot,
    Furniture,
    Hedge,
    Fence,
    SurfaceGrass,
    SurfacePaving,
    SurfaceGravel,
    SurfaceDecking,
    SurfaceBed,
}

/// <summary>What kind of scene element an article becomes.</summary>
internal enum ArticleGeometry
{
    Point,
    Line,
    Surface,
}

internal static class TypeModels
{
    private sealed record Facts(string Key, ArticleGeometry Geometry, bool IsPlant, SurfaceMaterial? Material);

    private static readonly Dictionary<TypeModel, Facts> All = new()
    {
        [TypeModel.DeciduousTree] = new("deciduous-tree", ArticleGeometry.Point, IsPlant: true, null),
        [TypeModel.Conifer] = new("conifer", ArticleGeometry.Point, IsPlant: true, null),
        [TypeModel.Shrub] = new("shrub", ArticleGeometry.Point, IsPlant: true, null),
        [TypeModel.Perennial] = new("perennial", ArticleGeometry.Point, IsPlant: true, null),
        [TypeModel.Pot] = new("pot", ArticleGeometry.Point, IsPlant: false, null),
        [TypeModel.Furniture] = new("furniture", ArticleGeometry.Point, IsPlant: false, null),
        [TypeModel.Hedge] = new("hedge", ArticleGeometry.Line, IsPlant: true, null),
        [TypeModel.Fence] = new("fence", ArticleGeometry.Line, IsPlant: false, null),
        [TypeModel.SurfaceGrass] = new("surface-grass", ArticleGeometry.Surface, IsPlant: false, SurfaceMaterial.Grass),
        [TypeModel.SurfacePaving] = new("surface-paving", ArticleGeometry.Surface, IsPlant: false, SurfaceMaterial.Paving),
        [TypeModel.SurfaceGravel] = new("surface-gravel", ArticleGeometry.Surface, IsPlant: false, SurfaceMaterial.Gravel),
        [TypeModel.SurfaceDecking] = new("surface-decking", ArticleGeometry.Surface, IsPlant: false, SurfaceMaterial.Decking),
        [TypeModel.SurfaceBed] = new("surface-bed", ArticleGeometry.Surface, IsPlant: false, SurfaceMaterial.Bed),
    };

    private static readonly Dictionary<string, TypeModel> ByKey = All.ToDictionary(p => p.Value.Key, p => p.Key, StringComparer.Ordinal);

    public static string Key(TypeModel model) => All[model].Key;

    public static bool TryParse(string key, out TypeModel model) => ByKey.TryGetValue(key, out model);

    public static ArticleGeometry Geometry(TypeModel model) => All[model].Geometry;

    /// <summary>Points are sold per piece, lines per meter, surfaces per square meter.</summary>
    public static ArticleUnit Unit(TypeModel model) => Geometry(model) switch
    {
        ArticleGeometry.Point => ArticleUnit.Piece,
        ArticleGeometry.Line => ArticleUnit.Meter,
        _ => ArticleUnit.SquareMeter,
    };

    public static bool IsPlant(TypeModel model) => All[model].IsPlant;

    /// <summary>The scene's surface material for surface models, so the scene and the catalog say the same thing.</summary>
    public static SurfaceMaterial? Material(TypeModel model) => All[model].Material;
}
