using System.Globalization;
using Clipper2Lib;
using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Features.PhotoInterpretation;

/// <summary>
/// Plain code between Claude and the scene (docs/superpowers/specs/2026-11-18-photo-interpretation-design.md, section 4):
/// moves each photo's camera-relative map onto the plot with the camera pose the customer gave, drops what is unsure,
/// absurd or outside, clips lawns and beds to the plot, merges what several photos saw twice, and maps Claude's words to
/// scene elements. It never throws on a bad answer; what it cannot use ends up in <see cref="PhotoProposal.Dropped"/>.
/// </summary>
internal static class PhotoPlacement
{
    /// <summary>How far outside the plot a thing may lie, the same as the scene check (fences stand on the boundary).</summary>
    public const double PlotTolerance = 0.5;

    public const double MinConfidence = 0.3;

    /// <summary>Nothing in a garden photo is further than this from the camera; anything beyond is a bad answer.</summary>
    public const double MaxCameraDistance = 200;

    /// <summary>Surfaces of the same material merge when they overlap by this share of the smaller one (lawns always).</summary>
    public const double SurfaceOverlap = 0.3;

    /// <summary>Lines merge when this share of the shorter lies within <see cref="LineDistance"/> of the other.</summary>
    public const double LineShare = 0.5;

    public const double LineDistance = 1.5;

    /// <summary>Clipped bits smaller than this (square meters) are slivers along the boundary, not lawns.</summary>
    private const double MinSurfaceArea = 0.5;

    private const double DefaultWidth = 1;
    private const double MaxSize = 40;
    private const double LargeTreeHeight = 6;
    private const int Precision = 3;

    private static readonly string[] ConiferWords = ["barr", "gran", "tall", "tuja", "thuja", "cypress", "conifer", "spruce", "pine", "fir", "en "];

    /// <summary>The colour words Claude uses, longest first so "red-brown" is not read as "red".</summary>
    private static readonly (string Word, string Hex)[] Colors =
    [
        ("red-brown", "#7A3B2E"), ("falu red", "#8E2A1E"), ("falurött", "#8E2A1E"), ("yellow", "#E3B23C"), ("gul", "#E3B23C"),
        ("white", "#F2F0EA"), ("vit", "#F2F0EA"), ("grey", "#9A9C9E"), ("gray", "#9A9C9E"), ("grå", "#9A9C9E"),
        ("black", "#2B2B2B"), ("svart", "#2B2B2B"), ("brown", "#6B4A32"), ("brun", "#6B4A32"), ("green", "#5E7A4A"),
        ("grön", "#5E7A4A"), ("blue", "#5F7FA0"), ("blå", "#5F7FA0"), ("red", "#8E2A1E"), ("röd", "#8E2A1E"),
    ];

    private enum Kind
    {
        Surface,
        Line,
        Point,
    }

    /// <summary>One usable thing on the plot, before duplicates are merged.</summary>
    private sealed record Candidate(
        Kind Kind, string Type, string Label, double Confidence, SurfaceMaterial Material, Point2[] Points, double? Width, double? Height);

    /// <summary>A point in the camera's frame (x right, y ahead) as a point on the plot.</summary>
    public static Point2 ToScene(double x, double y, CameraPose camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var heading = camera.HeadingDegrees * Math.PI / 180;
        var (sin, cos) = Math.SinCos(heading);
        // Ahead is (sin h, cos h) and right is (cos h, -sin h): heading 0 looks north with east to the right.
        return new Point2(camera.Position.X + (y * sin) + (x * cos), camera.Position.Y + (y * cos) - (x * sin));
    }

