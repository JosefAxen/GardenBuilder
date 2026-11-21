using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Design.Domain;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Design;

public sealed class SceneValidatorTests
{
    private static readonly Guid Article = Guid.Parse("019a7d3c-a000-7000-8000-000000000001");

    private static readonly PlotElement Plot = new(
        Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, [new(-10, -10), new(10, -10), new(10, 10), new(-10, 10)], Sloped: false);

    private static readonly BuildingElement House = new(
        Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, [new(-4, 2), new(4, 2), new(4, 8), new(-4, 8)], Height: 5, Roof: RoofShape.Gable);

    private static readonly SurfaceElement Lawn = new(
        Ids.New(), ElementStatus.New, MeasurementSource.Estimated, Article, [new(-9, -9), new(9, -9), new(9, 1), new(-9, 1)], SurfaceMaterial.Grass);

    private static readonly LineElement Hedge = new(
        Ids.New(), ElementStatus.New, MeasurementSource.Estimated, Article, [new(-9.5, -9.5), new(9.5, -9.5)]);

    private static readonly PointElement Tree = new(
        Ids.New(), ElementStatus.New, MeasurementSource.Estimated, Article, new(2, -5), Rotation: 0);

    /// <summary>A small scene that breaks no rule: plot 20 × 20 m around the origin, a house, a lawn, a hedge and a tree.</summary>
    private static SceneDocument ValidScene(params SceneElement[] replacements)
    {
        SceneElement[] elements = [Plot, House, Lawn, Hedge, Tree];
        var byId = replacements.ToDictionary(e => e.Id);
        return new SceneDocument(SceneDocument.CurrentFormatVersion, [.. elements.Select(e => byId.GetValueOrDefault(e.Id, e))]);
    }

    private static SceneError SingleError(SceneDocument scene) => Assert.Single(SceneValidator.Validate(scene));

    [Fact]
    public void Valid_scene_has_no_errors()
    {
        Assert.Empty(SceneValidator.Validate(ValidScene()));
    }

