using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Features.DesignProposals;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Ai;

public sealed class ZonesTests
{
    private static Point2[] Rect(double x0, double y0, double x1, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

    private static PlotElement Plot(params Point2[] points) =>
        new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer, points.Length > 0 ? points : Rect(0, 0, 20, 20), Sloped: false);

    /// <summary>A 10 × 6 m house, 5 m high, in the north half of a 20 × 20 m plot.</summary>
    private static BuildingElement House(params Point2[] points) =>
        new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer, points.Length > 0 ? points : Rect(5, 12, 15, 18), 5, RoofShape.Gable);

    private static PointElement Tree(double x, double y, double width, ElementStatus status = ElementStatus.ExistingKeep) =>
        new(Ids.New(), status, MeasurementSource.Estimated, null, new Point2(x, y), 0, Form: "tree-large", Width: width, Height: 8);

    private static SceneDocument Scene(params SceneElement[] elements) => new(SceneDocument.CurrentFormatVersion, elements);

    private static Zone? ZoneAt(IReadOnlyList<Zone> zones, double x, double y) =>
        // An outline is rings under the even-odd rule: a point inside a hole is inside two rings.
        zones.SingleOrDefault(z => z.Outline.Count(ring => new Polygon(ring).Contains(new Point2(x, y))) % 2 == 1);

    [Fact]
    public void A_house_gives_four_house_zones_named_by_compass()
    {
        var zones = Zones.Divide(Scene(Plot(), House()));

        Assert.Equal("söder om huset", zones.Single(z => z.Key == "house-south").Name);
        Assert.Equal("norr om huset", zones.Single(z => z.Key == "house-north").Name);
        Assert.Equal("öster om huset", zones.Single(z => z.Key == "house-east").Name);
        Assert.Equal("väster om huset", zones.Single(z => z.Key == "house-west").Name);
        Assert.Equal("house-south", ZoneAt(zones, 10, 10)?.Key);
    }

    [Fact]
    public void South_of_the_house_is_sun_and_north_is_shade()
    {
        var zones = Zones.Divide(Scene(Plot(), House()));

        Assert.Equal(ZoneLight.Sun, zones.Single(z => z.Key == "house-south").Light);
        Assert.Equal(ZoneLight.Sun, zones.Single(z => z.Key == "house-west").Light);
        Assert.Equal(ZoneLight.Shade, zones.Single(z => z.Key == "house-north").Light);
        Assert.Equal(ZoneLight.PartShade, zones.Single(z => z.Key == "house-east").Light);
    }

    [Fact]
    public void Boundary_zones_are_three_meters_deep_and_named_by_side()
    {
        var zones = Zones.Divide(Scene(Plot(), House()));

        var south = ZoneAt(zones, 10, 1.5);
        Assert.Equal("boundary-south", south?.Key);
        Assert.Equal("längs södra gränsen", south?.Name);
        Assert.NotEqual("boundary-south", ZoneAt(zones, 10, 4)?.Key);
        Assert.Equal("längs västra gränsen", ZoneAt(zones, 1.5, 8)?.Name);
    }

    [Fact]
    public void The_rest_is_the_middle()
    {
        var zones = Zones.Divide(Scene(Plot(), House()));

        Assert.Equal("middle", ZoneAt(zones, 10, 5)?.Key);
        Assert.Equal("mitten", ZoneAt(zones, 10, 5)?.Name);
    }

    [Fact]
    public void Zones_cover_the_free_ground_once()
    {
        var zones = Zones.Divide(Scene(Plot(), House()));

        // 400 m² plot − 60 m² house.
        Assert.Equal(340, zones.Sum(z => z.Area), 1);
        Assert.Equal(zones.Count, zones.Select(z => z.Key).Distinct().Count());
    }

    [Fact]
    public void At_most_eight_zones()
    {
        Assert.True(Zones.Divide(Scene(Plot(), House())).Count <= Zones.MaxZones);
        Assert.Equal(8, Zones.MaxZones);
    }

    [Fact]
    public void Zones_under_four_square_meters_merge_into_a_neighbour()
    {
        // The strip north of this house is 0.3 m × 10 m = 3 m².
        var zones = Zones.Divide(Scene(Plot(), House(Rect(5, 12, 15, 19.7))));

        // Small pieces join a neighbour (here the corners join the strip north of the house), so none is under 4 m².
        Assert.All(zones, z => Assert.True(z.Area >= Zones.MinZoneArea, $"{z.Key} is {z.Area} m²"));
        Assert.Equal(400 - 77, zones.Sum(z => z.Area), 1);
    }

    [Fact]
    public void A_kept_tree_is_cut_out_with_its_width()
    {
        var zones = Zones.Divide(Scene(Plot(), House(), Tree(10, 5, 2)));

        Assert.Null(ZoneAt(zones, 10, 5));
        Assert.Null(ZoneAt(zones, 11.5, 5));
        Assert.NotNull(ZoneAt(zones, 12.5, 5));
    }

    [Fact]
    public void A_removed_hedge_is_free_ground()
    {
        var hedge = new LineElement(Ids.New(), ElementStatus.ExistingRemove, MeasurementSource.Estimated, null,
            [new Point2(2, 5), new Point2(18, 5)], Form: "hedge", Height: 1.5);

        Assert.NotNull(ZoneAt(Zones.Divide(Scene(Plot(), House(), hedge)), 10, 5));
    }

    [Fact]
    public void A_kept_hedge_is_not_free_ground()
    {
        var hedge = new LineElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null,
            [new Point2(2, 5), new Point2(18, 5)], Form: "hedge", Height: 1.5);

        Assert.Null(ZoneAt(Zones.Divide(Scene(Plot(), House(), hedge)), 10, 5));
    }

    [Fact]
    public void Lawn_is_free_ground()
    {
        var lawn = new SurfaceElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, Rect(0, 0, 20, 11), SurfaceMaterial.Grass);

        Assert.NotNull(ZoneAt(Zones.Divide(Scene(Plot(), House(), lawn)), 10, 5));
    }

    [Fact]
    public void A_new_lawn_the_customer_placed_is_not_free_ground()
    {
        var turf = new SurfaceElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, Ids.New(), Rect(8, 2, 12, 6), SurfaceMaterial.Grass);

        Assert.Null(ZoneAt(Zones.Divide(Scene(Plot(), House(), turf)), 10, 4));
    }

    [Fact]
    public void A_kept_patio_is_not_free_ground()
    {
        var patio = new SurfaceElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, Rect(8, 8, 12, 11), SurfaceMaterial.Paving);

        Assert.Null(ZoneAt(Zones.Divide(Scene(Plot(), House(), patio)), 10, 9.5));
    }

    [Fact]
    public void Part_shade_under_a_kept_tree()
    {
        // A wide crown next to the middle of the garden.
        var zones = Zones.Divide(Scene(Plot(), House(), Tree(10, 7, 6)));

        Assert.Equal(ZoneLight.PartShade, zones.Single(z => z.Key == "middle").Light);
    }

    [Fact]
    public void An_L_shaped_house_has_one_zone_per_compass_direction()
    {
        Point2[] l = [new(5, 12), new(15, 12), new(15, 15), new(10, 15), new(10, 18), new(5, 18)];

        var zones = Zones.Divide(Scene(Plot(), House(l)));

        Assert.Equal(["house-east", "house-north", "house-south", "house-west"], zones.Where(z => z.Kind == ZoneKind.House).Select(z => z.Key).Order());
    }

    [Fact]
    public void A_plot_full_of_house_gives_no_zones()
    {
        Assert.Empty(Zones.Divide(Scene(Plot(), House(Rect(0, 0, 20, 20)))));
    }

    [Theory]
    [InlineData(0, 0, 10, 0, "south")]
    [InlineData(10, 0, 10, 10, "east")]
    [InlineData(10, 10, 0, 10, "north")]
    [InlineData(0, 10, 0, 0, "west")]
    public void Facing_is_the_outward_side_of_a_counter_clockwise_edge(double x0, double y0, double x1, double y1, string facing)
    {
        Assert.Equal(facing, Zones.Facing(new Point2(x0, y0), new Point2(x1, y1)));
    }
}
