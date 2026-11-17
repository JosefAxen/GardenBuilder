using GardenBuilder.Contracts.Scenes;

namespace GardenBuilder.Modules.Catalog.Domain;

/// <summary>
/// The form an article is drawn with (docs/superpowers/specs/2026-11-17-model-library-design.md). The form also decides
/// the article's family, what kind of scene element it becomes and which unit it is sold in. The form is only how the
/// editor draws the article: the article itself is what the element is.
/// </summary>
internal enum TypeModel
{
    TreeLarge,
    TreeFruit,
    TreeSmall,
    Pine,
    Spruce,
    ConiferColumn,
    ShrubRound,
    ShrubFlowering,
    PerennialTall,
    PerennialLow,
    FoliagePlant,
    OrnamentalGrass,
    Rock,
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
    private sealed record Facts(string Key, string Family, ArticleGeometry Geometry, SurfaceMaterial? Material);

    private static readonly HashSet<string> PlantFamilies = new(StringComparer.Ordinal) { "deciduous-tree", "conifer", "shrub", "perennial", "hedge" };

    private static readonly Dictionary<TypeModel, Facts> All = new()
    {
        [TypeModel.TreeLarge] = new("tree-large", "deciduous-tree", ArticleGeometry.Point, null),
        [TypeModel.TreeFruit] = new("tree-fruit", "deciduous-tree", ArticleGeometry.Point, null),
        [TypeModel.TreeSmall] = new("tree-small", "deciduous-tree", ArticleGeometry.Point, null),
        [TypeModel.Pine] = new("pine", "conifer", ArticleGeometry.Point, null),
        [TypeModel.Spruce] = new("spruce", "conifer", ArticleGeometry.Point, null),
        [TypeModel.ConiferColumn] = new("conifer-column", "conifer", ArticleGeometry.Point, null),
        [TypeModel.ShrubRound] = new("shrub-round", "shrub", ArticleGeometry.Point, null),
        [TypeModel.ShrubFlowering] = new("shrub-flowering", "shrub", ArticleGeometry.Point, null),
        [TypeModel.PerennialTall] = new("perennial-tall", "perennial", ArticleGeometry.Point, null),
        [TypeModel.PerennialLow] = new("perennial-low", "perennial", ArticleGeometry.Point, null),
        [TypeModel.FoliagePlant] = new("foliage-plant", "perennial", ArticleGeometry.Point, null),
        [TypeModel.OrnamentalGrass] = new("ornamental-grass", "perennial", ArticleGeometry.Point, null),
        [TypeModel.Rock] = new("rock", "furniture", ArticleGeometry.Point, null),
        [TypeModel.Pot] = new("pot", "pot", ArticleGeometry.Point, null),
        [TypeModel.Furniture] = new("furniture", "furniture", ArticleGeometry.Point, null),
        [TypeModel.Hedge] = new("hedge", "hedge", ArticleGeometry.Line, null),
        [TypeModel.Fence] = new("fence", "fence", ArticleGeometry.Line, null),
        [TypeModel.SurfaceGrass] = new("surface-grass", "surface-grass", ArticleGeometry.Surface, SurfaceMaterial.Grass),
        [TypeModel.SurfacePaving] = new("surface-paving", "surface-paving", ArticleGeometry.Surface, SurfaceMaterial.Paving),
        [TypeModel.SurfaceGravel] = new("surface-gravel", "surface-gravel", ArticleGeometry.Surface, SurfaceMaterial.Gravel),
        [TypeModel.SurfaceDecking] = new("surface-decking", "surface-decking", ArticleGeometry.Surface, SurfaceMaterial.Decking),
        [TypeModel.SurfaceBed] = new("surface-bed", "surface-bed", ArticleGeometry.Surface, SurfaceMaterial.Bed),
    };

    private static readonly Dictionary<string, TypeModel> ByKey = All.ToDictionary(p => p.Value.Key, p => p.Key, StringComparer.Ordinal);

    public static string Key(TypeModel model) => All[model].Key;

    /// <summary>All keys in declaration order, for messages that list them.</summary>
    public static IReadOnlyList<string> Keys { get; } = Enum.GetValues<TypeModel>().Select(m => All[m].Key).ToArray();

    /// <summary>
    /// The kind of thing the form stands for, e.g. "conifer" for "conifer-column": the old type model key. Pricing and the
    /// plant rules use it, so a new form never changes a price.
    /// </summary>
    public static string Family(TypeModel model) => All[model].Family;

    public static bool TryParse(string key, out TypeModel model) => ByKey.TryGetValue(key, out model);

    public static ArticleGeometry Geometry(TypeModel model) => All[model].Geometry;

    /// <summary>Points are sold per piece, lines per meter, surfaces per square meter.</summary>
    public static ArticleUnit Unit(TypeModel model) => Geometry(model) switch
    {
        ArticleGeometry.Point => ArticleUnit.Piece,
        ArticleGeometry.Line => ArticleUnit.Meter,
        _ => ArticleUnit.SquareMeter,
    };

    public static bool IsPlant(TypeModel model) => PlantFamilies.Contains(All[model].Family);

    /// <summary>The scene's surface material for surface models, so the scene and the catalog say the same thing.</summary>
    public static SurfaceMaterial? Material(TypeModel model) => All[model].Material;
}
