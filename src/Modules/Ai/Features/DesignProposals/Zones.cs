using Clipper2Lib;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Features.DesignProposals;

internal enum ZoneLight
{
    Sun,
    PartShade,
    Shade,
}

internal enum ZoneKind
{
    House,
    Boundary,
    Middle,
}

/// <summary>A named part of the free ground that a design proposal can use.</summary>
/// <param name="Key">"house-south", "boundary-north", "middle" … Stable, used by the model to name a zone.</param>
/// <param name="Name">Swedish, e.g. "söder om huset", "längs norra gränsen", "mitten".</param>
/// <param name="Facing">"north", "south", "east" or "west" for house and boundary zones; null for the middle.</param>
/// <param name="Outline">Rings under the even-odd rule: outer edges and the holes cut by things that stay.</param>
internal sealed record Zone(string Key, string Name, ZoneKind Kind, string? Facing, IReadOnlyList<Point2[]> Outline, double Area, ZoneLight Light);

/// <summary>
/// Divides the free ground into named zones with light (docs/superpowers/specs/2026-11-18-design-proposals-design.md,
/// section 1). Plain code: the model only ever names a zone, never draws one.
/// </summary>
internal static class Zones
{
    public const int MaxZones = 8;
    public const double MinZoneArea = 4;
    public const double HouseDepth = 4;
    public const double BoundaryDepth = 3;
    public const double MinFacade = 2;

    /// <summary>The margin around surfaces and lines that stay.</summary>
    public const double SurfaceMargin = 0.3;

    /// <summary>A plant without a known width is taken to be this wide.</summary>
    public const double DefaultPlantWidth = 1;

    private const int Precision = 3;
    private const double ShadeReach = 1.5;

    private static readonly string[] Compass = ["south", "west", "east", "north"];

    private static readonly Dictionary<string, string> HouseNames = new(StringComparer.Ordinal)
    {
        ["north"] = "norr om huset", ["south"] = "söder om huset", ["east"] = "öster om huset", ["west"] = "väster om huset",
    };

    private static readonly Dictionary<string, string> BoundaryNames = new(StringComparer.Ordinal)
    {
        ["north"] = "längs norra gränsen", ["south"] = "längs södra gränsen", ["east"] = "längs östra gränsen", ["west"] = "längs västra gränsen",
    };

    /// <summary>The compass side an edge faces: the outward normal of a counter-clockwise edge, to the nearest of four.</summary>
    public static string Facing(Point2 from, Point2 to)
    {
        var (nx, ny) = (to.Y - from.Y, -(to.X - from.X));
        return Math.Abs(ny) >= Math.Abs(nx) ? (ny > 0 ? "north" : "south") : (nx > 0 ? "east" : "west");
    }

    /// <summary>
    /// The ground a proposal may use: the plot minus the house and everything that stays (with margin). Lawns and
    /// things the customer marked for removal are free.
    /// </summary>
    public static PathsD FreeGround(SceneDocument scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Elements.OfType<PlotElement>().FirstOrDefault() is not { } plot)
        {
            return [];
        }

        var blocked = new PathsD();
        foreach (var element in scene.Elements)
        {
            blocked.AddRange(Blocked(element));
        }

