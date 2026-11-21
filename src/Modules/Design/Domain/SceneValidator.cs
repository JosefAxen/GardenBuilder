using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Design.Domain;

/// <summary>
/// One broken rule in a scene. <see cref="Code"/> is stable and the client translates it to Swedish;
/// <see cref="Message"/> is English for logs and for feedback to an AI model.
/// </summary>
/// <param name="ElementId">The element the error is about, or null when it concerns the whole document.</param>
internal sealed record SceneError(string Code, Guid? ElementId, string Message);

/// <summary>
/// Checks that a scene is sensible before it touches a design (docs/design.md, "AI-flödena": AI output is validated by
/// plain code). Reading the JSON already guarantees the shape; this checks the content. Each element gets at most one
/// error, the most basic one, so a far-away tree is reported as out of range and not also as outside the plot.
/// </summary>
internal static class SceneValidator
{
    /// <summary>No garden is a kilometer from its own origin; larger values are bugs or bad AI output.</summary>
    public const double MaxCoordinate = 1000;

    /// <summary>
    /// The most corners one surface, building, plot or line may have. A drawn garden needs tens; the limit keeps the
    /// self-intersection check (every pair of edges) cheap, since anyone can send a scene to the price endpoint.
    /// </summary>
    public const int MaxPoints = 1000;

    public const double MinPolygonArea = 0.01;

    /// <summary>
    /// Area divided by perimeter squared: about 0.06 for a square and about width / (4 × length) for a long strip.
    /// Below this a polygon is a sliver (a strip more than about 250 times longer than wide), not a plot or a bed.
    /// </summary>
    public const double MinCompactness = 0.001;

    public const double MaxBuildingHeight = 30;

    /// <summary>How far outside the plot a point may lie: fences and hedges stand on the boundary.</summary>
    public const double PlotTolerance = 0.5;

    /// <summary>The largest width or height an existing thing without an article may have, in meters.</summary>
    public const double MaxThingSize = 40;

    public const int MaxViews = 8;

    public const int MaxViewName = 40;

    /// <summary>A view may stand outside the plot, on the street or a neighbour's lawn, but not far away.</summary>
    public const double ViewTolerance = 10;

    public const double MinEyeHeight = 0.5;

    public const double MaxViewHeight = 30;

    public const double MinFieldOfView = 20;

    public const double MaxFieldOfView = 90;

    public static IReadOnlyList<SceneError> Validate(SceneDocument scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var errors = new List<SceneError>();

        if (scene.FormatVersion != SceneDocument.CurrentFormatVersion)
        {
            errors.Add(new("format-version", null, $"Format version {scene.FormatVersion} is not supported; expected {SceneDocument.CurrentFormatVersion}."));
        }

        var seen = new HashSet<Guid>();
        foreach (var element in scene.Elements.Where(e => !seen.Add(e.Id)))
        {
            errors.Add(new("duplicate-id", element.Id, $"More than one element has id {element.Id}."));
        }

        var plotCount = scene.Elements.Count(e => e is PlotElement);
        if (plotCount != 1)
        {
            errors.Add(new("plot-count", null, $"A scene needs exactly one plot; found {plotCount}."));
        }

        Polygon? plot = null;
        var toPlace = new List<SceneElement>();
        foreach (var element in scene.Elements)
        {
            var error = CheckGeometry(element) ?? CheckArticle(element) ?? CheckLook(element);
            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            if (element is PlotElement plotElement)
            {
                if (plotCount == 1)
                {
                    plot = new Polygon(plotElement.Points);
                }
            }
            else
            {
                toPlace.Add(element);
            }
        }

        // Only a usable plot can tell inside from outside; without one, the plot error above already says enough.
        if (plot is not null)
        {
            errors.AddRange(toPlace
                .Where(e => PointsOf(e).Any(p => plot.DistanceTo(p) > PlotTolerance))
                .Select(e => new SceneError("outside-plot", e.Id, $"Element {e.Id} lies more than {PlotTolerance} m outside the plot.")));
        }

        errors.AddRange(CheckViews(scene.ViewsOrEmpty, plot));
        return errors;
    }

    private static IEnumerable<SceneError> CheckViews(IReadOnlyList<SceneView> views, Polygon? plot)
    {
        if (views.Count > MaxViews)
        {
            yield return new SceneError("too-many-views", null, $"A design may have at most {MaxViews} views; found {views.Count}.");
            yield break;
        }

        foreach (var view in views)
        {
            var name = view.Name?.Trim() ?? string.Empty;
            if (name.Length is 0 or > MaxViewName)
            {
                yield return new SceneError("bad-view-name", view.Id, $"View {view.Id} needs a name of 1 to {MaxViewName} characters.");
            }
            else if (view.Eye.Height is < MinEyeHeight or > MaxViewHeight || view.Target.Height is < 0 or > MaxViewHeight)
            {
                yield return new SceneError("bad-view-height", view.Id, $"View {view.Id} has an eye height outside {MinEyeHeight}–{MaxViewHeight} m or a target outside 0–{MaxViewHeight} m.");
            }
            else if (view.FieldOfView is < MinFieldOfView or > MaxFieldOfView)
            {
                yield return new SceneError("bad-field-of-view", view.Id, $"View {view.Id} has a field of view outside {MinFieldOfView}–{MaxFieldOfView} degrees.");
            }
            else if (plot is not null
                && (plot.DistanceTo(new Point2(view.Eye.X, view.Eye.Y)) > ViewTolerance || plot.DistanceTo(new Point2(view.Target.X, view.Target.Y)) > ViewTolerance))
            {
                yield return new SceneError("view-outside-plot", view.Id, $"View {view.Id} stands or looks more than {ViewTolerance} m outside the plot.");
            }
        }
    }