    public static PhotoProposal Place(IReadOnlyList<(PhotoAnswer Answer, CameraPose Camera)> photos, IReadOnlyList<Point2> plot, IReadOnlyList<Point2> house)
    {
        ArgumentNullException.ThrowIfNull(photos);
        var plotPolygon = new Polygon(plot);
        var housePolygon = house.Count >= 3 ? new Polygon(house) : null;
        var dropped = new List<string>();
        var candidates = new List<Candidate>();

        foreach (var (answer, camera) in photos)
        {
            foreach (var thing in answer.Elements ?? [])
            {
                var (usable, why) = ToCandidates(thing, camera, plotPolygon, housePolygon);
                candidates.AddRange(usable);
                if (why is not null)
                {
                    dropped.Add($"{Label(thing)} ({why})");
                }
            }
        }

        var merged = Merge(candidates);
        return new PhotoProposal([.. merged.Select(ToProposed)], HouseOf(photos), dropped);
    }

    private static string Label(PhotoThing thing) => string.IsNullOrWhiteSpace(thing.Label) ? thing.Type : thing.Label.Trim();

    /// <summary>The usable candidates one thing gives, or none and the Swedish reason it was left out.</summary>
    private static (IReadOnlyList<Candidate> Candidates, string? Dropped) ToCandidates(
        PhotoThing thing, CameraPose camera, Polygon plot, Polygon? house)
    {
        if (!double.IsFinite(thing.Confidence) || thing.Confidence < MinConfidence)
        {
            return ([], "för osäker");
        }

        var type = (thing.Type ?? string.Empty).Trim().ToLowerInvariant();
        (Kind Kind, SurfaceMaterial Material)? mapped = type switch
        {
            "lawn" => (Kind.Surface, SurfaceMaterial.Grass),
            "flowerbed" => (Kind.Surface, SurfaceMaterial.Bed),
            "gravel" => (Kind.Surface, SurfaceMaterial.Gravel),
            "patio" or "path" => (Kind.Surface, SurfaceMaterial.Paving),
            "hedge" or "fence" => (Kind.Line, default),
            "tree" or "shrub" => (Kind.Point, default),
            _ => null,
        };
        if (mapped is not { } map)
        {
            return ([], "stöds inte än");
        }

        var raw = thing.Points ?? [];
        if (raw.Any(p => p is null || p.Length < 2 || !double.IsFinite(p[0]) || !double.IsFinite(p[1])
            || Math.Abs(p[0]) > MaxCameraDistance || Math.Abs(p[1]) > MaxCameraDistance))
        {
            return ([], "orimligt läge");
        }

        var points = raw.Select(p => ToScene(p[0], p[1], camera)).ToArray();
        var label = Label(thing);
        var width = Size(thing.WidthM);
        var height = Size(thing.HeightM);
        return map.Kind switch
        {
            Kind.Surface => Surfaces(type, label, thing.Confidence, map.Material, points, plot),
            Kind.Line => Line(type, label, thing.Confidence, points, height, plot),
            _ => Point(type, label, thing.Confidence, points, width, height, plot, house),
        };
    }

    private static double? Size(double? meters) => meters is { } m && double.IsFinite(m) && m > 0 ? Math.Min(m, MaxSize) : null;

    private static (IReadOnlyList<Candidate>, string?) Surfaces(
        string type, string label, double confidence, SurfaceMaterial material, Point2[] points, Polygon plot)
    {
        if (points.Length < 3)
        {
            return ([], "för få punkter");
        }

        // Clipping a simple outline to the plot gives outlines without holes; slivers along the boundary are left out.
        var parts = Clipper.Intersect([ToPath(points)], [ToPath(plot.Points)], FillRule.NonZero, Precision)
            .Where(path => Math.Abs(Clipper.Area(path)) >= MinSurfaceArea)
            .Select(path => new Candidate(Kind.Surface, type, label, confidence, material, FromPath(path), null, null))
            .ToList();
        return parts.Count == 0 ? ([], "utanför tomten") : (parts, null);
    }

