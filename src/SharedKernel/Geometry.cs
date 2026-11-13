namespace GardenBuilder.SharedKernel;

/// <summary>A point in the garden's local coordinate system, in meters. +Y points north.</summary>
public readonly record struct Point2(double X, double Y)
{
    public double DistanceTo(Point2 other) => Math.Sqrt(Math.Pow(other.X - X, 2) + Math.Pow(other.Y - Y, 2));
}

/// <summary>A length in meters.</summary>
public readonly record struct Length(double Meters);

/// <summary>An area in square meters.</summary>
public readonly record struct Area(double SquareMeters);

/// <summary>An open line through two or more points, e.g. a hedge or a fence.</summary>
public sealed class Polyline
{
    public Polyline(IReadOnlyList<Point2> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            throw new ArgumentException("A polyline needs at least two points.", nameof(points));
        }

        Points = points;
    }

    public IReadOnlyList<Point2> Points { get; }

    public Length Length
    {
        get
        {
            var total = 0d;
            for (var i = 1; i < Points.Count; i++)
            {
                total += Points[i - 1].DistanceTo(Points[i]);
            }

            return new Length(total);
        }
    }
}

/// <summary>A simple closed polygon, e.g. the plot, a lawn or a building footprint.</summary>
public sealed class Polygon
{
    public Polygon(IReadOnlyList<Point2> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3)
        {
            throw new ArgumentException("A polygon needs at least three points.", nameof(points));
        }

        Points = points;
    }

    public IReadOnlyList<Point2> Points { get; }

    /// <summary>Area by the shoelace formula. Independent of winding order.</summary>
    public Area Area
    {
        get
        {
            var sum = 0d;
            for (var i = 0; i < Points.Count; i++)
            {
                var a = Points[i];
                var b = Points[(i + 1) % Points.Count];
                sum += (a.X * b.Y) - (b.X * a.Y);
            }

            return new Area(Math.Abs(sum) / 2d);
        }
    }

    public Length Perimeter
    {
        get
        {
            var total = 0d;
            for (var i = 0; i < Points.Count; i++)
            {
                total += Points[i].DistanceTo(Points[(i + 1) % Points.Count]);
            }

            return new Length(total);
        }
    }

    /// <summary>Ray casting point-in-polygon test. Points exactly on an edge may go either way.</summary>
    public bool Contains(Point2 point)
    {
        var inside = false;
        for (int i = 0, j = Points.Count - 1; i < Points.Count; j = i++)
        {
            var a = Points[i];
            var b = Points[j];
            var crosses = (a.Y > point.Y) != (b.Y > point.Y)
                && point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X;
            if (crosses)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