    private static IEnumerable<Point2> PointsOf(SceneElement element) => element switch
    {
        BuildingElement building => building.Points,
        SurfaceElement surface => surface.Points,
        LineElement line => line.Points,
        PointElement point => [point.Position],
        _ => [],
    };

    /// <summary>
    /// An existing thing without an article is drawn from its own form and size, so it needs a form; sizes and colours
    /// must be usable when given.
    /// </summary>
    private static SceneError? CheckLook(SceneElement element)
    {
        var (articleId, form, sizes, colors) = element switch
        {
            PointElement p => (p.ArticleId, p.Form, new[] { p.Width, p.Height }, Array.Empty<string?>()),
            LineElement l => (l.ArticleId, l.Form, new[] { l.Height }, Array.Empty<string?>()),
            BuildingElement b => (Guid.Empty, "building", Array.Empty<double?>(), new[] { b.WallColor, b.RoofColor }),
            _ => (Guid.Empty, "other", Array.Empty<double?>(), Array.Empty<string?>()),
        };

        if (articleId is null && string.IsNullOrWhiteSpace(form))
        {
            return new SceneError("missing-form", element.Id, $"Element {element.Id} has neither an article nor a form.");
        }

        if (sizes.Any(s => s is { } size && size is <= 0 or > MaxThingSize))
        {
            return new SceneError("bad-size", element.Id, $"Element {element.Id} has a width or height outside (0, {MaxThingSize}] m.");
        }

        return colors.Any(c => c is not null && !IsHexColor(c))
            ? new SceneError("bad-color", element.Id, $"Building {element.Id} has a colour that is not #RRGGBB.")
            : null;
    }

    private static bool IsHexColor(string color) =>
        color.Length == 7 && color[0] == '#' && color.Skip(1).All(char.IsAsciiHexDigit);

    private static SceneError? CheckGeometry(SceneElement element) => element switch
    {
        PlotElement plot => CheckPolygon(plot.Id, plot.Points),
        SurfaceElement surface => CheckPolygon(surface.Id, surface.Points),
        BuildingElement building => CheckPolygon(building.Id, building.Points) ?? CheckBuildingHeight(building),
        LineElement line => CheckPoints(line.Id, line.Points, minimum: 2),
        PointElement point => IsInRange(point.Position) && double.IsFinite(point.Rotation)
            ? null
            : OutOfRange(point.Id),
        _ => null,
    };

    private static SceneError? CheckPolygon(Guid id, IReadOnlyList<Point2> points)
    {
        if (CheckPoints(id, points, minimum: 3) is { } pointsError)
        {
            return pointsError;
        }

        var polygon = new Polygon(points);
        if (polygon.IsSelfIntersecting)
        {
            return new SceneError("self-intersecting", id, $"Polygon {id} crosses itself.");
        }

        var area = polygon.Area.SquareMeters;
        var perimeter = polygon.Perimeter.Meters;
        return area < MinPolygonArea || area / (perimeter * perimeter) < MinCompactness
            ? new SceneError("degenerate-polygon", id, $"Polygon {id} has almost no area or is a thin sliver.")
            : null;
    }

    private static SceneError? CheckPoints(Guid id, IReadOnlyList<Point2> points, int minimum)
    {
        // First, so no other check ever runs over a huge list of points.
        if (points.Count > MaxPoints)
        {
            return new SceneError("too-many-points", id, $"Element {id} has {points.Count} points; at most {MaxPoints} are allowed.");
        }

        if (!points.All(IsInRange))
        {
            return OutOfRange(id);
        }

        return points.Count < minimum
            ? new SceneError("too-few-points", id, $"Element {id} has {points.Count} points; it needs at least {minimum}.")
            : null;
    }

    private static SceneError? CheckBuildingHeight(BuildingElement building)
    {
        if (!double.IsFinite(building.Height))
        {
            return OutOfRange(building.Id);
        }

        return building.Height is <= 0 or > MaxBuildingHeight
            ? new SceneError("building-height", building.Id, $"Building {building.Id} is {building.Height} m high; allowed is above 0 and up to {MaxBuildingHeight} m.")
            : null;
    }

    /// <summary>Everything new that costs money must point at a catalog article; existing things may lack one.</summary>
    private static SceneError? CheckArticle(SceneElement element)
    {
        var articleId = element switch
        {
            SurfaceElement surface => surface.ArticleId,
            LineElement line => line.ArticleId,
            PointElement point => point.ArticleId,
            _ => Guid.Empty, // The plot and buildings are not priced.
        };

        return element.Status == ElementStatus.New && articleId is null
            ? new SceneError("missing-article", element.Id, $"New element {element.Id} does not point at a catalog article.")
            : null;
    }

    private static bool IsInRange(Point2 point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && Math.Abs(point.X) <= MaxCoordinate && Math.Abs(point.Y) <= MaxCoordinate;

    private static SceneError OutOfRange(Guid id) =>
        new("out-of-range", id, $"Element {id} has a coordinate or value that is not a finite number within {MaxCoordinate} m.");
}