    private static (IReadOnlyList<Candidate>, string?) Line(
        string type, string label, double confidence, Point2[] points, double? height, Polygon plot)
    {
        if (points.Length < 2)
        {
            return ([], "för få punkter");
        }

        if (points.All(p => plot.DistanceTo(p) > PlotTolerance))
        {
            return ([], "utanför tomten");
        }

        // A hedge that runs a little past the corner of the plot is pulled onto the boundary.
        var onPlot = points.Select(p => plot.DistanceTo(p) > PlotTolerance ? NearestOnBoundary(p, plot.Points) : p).ToArray();
        return ([new Candidate(Kind.Line, type, label, confidence, default, onPlot, null, height)], null);
    }

    private static (IReadOnlyList<Candidate>, string?) Point(
        string type, string label, double confidence, Point2[] points, double? width, double? height, Polygon plot, Polygon? house)
    {
        if (points.Length == 0)
        {
            return ([], "för få punkter");
        }

        var at = new Point2(points.Average(p => p.X), points.Average(p => p.Y));
        if (plot.DistanceTo(at) > PlotTolerance)
        {
            return ([], "utanför tomten");
        }

        return house?.Contains(at) == true
            ? ([], "inne i huset")
            : ([new Candidate(Kind.Point, type, label, confidence, default, [at], width, height)], null);
    }

    private static List<Candidate> Merge(List<Candidate> candidates)
    {
        var merged = new List<Candidate>(candidates);
        bool changed;
        do
        {
            changed = false;
            for (var i = 0; i < merged.Count && !changed; i++)
            {
                for (var j = i + 1; j < merged.Count && !changed; j++)
                {
                    if (TryMerge(merged[i], merged[j]) is { } both)
                    {
                        merged[i] = both;
                        merged.RemoveAt(j);
                        changed = true;
                    }
                }
            }
        }
        while (changed);

        return merged;
    }

    private static Candidate? TryMerge(Candidate a, Candidate b)
    {
        if (a.Kind != b.Kind || a.Type != b.Type || a.Material != b.Material)
        {
            return null;
        }

        var best = a.Confidence >= b.Confidence ? a : b;
        switch (a.Kind)
        {
            case Kind.Surface:
                var overlap = Math.Abs(Clipper.Area(Clipper.Intersect([ToPath(a.Points)], [ToPath(b.Points)], FillRule.NonZero, Precision)));
                var smaller = Math.Min(Math.Abs(Clipper.Area(ToPath(a.Points))), Math.Abs(Clipper.Area(ToPath(b.Points))));
                var enough = a.Material == SurfaceMaterial.Grass ? overlap > 0 : overlap >= SurfaceOverlap * smaller;
                if (!enough)
                {
                    return null;
                }

                var union = Clipper.Union([ToPath(a.Points)], [ToPath(b.Points)], FillRule.NonZero, Precision)
                    .OrderByDescending(path => Math.Abs(Clipper.Area(path)))
                    .First();
                return best with { Points = FromPath(union), Confidence = Math.Max(a.Confidence, b.Confidence) };

            case Kind.Line:
                var (shorter, longer) = Length(a.Points) <= Length(b.Points) ? (a, b) : (b, a);
                var samples = Sample(shorter.Points);
                var near = samples.Count(p => DistanceToPolyline(p, longer.Points) <= LineDistance);
                return near >= LineShare * samples.Count
                    ? best with { Points = longer.Points, Confidence = Math.Max(a.Confidence, b.Confidence), Height = Max(a.Height, b.Height) }
                    : null;

            default:
                var reach = Math.Max(a.Width ?? DefaultWidth, b.Width ?? DefaultWidth) / 2;
                if (a.Points[0].DistanceTo(b.Points[0]) >= reach)
                {
                    return null;
                }

                var middle = new Point2((a.Points[0].X + b.Points[0].X) / 2, (a.Points[0].Y + b.Points[0].Y) / 2);
                return best with
                {
                    Points = [middle],
                    Confidence = Math.Max(a.Confidence, b.Confidence),
                    Width = Max(a.Width, b.Width),
                    Height = Max(a.Height, b.Height),
                };
        }
    }