        var free = new PathsD { Path(plot.Points) };
        return blocked.Count == 0 ? free : Clipper.Difference(free, Clipper.Union(blocked, FillRule.NonZero), FillRule.NonZero, Precision);
    }

    public static IReadOnlyList<Zone> Divide(SceneDocument scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var free = FreeGround(scene);
        var plot = scene.Elements.OfType<PlotElement>().FirstOrDefault();
        if (plot is null || Area(free) < MinZoneArea)
        {
            return [];
        }

        var house = scene.Elements.OfType<BuildingElement>().FirstOrDefault();
        var parts = new List<(string Key, string Name, ZoneKind Kind, string? Facing, PathsD Shape)>();
        var left = free;

        if (house is not null)
        {
            foreach (var side in Compass)
            {
                var strip = Strips(CounterClockwise(house.Points), side, HouseDepth, outward: true);
                Take(ref left, strip, ($"house-{side}", HouseNames[side], ZoneKind.House, side), parts);
            }
        }

        foreach (var side in Compass)
        {
            var strip = Strips(CounterClockwise(plot.Points), side, BoundaryDepth, outward: false);
            Take(ref left, strip, ($"boundary-{side}", BoundaryNames[side], ZoneKind.Boundary, side), parts);
        }

        if (Area(left) > 0)
        {
            parts.Add(("middle", "mitten", ZoneKind.Middle, null, left));
        }

        MergeSmall(parts);
        var trees = scene.Elements.OfType<PointElement>().Where(Stays).ToList();
        return [.. parts.Select(p => new Zone(
            p.Key, p.Name, p.Kind, p.Facing, [.. p.Shape.Select(ToPoints)], Area(p.Shape),
            LightOf(p.Kind, p.Facing, p.Shape, house, trees)))];
    }

    private static void Take(
        ref PathsD left, PathsD strip, (string Key, string Name, ZoneKind Kind, string Facing) zone,
        List<(string Key, string Name, ZoneKind Kind, string? Facing, PathsD Shape)> parts)
    {
        if (strip.Count == 0)
        {
            return;
        }

        var shape = Clipper.Intersect(left, strip, FillRule.NonZero, Precision);
        if (Area(shape) <= 0)
        {
            return;
        }

        parts.Add((zone.Key, zone.Name, zone.Kind, zone.Facing, shape));
        left = Clipper.Difference(left, strip, FillRule.NonZero, Precision);
    }

    /// <summary>Zones under <see cref="MinZoneArea"/> join their nearest neighbour; then the smallest join until at most <see cref="MaxZones"/> are left.</summary>
    private static void MergeSmall(List<(string Key, string Name, ZoneKind Kind, string? Facing, PathsD Shape)> parts)
    {
        while (parts.Count > 1)
        {
            var smallest = parts.Select((p, i) => (Index: i, Area: Area(p.Shape))).MinBy(p => p.Area);
            if (smallest.Area >= MinZoneArea && parts.Count <= MaxZones)
            {
                return;
            }

            var small = parts[smallest.Index];
            var centre = Centroid(small.Shape);
            var nearest = parts
                .Select((p, i) => (Index: i, Distance: i == smallest.Index ? double.MaxValue : Distance(centre, p.Shape)))
                .MinBy(p => p.Distance).Index;
            var into = parts[nearest];
            parts[nearest] = into with { Shape = Clipper.Union(into.Shape, small.Shape, FillRule.NonZero, Precision) };
            parts.RemoveAt(smallest.Index);
        }

        if (parts.Count == 1 && Area(parts[0].Shape) < MinZoneArea)
        {
            parts.Clear();
        }
    }

    private static ZoneLight LightOf(ZoneKind kind, string? facing, PathsD shape, BuildingElement? house, IReadOnlyList<PointElement> trees)
    {
        var centre = Centroid(shape);
        if (trees.Any(t => t.Position.DistanceTo(centre) <= (WidthOf(t) / 2) + 1))
        {
            return ZoneLight.PartShade;
        }

        if (house is null)
        {
            return ZoneLight.Sun;
        }

        var side = kind == ZoneKind.House ? facing! : SideOf(centre, house.Points);
        return side switch
        {
            "north" => new Polygon(house.Points).DistanceTo(centre) <= house.Height * ShadeReach ? ZoneLight.Shade : ZoneLight.Sun,
            "east" => ZoneLight.PartShade,
            _ => ZoneLight.Sun,
        };
    }

    /// <summary>Which side of the house a point is on, by the direction from the house's centre.</summary>
    private static string SideOf(Point2 point, IReadOnlyList<Point2> house)
    {
        var centre = new Point2(house.Average(p => p.X), house.Average(p => p.Y));
        return DirectionOf(point.X - centre.X, point.Y - centre.Y);
    }

    private static string DirectionOf(double dx, double dy) =>
        Math.Abs(dy) >= Math.Abs(dx) ? (dy > 0 ? "north" : "south") : (dx > 0 ? "east" : "west");

    /// <summary>Rectangles along every edge of at least <see cref="MinFacade"/> m that faces <paramref name="side"/>, outward or inward.</summary>
    private static PathsD Strips(Point2[] ring, string side, double depth, bool outward)
    {
        var strips = new PathsD();
        for (var i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var length = a.DistanceTo(b);
            if (length < MinFacade || Facing(a, b) != side)
            {
                continue;
            }

            // The outward normal of a counter-clockwise edge; inward strips use the opposite direction.
            var (nx, ny) = ((b.Y - a.Y) / length, -(b.X - a.X) / length);
            var sign = outward ? depth : -depth;
            strips.Add(Path([a, b, new Point2(b.X + (nx * sign), b.Y + (ny * sign)), new Point2(a.X + (nx * sign), a.Y + (ny * sign))]));
        }

        return strips.Count == 0 ? strips : Clipper.Union(strips, FillRule.NonZero);
    }

    private static bool Stays(SceneElement element) => element.Status != ElementStatus.ExistingRemove;

    private static double WidthOf(PointElement point) => point.Width is { } w && w > 0 ? w : DefaultPlantWidth;

    /// <summary>What an element keeps from use, with its margin. The plot, lawns and removed things block nothing.</summary>
    private static PathsD Blocked(SceneElement element)
    {
        if (!Stays(element))
        {
            return [];
        }

        switch (element)
        {
            case BuildingElement building:
                // No margin: a patio lies right against the facade.
                return [Path(building.Points)];
            case SurfaceElement surface when surface.Material != SurfaceMaterial.Grass:
                return Clipper.InflatePaths([Path(surface.Points)], SurfaceMargin, JoinType.Miter, EndType.Polygon, 2, Precision);
            case LineElement line:
                return Clipper.InflatePaths([Path(line.Points)], SurfaceMargin + 0.25, JoinType.Round, EndType.Round, 2, Precision);
            case PointElement point:
                // The crown (half the width) plus a margin of half the width.
                return [Circle(point.Position, WidthOf(point))];
            default:
                return [];
        }
    }

    private static PathD Circle(Point2 centre, double radius) =>
        new(Enumerable.Range(0, 16).Select(i => new PointD(centre.X + (radius * Math.Cos(i * Math.PI / 8)), centre.Y + (radius * Math.Sin(i * Math.PI / 8)))));

    private static Point2[] CounterClockwise(IReadOnlyList<Point2> points)
    {
        var ring = points.ToArray();
        return Clipper.Area(Path(ring)) < 0 ? [.. ring.Reverse()] : ring;
    }

    internal static PathD Path(IEnumerable<Point2> points) => new(points.Select(p => new PointD(p.X, p.Y)));

    internal static Point2[] ToPoints(PathD path) => [.. path.Select(p => new Point2(p.x, p.y))];

    internal static double Area(PathsD shape) => Math.Abs(Clipper.Area(shape));

    /// <summary>The area-weighted centre of all rings (holes count negative).</summary>
    internal static Point2 Centroid(PathsD shape)
    {
        double area = 0, x = 0, y = 0;
        foreach (var ring in shape)
        {
            for (var i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                var cross = (a.x * b.y) - (b.x * a.y);
                area += cross;
                x += (a.x + b.x) * cross;
                y += (a.y + b.y) * cross;
            }
        }

        return area == 0 ? new Point2(0, 0) : new Point2(x / (3 * area), y / (3 * area));
    }

    private static double Distance(Point2 point, PathsD shape) =>
        shape.Count == 0 ? double.MaxValue : shape.Min(ring => new Polygon(ToPoints(ring)).DistanceTo(point));
}
