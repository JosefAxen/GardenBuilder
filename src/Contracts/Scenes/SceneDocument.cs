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
    public const int CurrentFormatVersion = 1;
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
public sealed record BuildingElement(
    Guid Id,
    ElementStatus Status,
    MeasurementSource MeasurementSource,
    IReadOnlyList<Point2> Points,
    double Height,
    RoofShape Roof)
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
public sealed record LineElement(
    Guid Id,
    ElementStatus Status,
    MeasurementSource MeasurementSource,
    Guid? ArticleId,
    IReadOnlyList<Point2> Points)
    : SceneElement(Id, Status, MeasurementSource);

/// <summary>A tree, shrub, pot or piece of furniture. Priced per piece.</summary>
/// <param name="Rotation">Radians, counter-clockwise seen from above.</param>
public sealed record PointElement(
    Guid Id,
    ElementStatus Status,
    MeasurementSource MeasurementSource,
    Guid? ArticleId,
    Point2 Position,
    double Rotation)
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
