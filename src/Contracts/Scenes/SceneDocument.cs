using System.Text.Json.Serialization;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Contracts.Scenes;

/// <summary>
/// The scene document: the system's most important contract (docs/design.md, "Scendokumentet").
/// The editor, the AI, pricing and admin all read and write this format. Coordinates are meters in a local
/// system where +X is east and +Y is north. The ground is flat. TypeScript types and the JSON schema are generated
/// from these types (npm --prefix src/web run gen:scene).
/// </summary>
public sealed record SceneDocument(int FormatVersion, IReadOnlyList<SceneElement> Elements)
{
    /// <summary>Bump when the format changes in a way old documents must be upgraded for.</summary>
    /// <remarks>Version 2 added form and size for existing things without an article, and the house's colours.</remarks>
    public const int CurrentFormatVersion = 2;

    /// <summary>The oldest version <see cref="SceneJson.Deserialize"/> still reads and upgrades.</summary>
    public const int OldestReadableFormatVersion = 1;
}

/// <summary>One thing in the scene. The JSON property "kind" says which type it is.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PlotElement), "plot")]
[JsonDerivedType(typeof(BuildingElement), "building")]
[JsonDerivedType(typeof(SurfaceElement), "surface")]
[JsonDerivedType(typeof(LineElement), "line")]
[JsonDerivedType(typeof(PointElement), "point")]
public abstract record SceneElement(Guid Id, ElementStatus Status, MeasurementSource MeasurementSource);

/// <summary>The plot boundary. Exactly one per scene. Not priced.</summary>
public sealed record PlotElement(
    Guid Id,
    ElementStatus Status,
    MeasurementSource MeasurementSource,
    IReadOnlyList<Point2> Points,
    bool Sloped)
    : SceneElement(Id, Status, MeasurementSource);

/// <summary>A house, garage or shed. Not priced.</summary>
/// <param name="WallColor">"#RRGGBB", or null to draw the default colour.</param>
/// <param name="RoofColor">"#RRGGBB", or null to draw the default colour.</param>
public sealed record BuildingElement(
    Guid Id,
    ElementStatus Status,
    MeasurementSource MeasurementSource,
    IReadOnlyList<Point2> Points,
    double Height,
    RoofShape Roof,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WallColor = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RoofColor = null)
    : SceneElement(Id, Status, MeasurementSource);

/// <summary>Lawn, paving, gravel, decking or a bed. Priced per square meter.</summary>
public sealed record SurfaceElement(
    Guid Id,
    ElementStatus Status,
    MeasurementSource MeasurementSource,
    Guid? ArticleId,
    IReadOnlyList<Point2> Points,
    SurfaceMaterial Material)
    : SceneElement(Id, Status, MeasurementSource);

/// <summary>A hedge, fence or edging along two or more points. Priced per meter.</summary>
/// <param name="Form">What to draw when there is no article, e.g. an existing hedge: "hedge" or "fence".</param>
/// <param name="Height">Meters, for an existing line without an article.</param>
public sealed record LineElement(
    Guid Id,
    ElementStatus Status,
    MeasurementSource MeasurementSource,
    Guid? ArticleId,
    IReadOnlyList<Point2> Points,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Form = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Height = null)
    : SceneElement(Id, Status, MeasurementSource);

/// <summary>A tree, shrub, pot or piece of furniture. Priced per piece.</summary>
/// <param name="Rotation">Radians, counter-clockwise seen from above.</param>
/// <param name="Form">What to draw when there is no article, e.g. an existing tree: a form key such as "tree-large".</param>
/// <param name="Width">Crown or plant width in meters, for an existing thing without an article.</param>
/// <param name="Height">Height in meters, for an existing thing without an article.</param>
public sealed record PointElement(
    Guid Id,
    ElementStatus Status,
    MeasurementSource MeasurementSource,
    Guid? ArticleId,
    Point2 Position,
    double Rotation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Form = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Width = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Height = null)
    : SceneElement(Id, Status, MeasurementSource);

/// <summary>Whether an element exists today and is kept, exists and is removed, or is new.</summary>
public enum ElementStatus
{
    ExistingKeep,
    ExistingRemove,
    New,
}

/// <summary>Where an element's measurements come from. Drives how wide the price range is.</summary>
public enum MeasurementSource
{
    /// <summary>Estimated from a photo or map data.</summary>
    Estimated,

    /// <summary>Measured by the customer.</summary>
    Customer,

    /// <summary>Verified by a salesperson on site.</summary>
    Verified,
}

public enum RoofShape
{
    Flat,
    Gable,
}

/// <summary>How a surface is drawn when it has no article, e.g. an existing lawn.</summary>
public enum SurfaceMaterial
{
    Grass,
    Paving,
    Gravel,
    Decking,
    Bed,
}
