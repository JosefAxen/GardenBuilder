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

    /// <summary>
    /// True when two edges that are not neighbours cross or touch, like a bowtie. Area, perimeter and
    /// <see cref="Contains"/> give meaningless answers for such a polygon.
    /// </summary>
    public bool IsSelfIntersecting
    {
        get
        {
            var n = Points.Count;
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    // Edges i and j share a corner when they are next to each other (including last and first).
                    var neighbours = j == i + 1 || (i == 0 && j == n - 1);
                    if (!neighbours && SegmentsIntersect(Points[i], Points[(i + 1) % n], Points[j], Points[(j + 1) % n]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private static bool SegmentsIntersect(Point2 a, Point2 b, Point2 c, Point2 d)
    {
        var d1 = Cross(c, d, a);
        var d2 = Cross(c, d, b);
        var d3 = Cross(a, b, c);
        var d4 = Cross(a, b, d);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
        {
            return true;
        }

        // Touching or overlapping: a point lies on the other segment.
        return (d1 == 0 && OnSegment(c, d, a)) || (d2 == 0 && OnSegment(c, d, b))
            || (d3 == 0 && OnSegment(a, b, c)) || (d4 == 0 && OnSegment(a, b, d));
    }

    /// <summary>Which side of the line from <paramref name="p"/> to <paramref name="q"/> the point <paramref name="r"/> is on.</summary>
    private static double Cross(Point2 p, Point2 q, Point2 r) => ((q.X - p.X) * (r.Y - p.Y)) - ((q.Y - p.Y) * (r.X - p.X));

    private static bool OnSegment(Point2 p, Point2 q, Point2 r) =>
        Math.Min(p.X, q.X) <= r.X && r.X <= Math.Max(p.X, q.X) && Math.Min(p.Y, q.Y) <= r.Y && r.Y <= Math.Max(p.Y, q.Y);

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

    /// <summary>Zero inside the polygon, otherwise the distance to the nearest edge.</summary>
    public double DistanceTo(Point2 point)
    {
        if (Contains(point))
        {
            return 0;
        }

        var nearest = double.PositiveInfinity;
        for (int i = 0, j = Points.Count - 1; i < Points.Count; j = i++)
        {
            nearest = Math.Min(nearest, DistanceToSegment(point, Points[j], Points[i]));
        }

        return nearest;
    }

    private static double DistanceToSegment(Point2 p, Point2 a, Point2 b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        var t = lengthSquared == 0 ? 0 : Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared, 0, 1);
        return p.DistanceTo(new Point2(a.X + (t * dx), a.Y + (t * dy)));
    }
}