    /// <summary>A regular polygon on a circle of radius 5 m: valid in every way except, above the limit, its size.</summary>
    private static Point2[] Circle(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new Point2(5 * Math.Cos(2 * Math.PI * i / count), 5 * Math.Sin(2 * Math.PI * i / count)))];

    [Fact]
    public void Polygon_at_the_point_limit_is_valid()
    {
        Assert.Empty(SceneValidator.Validate(ValidScene(Lawn with { Points = Circle(SceneValidator.MaxPoints) })));
    }

    [Fact]
    public void Polygon_over_the_point_limit_is_rejected_before_the_costly_checks()
    {
        var error = SingleError(ValidScene(Lawn with { Points = Circle(SceneValidator.MaxPoints + 1) }));

        Assert.Equal(("too-many-points", Lawn.Id), (error.Code, error.ElementId));
    }

    [Fact]
    public void Line_over_the_point_limit_is_rejected()
    {
        var points = Enumerable.Range(0, SceneValidator.MaxPoints + 1).Select(i => new Point2(-9 + (i * 0.01), -9.5)).ToArray();

        var error = SingleError(ValidScene(Hedge with { Points = points }));

        Assert.Equal(("too-many-points", Hedge.Id), (error.Code, error.ElementId));
    }

    [Fact]
    public void Unknown_format_version_is_an_error()
    {
        var error = SingleError(ValidScene() with { FormatVersion = 4 });

        Assert.Equal("format-version", error.Code);
        Assert.Null(error.ElementId);
    }

    [Fact]
    public void Duplicate_ids_are_an_error()
    {
        var scene = ValidScene() with { Elements = [Plot, House, Lawn, Hedge, Tree with { Id = House.Id }] };

        var error = SingleError(scene);

        Assert.Equal("duplicate-id", error.Code);
        Assert.Equal(House.Id, error.ElementId);
    }

    [Fact]
    public void Missing_plot_is_an_error()
    {
        var scene = ValidScene() with { Elements = [House, Lawn, Hedge, Tree] };

        Assert.Equal("plot-count", SingleError(scene).Code);
    }

    [Fact]
    public void Line_with_one_point_is_an_error()
    {
        var error = SingleError(ValidScene(Hedge with { Points = [new(0, -9.5)] }));

        Assert.Equal("too-few-points", error.Code);
        Assert.Equal(Hedge.Id, error.ElementId);
    }

    [Fact]
    public void Plot_with_points_on_a_line_is_an_error()
    {
        // The tree check needs a usable plot, so a degenerate plot must not also report the tree as outside.
        var error = SingleError(ValidScene(Plot with { Points = [new(0, 0), new(5, 0), new(10, 0)] }));

        Assert.Equal("degenerate-polygon", error.Code);
        Assert.Equal(Plot.Id, error.ElementId);
    }

    [Fact]
    public void Plot_thinner_than_a_hand_is_an_error()
    {
        // 1 000 m long and 2 cm wide: the area (20 m²) passes an absolute limit, but no real plot looks like this.
        var error = SingleError(ValidScene(Plot with { Points = [new(0, 0), new(1000, 0), new(1000, 0.02), new(0, 0.02)] }));

        Assert.Equal("degenerate-polygon", error.Code);
    }

    [Fact]
    public void Long_narrow_bed_is_allowed()
    {
        // 20 × 0.5 m, a normal bed along a fence, must stay valid.
        Assert.Empty(SceneValidator.Validate(ValidScene(Lawn with { Points = [new(-9, -9), new(9, -9), new(9, -8.5), new(-9, -8.5)] })));
    }

    [Fact]
    public void Self_intersecting_plot_is_an_error()
    {
        var error = SingleError(ValidScene(Plot with { Points = [new(-10, -10), new(10, 10), new(10, -10), new(-10, 10)] }));

        Assert.Equal("self-intersecting", error.Code);
        Assert.Equal(Plot.Id, error.ElementId);
    }

    [Fact]
    public void Far_away_coordinate_is_an_error()
    {
        var error = SingleError(ValidScene(Tree with { Position = new(1e9, 0) }));

        Assert.Equal("out-of-range", error.Code);
        Assert.Equal(Tree.Id, error.ElementId);
    }

    [Fact]
    public void NaN_rotation_is_an_error()
    {
        Assert.Equal("out-of-range", SingleError(ValidScene(Tree with { Rotation = double.NaN })).Code);
    }

    [Fact]
    public void Building_height_must_be_positive()
    {
        var error = SingleError(ValidScene(House with { Height = 0 }));

        Assert.Equal("building-height", error.Code);
        Assert.Equal(House.Id, error.ElementId);
    }

    [Fact]
    public void Point_outside_plot_is_an_error()
    {
        var error = SingleError(ValidScene(Tree with { Position = new(15, 0) }));

        Assert.Equal("outside-plot", error.Code);
        Assert.Equal(Tree.Id, error.ElementId);
    }

    [Fact]
    public void New_element_without_article_is_an_error()
    {
        var error = SingleError(ValidScene(Tree with { ArticleId = null }));

        Assert.Equal("missing-article", error.Code);
        Assert.Equal(Tree.Id, error.ElementId);
    }

    [Fact]
    public void Existing_point_with_form_is_valid()
    {
        Assert.Empty(SceneValidator.Validate(ValidScene(Tree with { Status = ElementStatus.ExistingKeep, ArticleId = null, Form = "tree-large", Width = 6, Height = 9 })));
    }

    [Fact]
    public void Existing_point_without_article_or_form_is_missing_form()
    {
        var error = SingleError(ValidScene(Tree with { Status = ElementStatus.ExistingKeep, ArticleId = null }));

        Assert.Equal("missing-form", error.Code);
        Assert.Equal(Tree.Id, error.ElementId);
    }

    [Fact]
    public void Existing_line_without_article_or_form_is_missing_form()
    {
        var error = SingleError(ValidScene(Hedge with { Status = ElementStatus.ExistingKeep, ArticleId = null }));

        Assert.Equal("missing-form", error.Code);
    }

    [Fact]
    public void Hedge_half_a_meter_outside_is_valid()
    {
        Assert.Empty(SceneValidator.Validate(ValidScene(Hedge with { Points = [new(-9.5, 10.4), new(9.5, 10.4)] })));
    }

    [Fact]
    public void Surface_two_meters_outside_is_outside_plot()
    {
        var error = SingleError(ValidScene(Lawn with { Points = [new(-9, -9), new(12, -9), new(12, 1), new(-9, 1)] }));

        Assert.Equal("outside-plot", error.Code);
        Assert.Equal(Lawn.Id, error.ElementId);
    }

    [Fact]
    public void Building_outside_is_outside_plot()
    {
        var error = SingleError(ValidScene(House with { Points = [new(-4, 2), new(4, 2), new(4, 13), new(-4, 13)] }));

        Assert.Equal("outside-plot", error.Code);
        Assert.Equal(House.Id, error.ElementId);
    }

    [Fact]
    public void Wall_color_red_is_bad_color()
    {
        var error = SingleError(ValidScene(House with { WallColor = "red" }));

        Assert.Equal("bad-color", error.Code);
    }

    [Fact]
    public void Hex_wall_and_roof_colors_are_valid()
    {
        Assert.Empty(SceneValidator.Validate(ValidScene(House with { WallColor = "#E3B23C", RoofColor = "#7a3b2e" })));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(41)]
    public void Point_size_outside_0_to_40_meters_is_bad_size(double width)
    {
        var error = SingleError(ValidScene(Tree with { Width = width }));

        Assert.Equal("bad-size", error.Code);
    }

    [Fact]
    public void Web_demo_scene_is_valid()
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot.Path, "src", "web", "src", "editor", "demoScene.json"));

        var scene = SceneJson.Deserialize(json);

        Assert.Empty(SceneValidator.Validate(scene));
    }

    private static SceneView View(string name = "Från altanen", double eyeX = 0, double eyeY = -12, double eyeHeight = 1.7, double fov = 50) =>
        new(Ids.New(), name, new Point3(eyeX, eyeY, eyeHeight), new Point3(0, 5, 1), fov);

    private static SceneDocument WithViews(params SceneView[] views) => ValidScene() with { Views = views };

    [Fact]
    public void A_view_nine_meters_outside_the_plot_is_valid()
    {
        Assert.Empty(SceneValidator.Validate(WithViews(View(eyeY: -19))));
    }

    [Fact]
    public void A_view_eleven_meters_outside_the_plot_is_refused()
    {
        Assert.Equal("view-outside-plot", SingleError(WithViews(View(eyeY: -21))).Code);
    }

    [Fact]
    public void Nine_views_are_too_many()
    {
        Assert.Equal("too-many-views", SingleError(WithViews([.. Enumerable.Range(0, 9).Select(_ => View())])).Code);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("En vy med ett namn som är alldeles för långt")]
    public void A_view_needs_a_short_name(string name)
    {
        Assert.Equal("bad-view-name", SingleError(WithViews(View(name))).Code);
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(30.5)]
    public void A_view_height_must_be_sensible(double height)
    {
        Assert.Equal("bad-view-height", SingleError(WithViews(View(eyeHeight: height))).Code);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(91)]
    public void A_field_of_view_must_be_sensible(double fov)
    {
        Assert.Equal("bad-field-of-view", SingleError(WithViews(View(fov: fov))).Code);
    }
}