    private static double? Max(double? a, double? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);

    private static ProposedElement ToProposed(Candidate c)
    {
        SceneElement element = c.Kind switch
        {
            Kind.Surface => new SurfaceElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, c.Points, c.Material),
            Kind.Line => new LineElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, c.Points, Form: c.Type, Height: c.Height),
            _ => new PointElement(
                Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, c.Points[0], 0,
                Form: PointForm(c), Width: c.Width, Height: c.Height),
        };
        return new ProposedElement(c.Label, c.Confidence, element);
    }

    private static string PointForm(Candidate c)
    {
        if (c.Type == "shrub")
        {
            return "shrub-round";
        }

        var label = c.Label.ToLowerInvariant() + " ";
        if (ConiferWords.Any(label.Contains))
        {
            return "pine";
        }

        return (c.Height ?? LargeTreeHeight) >= LargeTreeHeight ? "tree-large" : "tree-small";
    }

    private static HouseLook? HouseOf(IReadOnlyList<(PhotoAnswer Answer, CameraPose Camera)> photos)
    {
        var house = photos
            .Select(p => p.Answer.House)
            .Where(h => h is { Visible: true } && double.IsFinite(h.Confidence))
            .OrderByDescending(h => h!.Confidence)
            .FirstOrDefault();
        if (house is null)
        {
            return null;
        }

        var roof = (house.RoofForm ?? string.Empty).Trim().ToLowerInvariant() is "flat" or "shed" ? RoofShape.Flat : RoofShape.Gable;
        return new HouseLook(ColorOf(house.WallColor), ColorOf(house.RoofColor), roof);
    }

    private static string? ColorOf(string? words)
    {
        if (string.IsNullOrWhiteSpace(words))
        {
            return null;
        }

        var text = words.Trim().ToLower(CultureInfo.GetCultureInfo("sv-SE"));
        return Colors.FirstOrDefault(c => text.Contains(c.Word, StringComparison.Ordinal)).Hex;
    }

    private static PathD ToPath(IEnumerable<Point2> points) => new(points.Select(p => new PointD(p.X, p.Y)));

    private static Point2[] FromPath(PathD path) => [.. path.Select(p => new Point2(Math.Round(p.x, Precision), Math.Round(p.y, Precision)))];

    private static double Length(Point2[] points) => points.Zip(points.Skip(1), (p, q) => p.DistanceTo(q)).Sum();

    /// <summary>Points every half meter along a line, ends included.</summary>
    private static List<Point2> Sample(Point2[] points)
    {
        var samples = new List<Point2> { points[0] };
        foreach (var (p, q) in points.Zip(points.Skip(1)))
        {
            var steps = Math.Max(1, (int)Math.Ceiling(p.DistanceTo(q) / 0.5));
            for (var k = 1; k <= steps; k++)
            {
                samples.Add(new Point2(p.X + ((q.X - p.X) * k / steps), p.Y + ((q.Y - p.Y) * k / steps)));
            }
        }

        return samples;
    }

    private static double DistanceToPolyline(Point2 p, Point2[] line) =>
        line.Zip(line.Skip(1), (a, b) => p.DistanceTo(Nearest(p, a, b))).DefaultIfEmpty(p.DistanceTo(line[0])).Min();

    private static Point2 NearestOnBoundary(Point2 p, IReadOnlyList<Point2> polygon) =>
        polygon.Select((a, i) => Nearest(p, a, polygon[(i + 1) % polygon.Count])).MinBy(q => q.DistanceTo(p));

    private static Point2 Nearest(Point2 p, Point2 a, Point2 b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        var t = lengthSquared == 0 ? 0 : Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared, 0, 1);
        return new Point2(a.X + (t * dx), a.Y + (t * dy));
    }
}
