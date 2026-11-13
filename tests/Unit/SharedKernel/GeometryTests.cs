using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.SharedKernel;

public sealed class GeometryTests
{
    private static readonly Polygon Rectangle10By5 = new([new(0, 0), new(10, 0), new(10, 5), new(0, 5)]);

    [Fact]
    public void Polygon_area_of_rectangle()
    {
        Assert.Equal(50d, Rectangle10By5.Area.SquareMeters, precision: 9);
    }

    [Fact]
    public void Polygon_area_is_independent_of_winding_order()
    {
        var clockwise = new Polygon([new(0, 0), new(0, 5), new(10, 5), new(10, 0)]);

        Assert.Equal(50d, clockwise.Area.SquareMeters, precision: 9);
    }

    [Fact]
    public void Polygon_perimeter_of_rectangle()
    {
        Assert.Equal(30d, Rectangle10By5.Perimeter.Meters, precision: 9);
    }

    [Theory]
    [InlineData(5, 2.5, true)]
    [InlineData(11, 2.5, false)]
    [InlineData(-1, -1, false)]
    public void Polygon_contains_point(double x, double y, bool expected)
    {
        Assert.Equal(expected, Rectangle10By5.Contains(new Point2(x, y)));
    }

    [Fact]
    public void Polyline_length_sums_segments()
    {
        var hedge = new Polyline([new(0, 0), new(3, 4), new(3, 10)]);

        Assert.Equal(11d, hedge.Length.Meters, precision: 9);
    }

    [Fact]
    public void Polygon_requires_three_points()
    {
        Assert.Throws<ArgumentException>(() => new Polygon([new(0, 0), new(1, 1)]));
    }
}
