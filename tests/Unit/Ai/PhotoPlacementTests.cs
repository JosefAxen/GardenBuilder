using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Features.PhotoInterpretation;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Ai;

public sealed class PhotoPlacementTests
{
    /// <summary>A 20 × 20 m plot with its south-west corner at the origin, and a small house in the north-east.</summary>
    private static readonly Point2[] Plot = Rect(0, 0, 20, 20);

    private static readonly Point2[] House = Rect(14, 16, 18, 19);

    /// <summary>Standing on the southern boundary, in the middle, looking north into the garden.</summary>
    private static readonly CameraPose South = new(new Point2(10, 0), 0);

    private static Point2[] Rect(double x0, double y0, double x1, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

    /// <summary>Camera-frame points as Claude sends them: [x right, y ahead].</summary>
    private static double[][] Pts(params (double X, double Y)[] points) => [.. points.Select(p => new[] { p.X, p.Y })];

    private static PhotoThing Thing(string type, string shape, double confidence, double[][] points, string? label = null, double? width = null, double? height = null) =>
        new(type, label ?? type, shape, points, width, height, confidence);

    private static PhotoAnswer Answer(params PhotoThing[] things) => new("En trädgård.", null, things, null);

    private static PhotoProposal Place(params (PhotoAnswer Answer, CameraPose Camera)[] photos) => PhotoPlacement.Place(photos, Plot, House);

    /// <summary>Converts a scene point back to camera coordinates for the South camera, so tests can say where things are on the plot.</summary>
    private static (double X, double Y) AtScene(double x, double y) => (x - 10, y);

    private static double AreaOf(SceneElement element) => new Polygon(Assert.IsType<SurfaceElement>(element).Points).Area.SquareMeters;

    [Fact]
    public void Heading_90_turns_ahead_into_east()
    {
        var scene = PhotoPlacement.ToScene(0, 5, new CameraPose(new Point2(0, 0), 90));

        Assert.Equal(5, scene.X, 9);
        Assert.Equal(0, scene.Y, 9);
    }

    [Fact]
    public void Heading_0_keeps_ahead_as_north_and_right_as_east()
    {
        var scene = PhotoPlacement.ToScene(2, 5, South);

        Assert.Equal((12.0, 5.0), (Math.Round(scene.X, 9), Math.Round(scene.Y, 9)));
    }

    [Fact]
    public void Thing_far_outside_the_plot_is_dropped()
    {
        var proposal = Place((Answer(Thing("tree", "point", 0.9, Pts(AtScene(10, 30)), width: 4, height: 8)), South));

        Assert.Empty(proposal.Elements);
        Assert.Single(proposal.Dropped);
    }

    [Fact]
    public void Hedge_on_the_boundary_is_kept()
    {
        var hedge = Thing("hedge", "line", 0.8, Pts(AtScene(1, 20.3), AtScene(12, 20.3)), height: 1.5);

        var line = Assert.IsType<LineElement>(Assert.Single(Place((Answer(hedge), South)).Elements).Element);

        Assert.Equal("hedge", line.Form);
        Assert.Equal(1.5, line.Height);
    }

    [Fact]
    public void Lawn_is_clipped_to_the_plot()
    {
        var lawn = Thing("lawn", "area", 0.9, Pts(AtScene(-5, 0), AtScene(25, 0), AtScene(25, 30), AtScene(-5, 30)));

        var element = Assert.Single(Place((Answer(lawn), South)).Elements).Element;

        Assert.Equal(SurfaceMaterial.Grass, Assert.IsType<SurfaceElement>(element).Material);
        // The house is drawn on top of the lawn; a surface has one outline, so no hole is cut for it.
        Assert.Equal(400, AreaOf(element), 2);
    }

    [Fact]
    public void Same_lawn_from_two_photos_becomes_one_surface()
    {
        var left = Thing("lawn", "area", 0.8, Pts(AtScene(0, 0), AtScene(12, 0), AtScene(12, 10), AtScene(0, 10)));
        var right = Thing("lawn", "area", 0.7, Pts(AtScene(8, 0), AtScene(20, 0), AtScene(20, 10), AtScene(8, 10)));

        var proposal = Place((Answer(left), South), (Answer(right), South));

        var element = Assert.Single(proposal.Elements);
        Assert.Equal(200, AreaOf(element.Element), 2);
        Assert.Equal(0.8, element.Confidence);
    }

    [Fact]
    public void Two_trees_three_meters_apart_stay_two()
    {
        var proposal = Place((Answer(
            Thing("tree", "point", 0.8, Pts(AtScene(5, 5)), width: 2, height: 4),
            Thing("tree", "point", 0.8, Pts(AtScene(8, 5)), width: 2, height: 4)), South));

        Assert.Equal(2, proposal.Elements.Count);
    }

    [Fact]
    public void Two_trees_half_a_meter_apart_become_one_with_the_higher_confidence()
    {
        var proposal = Place(
            (Answer(Thing("tree", "point", 0.6, Pts(AtScene(5, 5)), width: 3, height: 5)), South),
            (Answer(Thing("tree", "point", 0.9, Pts(AtScene(5.5, 5)), width: 3, height: 5)), South));

        var tree = Assert.Single(proposal.Elements);
        Assert.Equal(0.9, tree.Confidence);
        Assert.Equal(5.25, Assert.IsType<PointElement>(tree.Element).Position.X, 9);
    }

    [Fact]
    public void Confidence_0_2_is_dropped()
    {
        Assert.Empty(Place((Answer(Thing("shrub", "point", 0.2, Pts(AtScene(5, 5)), width: 1)), South)).Elements);
    }

    [Fact]
    public void Shed_is_dropped_and_named()
    {
        var proposal = Place((Answer(Thing("shed", "area", 0.9, Pts(AtScene(2, 2), AtScene(4, 2), AtScene(4, 4)), label: "Förråd")), South));

        Assert.Empty(proposal.Elements);
        Assert.Contains("Förråd (stöds inte än)", proposal.Dropped);
    }

    [Fact]
    public void Tall_spruce_becomes_pine_and_a_birch_a_large_tree()
    {
        var proposal = Place((Answer(
            Thing("tree", "point", 0.9, Pts(AtScene(3, 5)), label: "Gran", width: 4, height: 15),
            Thing("tree", "point", 0.9, Pts(AtScene(12, 5)), label: "Björk", width: 5, height: 10),
            Thing("tree", "point", 0.9, Pts(AtScene(16, 8)), label: "Äppelträd", width: 3, height: 4)), South));

        var forms = proposal.Elements.Select(e => Assert.IsType<PointElement>(e.Element).Form).ToList();
        Assert.Equal(["pine", "tree-large", "tree-small"], forms);
    }

    [Fact]
    public void Yellow_wall_becomes_E3B23C_and_hip_roof_becomes_gable()
    {
        var answer = Answer() with { House = new PhotoHouse(true, "yellow", "hip", "red-brown", 0.8) };

        var house = Place((answer, South)).House;

        Assert.Equal(new HouseLook("#E3B23C", "#7A3B2E", RoofShape.Gable), house);
    }

    [Fact]
    public void House_from_the_most_confident_photo_wins()
    {
        var unsure = Answer() with { House = new PhotoHouse(true, "white", "flat", null, 0.4) };
        var sure = Answer() with { House = new PhotoHouse(true, "falu red", "gable", "black", 0.9) };

        var house = Place((unsure, South), (sure, South)).House;

        Assert.Equal(new HouseLook("#8E2A1E", "#2B2B2B", RoofShape.Gable), house);
    }

    [Fact]
    public void Invisible_house_gives_no_house_look()
    {
        var answer = Answer() with { House = new PhotoHouse(false, "yellow", "gable", "black", 0.9) };

        Assert.Null(Place((answer, South)).House);
    }

    [Fact]
    public void NaN_and_ten_kilometers_are_dropped_not_thrown()
    {
        var proposal = Place((Answer(
            Thing("tree", "point", 0.9, [[double.NaN, 5]], width: 2),
            Thing("tree", "point", 0.9, Pts((0, 10_000)), width: 2),
            Thing("lawn", "area", 0.9, Pts((0, 1), (double.PositiveInfinity, 1), (0, 3)))), South));

        Assert.Empty(proposal.Elements);
        Assert.Equal(3, proposal.Dropped.Count);
    }

    [Fact]
    public void Empty_answers_give_an_empty_proposal()
    {
        var proposal = Place((Answer(), South));

        Assert.Empty(proposal.Elements);
        Assert.Null(proposal.House);
        Assert.Empty(proposal.Dropped);
    }

    [Fact]
    public void Unknown_type_is_dropped()
    {
        var proposal = Place((Answer(Thing("trampoline", "point", 0.9, Pts(AtScene(5, 5)), label: "Studsmatta")), South));

        Assert.Empty(proposal.Elements);
        Assert.Contains("Studsmatta (stöds inte än)", proposal.Dropped);
    }

    [Fact]
    public void Area_with_two_points_is_dropped()
    {
        Assert.Empty(Place((Answer(Thing("patio", "area", 0.9, Pts(AtScene(2, 2), AtScene(4, 2)))), South)).Elements);
    }

    [Fact]
    public void Thing_inside_the_house_is_dropped()
    {
        Assert.Empty(Place((Answer(Thing("shrub", "point", 0.9, Pts(AtScene(16, 17.5)), width: 1)), South)).Elements);
    }

    [Fact]
    public void Every_proposed_element_is_existing_and_estimated()
    {
        var proposal = Place((Answer(
            Thing("patio", "area", 0.9, Pts(AtScene(2, 2), AtScene(6, 2), AtScene(6, 5), AtScene(2, 5))),
            Thing("fence", "line", 0.9, Pts(AtScene(0.2, 0.5), AtScene(0.2, 10)), height: 1.2),
            Thing("shrub", "point", 0.9, Pts(AtScene(8, 8)), width: 1.2, height: 1)), South));

        Assert.Equal(3, proposal.Elements.Count);
        Assert.All(proposal.Elements, e =>
        {
            Assert.Equal(ElementStatus.ExistingKeep, e.Element.Status);
            Assert.Equal(MeasurementSource.Estimated, e.Element.MeasurementSource);
        });
        Assert.Equal(SurfaceMaterial.Paving, Assert.IsType<SurfaceElement>(proposal.Elements[0].Element).Material);
        Assert.Equal("fence", Assert.IsType<LineElement>(proposal.Elements[1].Element).Form);
        Assert.Equal("shrub-round", Assert.IsType<PointElement>(proposal.Elements[2].Element).Form);
    }
}
