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

    public const double MinPolygonArea = 0.01;

    public const double MaxBuildingHeight = 30;

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
        var pointsToPlace = new List<PointElement>();
        foreach (var element in scene.Elements)
        {
            var error = CheckGeometry(element) ?? CheckArticle(element);
            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            if (element is PlotElement plotElement && plotCount == 1)
            {
                plot = new Polygon(plotElement.Points);
            }
            else if (element is PointElement point)
            {
                pointsToPlace.Add(point);
            }
        }

        // Only a usable plot can tell inside from outside; without one, the plot error above already says enough.
        if (plot is not null)
        {
            errors.AddRange(pointsToPlace
                .Where(p => !plot.Contains(p.Position))
                .Select(p => new SceneError("outside-plot", p.Id, $"Point element {p.Id} at ({p.Position.X}, {p.Position.Y}) is outside the plot.")));
        }

        return errors;
    }

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

    private static SceneError? CheckPolygon(Guid id, IReadOnlyList<Point2> points) =>
        CheckPoints(id, points, minimum: 3)
        ?? (new Polygon(points).Area.SquareMeters < MinPolygonArea
            ? new SceneError("degenerate-polygon", id, $"Polygon {id} has almost no area.")
            : null);

    private static SceneError? CheckPoints(Guid id, IReadOnlyList<Point2> points, int minimum)
    {
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
