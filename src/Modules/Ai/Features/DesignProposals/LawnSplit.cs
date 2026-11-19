using Clipper2Lib;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Features.DesignProposals;

/// <summary>
/// Today's lawn is free ground for a proposal, but taking it away costs work. Where new things cover a lawn, the lawn
/// keeps what is left (same id) and the covered part becomes its own element marked for removal, so pricing counts it.
/// </summary>
internal static class LawnSplit
{
    /// <summary>Pieces smaller than this, kept or cut, are not worth an element of their own.</summary>
    public const double MinPiece = 0.5;

    private const int Precision = 3;

    public static IReadOnlyList<SceneElement> Apply(IReadOnlyList<SceneElement> elements, IReadOnlyList<Point2[]> used)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(used);
        if (used.Count == 0)
        {
            return elements;
        }

        var usedShape = Clipper.Union(new PathsD(used.Select(Zones.Path)), FillRule.NonZero);
        return [.. elements.SelectMany(e => e is SurfaceElement { Material: SurfaceMaterial.Grass, Status: ElementStatus.ExistingKeep } lawn
            ? Split(lawn, usedShape)
            : [e])];
    }

    private static List<SceneElement> Split(SurfaceElement lawn, PathsD used)
    {
        var shape = new PathsD { Zones.Path(lawn.Points) };
        var cut = Pieces(Clipper.Intersect(shape, used, FillRule.NonZero, Precision));
        if (cut.Count == 0)
        {
            return [lawn];
        }

        // A lawn is one outline: where new things sit inside it (a bed in the middle) the outline stays and only the
        // removal is recorded; the new things are drawn on top.
        var kept = Pieces(Clipper.Difference(shape, used, FillRule.NonZero, Precision));
        var result = new List<SceneElement>();
        for (var i = 0; i < kept.Count; i++)
        {
            result.Add(i == 0 ? lawn with { Points = kept[i] } : lawn with { Id = Ids.New(), Points = kept[i] });
        }

        result.AddRange(cut.Select(points => (SceneElement)(lawn with { Id = Ids.New(), Status = ElementStatus.ExistingRemove, Points = points })));
        return result;
    }

    /// <summary>The outer rings at least <see cref="MinPiece"/> m², largest first. Holes are left out.</summary>
    private static List<Point2[]> Pieces(PathsD shape) =>
        [.. shape.Where(ring => Clipper.Area(ring) >= MinPiece).OrderByDescending(Clipper.Area).Select(Zones.ToPoints)];
}
