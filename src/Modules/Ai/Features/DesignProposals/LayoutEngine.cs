using System.Globalization;
using Clipper2Lib;
using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Features.DesignProposals;

/// <summary>
/// Turns the model's building blocks into exact scene elements (docs/superpowers/specs/2026-11-18-design-proposals-design.md,
/// section 3). The model says what and roughly where; this code decides exactly where, keeps everything that stays
/// untouched and never lets new things overlap. A block that does not fit places nothing and gets a Swedish error the
/// model can act on.
/// </summary>
internal static class LayoutEngine
{
    public const double MinPatioDepth = 2.5;
    public const double HedgeInset = 0.5;
    public const double FruitTreeSpacing = 4;
    public const double DefaultBedDepth = 1.5;
    public const double DefaultPathWidth = 1;
    public const int MaxBedPlants = 60;

    private const int Precision = 3;
    private const double GridStep = 0.5;
    private const double Slack = 0.05;
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    private static readonly Dictionary<string, string> TypeNames = new(StringComparer.Ordinal)
    {
        ["patio"] = "en uteplats", ["path"] = "en gång", ["gravel"] = "en grusyta", ["bed"] = "en rabatt", ["shrubs"] = "en buskgrupp",
        ["hedge"] = "en häck", ["tree"] = "ett träd", ["fruit-trees"] = "fruktträd", ["lawn"] = "en gräsmatta", ["furniture"] = "möblering",
    };

    private static readonly HashSet<string> PlantFamilies = new(StringComparer.Ordinal) { "deciduous-tree", "conifer", "shrub", "perennial" };

    /// <summary>What is being built, block by block.</summary>
    private sealed class Work(SceneDocument current, IReadOnlyList<Zone> zones, IReadOnlyDictionary<Guid, CatalogArticle> articles)
    {
        public SceneDocument Current { get; } = current;

        public Dictionary<string, Zone> Zones { get; } = zones.ToDictionary(z => z.Key, StringComparer.Ordinal);

        public IReadOnlyDictionary<Guid, CatalogArticle> Articles { get; } = articles;

        /// <summary>Ground still free: shrinks as new things are placed.</summary>
        public PathsD Free { get; set; } = DesignProposals.Zones.FreeGround(current);

        public PathsD? Patio { get; set; }

        public List<SceneElement> Added { get; } = [];

        public Dictionary<Guid, int> MadeBy { get; } = [];

        /// <summary>What new things cover, for cutting today's lawn.</summary>
        public List<Point2[]> Used { get; } = [];
    }

    public static Layout Build(
        SceneDocument current, IReadOnlyList<Zone> zones, IReadOnlyList<Block> blocks, IReadOnlyDictionary<Guid, CatalogArticle> articles)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(blocks);
        var work = new Work(current, zones, articles);
        var errors = new List<BlockError>();
        var placed = new List<int>();

        for (var i = 0; i < blocks.Count; i++)
        {
            var added = work.Added.Count;
            var error = Place(work, blocks[i]);
            if (error is not null)
            {
                work.Added.RemoveRange(added, work.Added.Count - added);
                errors.Add(new BlockError(i, error));
                continue;
            }

            placed.Add(i);
            foreach (var element in work.Added.Skip(added))
            {
                work.MadeBy[element.Id] = i;
            }
        }

        var kept = LawnSplit.Apply(current.Elements, work.Used);
        var scene = current with { Elements = [.. kept, .. work.Added] };
        return new Layout(scene, errors, placed, work.MadeBy);
    }

    /// <summary>Places one block, or returns why it cannot be placed. Nothing is added to the scene when it returns an error.</summary>
    private static string? Place(Work work, Block block)
    {
        if (!TypeNames.TryGetValue(block.Type ?? string.Empty, out var typeName))
        {
            return $"Byggklossen \"{block.Type}\" finns inte.";
        }

        var ids = block.Articles ?? [];
        if (ids.Count == 0)
        {
            return "Byggklossen saknar artikel.";
        }

        if (ids.Any(id => !work.Articles.ContainsKey(id)))
        {
            return "Artikeln finns inte i urvalet.";
        }

        var articles = ids.Select(id => work.Articles[id]).ToList();
        if (!articles.All(a => Fits(block.Type!, a)))
        {
            return $"Artikeln passar inte för {typeName}.";
        }

        // Every block but hedges, paths and furniture works in a zone.
        Zone? zone = null;
        if (block.Type is not ("hedge" or "path" or "furniture") && !(block.Type == "lawn" && block.Zone == "rest"))
        {
            if (block.Zone is null || !work.Zones.TryGetValue(block.Zone, out zone))
            {
                return $"Zonen \"{block.Zone}\" finns inte.";
            }
        }

        // A snapshot, so a block that fails halfway leaves the free ground as it was.
        var free = work.Free;
        var used = work.Used.Count;
        var error = block.Type switch
        {
            "patio" => Patio(work, block, zone!, articles[0]),
            "path" => Path(work, block, articles[0]),
            "gravel" => Gravel(work, block, zone!, articles[0]),
            "bed" => Bed(work, block, zone!, articles),
            "shrubs" => Shrubs(work, block, zone!, articles),
            "hedge" => Hedge(work, block, articles[0]),
            "tree" => Tree(work, zone!, articles[0]),
            "fruit-trees" => FruitTrees(work, block, zone!, articles[0]),
            "lawn" => Lawn(work, zone, articles[0]),
            _ => Furniture(work, articles),
        };
        if (error is not null)
        {
            work.Free = free;
            work.Used.RemoveRange(used, work.Used.Count - used);
        }

        return error;
    }

    private static bool Fits(string type, CatalogArticle article) => type switch
    {
        "patio" => article.Material is "paving" or "decking",
        "path" => article.Material is "gravel" or "paving",
        "gravel" => article.Material == "gravel",
        "lawn" => article.Material == "grass",
        "hedge" => article.Family == "hedge" && article.Geometry == "line",
        "tree" or "fruit-trees" => article.Geometry == "point" && article.Family is "deciduous-tree" or "conifer",
        "bed" or "shrubs" => article.Geometry == "point" && PlantFamilies.Contains(article.Family),
        _ => article.Geometry == "point" && !PlantFamilies.Contains(article.Family),
    };

    private static string? Patio(Work work, Block block, Zone zone, CatalogArticle article)
    {
        var house = work.Current.Elements.OfType<BuildingElement>().FirstOrDefault();
        var area = block.Area ?? 0;
        if (zone.Kind != ZoneKind.House || house is null || area <= 0)
        {
            return area <= 0 ? "Uteplatsen behöver en yta i m²." : $"En uteplats ska ligga vid huset, inte {zone.Name}.";
        }

        var ring = CounterClockwise(house.Points);
        var facade = Edges(ring).Where(e => DesignProposals.Zones.Facing(e.A, e.B) == zone.Facing).MaxBy(e => e.A.DistanceTo(e.B));
        if (facade.A == facade.B)
        {
            return $"Det finns ingen fasad {zone.Name}.";
        }

        var facadeLength = facade.A.DistanceTo(facade.B);
        var largest = facadeLength * DesignProposals.Zones.HouseDepth;
        if (area > largest)
        {
            return $"Uteplatsen på {Number(area)} m² får inte plats {zone.Name}, högst {Number(Math.Floor(largest))} m².";
        }

        // As wide as the facade allows, but never shallower than a table and chairs need.
        var depth = Math.Max(area / facadeLength, MinPatioDepth);
        var width = Math.Min(facadeLength, area / depth);
        var (ux, uy) = Unit(facade.A, facade.B);
        var (nx, ny) = (uy, -ux);
        var mid = new Point2((facade.A.X + facade.B.X) / 2, (facade.A.Y + facade.B.Y) / 2);
        var a = new Point2(mid.X - (ux * width / 2), mid.Y - (uy * width / 2));
        var b = new Point2(mid.X + (ux * width / 2), mid.Y + (uy * width / 2));
        Point2[] rect = [a, b, new(b.X + (nx * depth), b.Y + (ny * depth)), new(a.X + (nx * depth), a.Y + (ny * depth))];
        if (!InsideFree(work, [rect]))
        {
            return $"Uteplatsen får inte plats {zone.Name}: något som står kvar är i vägen.";
        }

        work.Patio = [Zones.Path(rect)];
        AddSurface(work, rect, article, Material(article));
        return null;
    }

    private static string? Path(Work work, Block block, CatalogArticle article)
    {
        var width = Math.Clamp(block.Width ?? DefaultPathWidth, 0.8, 1.2);
        Point2? start = block.From == "patio" ? (work.Patio is { } patio ? DesignProposals.Zones.Centroid(patio) : null) : ZonePoint(work, block.From);
        if (start is null)
        {
            return block.From == "patio" ? "Gången börjar vid en uteplats som inte finns." : $"Zonen \"{block.From}\" finns inte.";
        }

        if (ZonePoint(work, block.To) is not { } end)
        {
            return $"Zonen \"{block.To}\" finns inte.";
        }

        var allowed = work.Patio is { } p ? Clipper.Union(work.Free, p, FillRule.NonZero, Precision) : work.Free;
        var (ux, uy) = Unit(start.Value, end);
        var mid = new Point2((start.Value.X + end.X) / 2, (start.Value.Y + end.Y) / 2);
        foreach (var bend in new[] { 0.0, 2, -2, 4, -4, 6, -6 })
        {
            var via = new Point2(mid.X + (uy * bend), mid.Y - (ux * bend));
            PathD line = bend == 0 ? Zones.Path([start.Value, end]) : Zones.Path([start.Value, via, end]);
            var shape = Clipper.InflatePaths([line], width / 2, JoinType.Round, EndType.Butt, 2, Precision);
            if (DesignProposals.Zones.Area(Clipper.Difference(shape, allowed, FillRule.NonZero, Precision)) > Slack)
            {
                continue;
            }

            var surface = work.Patio is { } onPatio ? Clipper.Difference(shape, onPatio, FillRule.NonZero, Precision) : shape;
            foreach (var ring in surface.Where(r => Clipper.Area(r) > Slack))
            {
                AddSurface(work, Zones.ToPoints(ring), article, Material(article));
            }

            return null;
        }

        return $"Gången från \"{block.From}\" till \"{block.To}\" går inte att dra runt det som står kvar.";
    }

    private static string? Gravel(Work work, Block block, Zone zone, CatalogArticle article)
    {
        var area = block.Area ?? 0;
        var usable = Usable(work, zone);
        var available = DesignProposals.Zones.Area(usable);
        if (area <= 0 || available < area)
        {
            return $"Grusytan på {Number(area)} m² får inte plats {zone.Name}, högst {Number(Math.Floor(available))} m².";
        }

        var centre = Pole(usable, work.Free, 0) ?? DesignProposals.Zones.Centroid(usable);
        for (var side = Math.Sqrt(area); side < 60; side += 0.5)
        {
            Point2[] square = [new(centre.X - (side / 2), centre.Y - (side / 2)), new(centre.X + (side / 2), centre.Y - (side / 2)),
                new(centre.X + (side / 2), centre.Y + (side / 2)), new(centre.X - (side / 2), centre.Y + (side / 2))];
            var shape = Clipper.Intersect(usable, [Zones.Path(square)], FillRule.EvenOdd, Precision);
            if (DesignProposals.Zones.Area(shape) >= area)
            {
                foreach (var piece in HoleFree(shape))
                {
                    AddSurface(work, piece, article, SurfaceMaterial.Gravel);
                }

                return null;
            }
        }

        return $"Grusytan på {Number(area)} m² får inte plats {zone.Name}.";
    }

    private static string? Bed(Work work, Block block, Zone zone, List<CatalogArticle> plants)
    {
        var soil = work.Articles.Values.FirstOrDefault(a => a.Material == "bed");
        if (soil is null)
        {
            return "Det finns ingen rabattjord i lagret.";
        }

        var length = block.Length ?? 0;
        var depth = block.Depth ?? DefaultBedDepth;
        var usable = Usable(work, zone);
        var outer = usable.Where(r => Clipper.Area(r) > 0).MaxBy(r => Clipper.Area(r));
        if (length <= 0 || outer is null)
        {
            return length <= 0 ? "Rabatten behöver en längd." : $"Rabatten får inte plats {zone.Name}.";
        }

        // Along the longest edge of the zone, reaching into it.
        var ring = Zones.ToPoints(outer);
        var edge = Edges(ring).MaxBy(e => e.A.DistanceTo(e.B));
        var (ux, uy) = Unit(edge.A, edge.B);
        var (nx, ny) = (-uy, ux); // inward for a counter-clockwise outer ring
        var mid = new Point2((edge.A.X + edge.B.X) / 2, (edge.A.Y + edge.B.Y) / 2);
        var half = length / 2;
        Point2[] rect = [new(mid.X - (ux * half), mid.Y - (uy * half)), new(mid.X + (ux * half), mid.Y + (uy * half)),
            new(mid.X + (ux * half) + (nx * depth), mid.Y + (uy * half) + (ny * depth)), new(mid.X - (ux * half) + (nx * depth), mid.Y - (uy * half) + (ny * depth))];
        var region = Clipper.Intersect(usable, [Zones.Path(rect)], FillRule.EvenOdd, Precision);
        if (DesignProposals.Zones.Area(region) < 0.6 * length * depth)
        {
            return $"Rabatten på {Number(length)} m får inte plats {zone.Name}.";
        }

        // Plants in groups of three per kind, each at least its own half width from everything placed before it.
        var count = 0;
        foreach (var point in Grid(region).OrderBy(p => p.Y).ThenBy(p => p.X))
        {
            if (count >= MaxBedPlants)
            {
                break;
            }

            var plant = plants[(count / 3) % plants.Count];
            if (TryPlant(work, point, plant, plant.MatureWidth ?? DesignProposals.Zones.DefaultPlantWidth))
            {
                count++;
            }
        }

        if (count < plants.Count)
        {
            return $"Rabatten {zone.Name} blev för liten för växterna.";
        }

        var pieces = HoleFree(region);
        work.Added.InsertRange(work.Added.Count - count, pieces.Select(p => (SceneElement)NewSurface(p, soil, SurfaceMaterial.Bed)));
        work.Used.AddRange(pieces);
        work.Free = Clipper.Difference(work.Free, region, FillRule.NonZero, Precision);
        return null;
    }

    private static string? Shrubs(Work work, Block block, Zone zone, List<CatalogArticle> kinds)
    {
        var wanted = Math.Clamp(block.Count ?? 3, 1, 15);
        var usable = Usable(work, zone);
        var centre = DesignProposals.Zones.Centroid(usable);
        var count = 0;
        foreach (var point in Grid(usable).OrderBy(p => p.DistanceTo(centre)))
        {
            var kind = kinds[count % kinds.Count];
            if (count < wanted && TryPlant(work, point, kind, kind.MatureWidth ?? DesignProposals.Zones.DefaultPlantWidth))
            {
                count++;
            }
        }

        return count < wanted ? $"Buskgruppen får inte plats {zone.Name}, högst {count} buskar." : null;
    }

    private static string? Hedge(Work work, Block block, CatalogArticle article)
    {
        var plot = work.Current.Elements.OfType<PlotElement>().FirstOrDefault();
        var side = block.Side ?? string.Empty;
        var name = side switch
        {
            "north" => "längs norra gränsen", "south" => "längs södra gränsen", "east" => "längs östra gränsen", "west" => "längs västra gränsen",
            _ => null,
        };
        if (plot is null || name is null)
        {
            return $"Gränsen \"{block.Side}\" finns inte.";
        }

        var edge = Edges(CounterClockwise(plot.Points)).Where(e => DesignProposals.Zones.Facing(e.A, e.B) == side).MaxBy(e => e.A.DistanceTo(e.B));
        var edgeLength = edge.A.DistanceTo(edge.B);
        var length = Math.Min(block.Length ?? edgeLength, edgeLength - (2 * HedgeInset));
        if (length <= 0.5)
        {
            return $"Häcken får inte plats {name}.";
        }

        var (ux, uy) = Unit(edge.A, edge.B);
        var (nx, ny) = (-uy, ux);
        var mid = new Point2(((edge.A.X + edge.B.X) / 2) + (nx * HedgeInset), ((edge.A.Y + edge.B.Y) / 2) + (ny * HedgeInset));
        Point2[] line = [new(mid.X - (ux * length / 2), mid.Y - (uy * length / 2)), new(mid.X + (ux * length / 2), mid.Y + (uy * length / 2))];
        var footprint = Clipper.InflatePaths([Zones.Path(line)], DesignProposals.Zones.SurfaceMargin, JoinType.Round, EndType.Butt, 2, Precision);
        if (DesignProposals.Zones.Area(Clipper.Difference(footprint, work.Free, FillRule.NonZero, Precision)) > Slack)
        {
            return $"Häcken får inte plats {name}.";
        }

        work.Added.Add(new LineElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, article.Id, line));
        Take(work, footprint);
        return null;
    }

    private static string? Tree(Work work, Zone zone, CatalogArticle article)
    {
        var width = article.MatureWidth ?? 4;
        var spot = Pole(Usable(work, zone), work.Free, width / 2);
        return spot is { } point && TryPlant(work, point, article, width) ? null : $"Trädet får inte plats {zone.Name}.";
    }

    private static string? FruitTrees(Work work, Block block, Zone zone, CatalogArticle article)
    {
        var wanted = Math.Clamp(block.Count ?? 1, 1, 10);
        var width = article.MatureWidth ?? 4;
        var spacing = Math.Max(FruitTreeSpacing, width);
        var usable = Usable(work, zone);
        var centre = DesignProposals.Zones.Centroid(usable);
        var placed = new List<Point2>();
        foreach (var point in Grid(usable).OrderBy(p => p.DistanceTo(centre)))
        {
            if (placed.Count < wanted && placed.All(q => q.DistanceTo(point) >= spacing) && TryPlant(work, point, article, width))
            {
                placed.Add(point);
            }
        }

        return placed.Count < wanted ? $"Fruktträden får inte plats {zone.Name}, högst {placed.Count} stycken." : null;
    }

    private static string? Lawn(Work work, Zone? zone, CatalogArticle article)
    {
        var region = zone is null ? work.Free : Usable(work, zone);
        var grass = work.Current.Elements.OfType<SurfaceElement>()
            .Where(s => s.Material == SurfaceMaterial.Grass && s.Status != ElementStatus.ExistingRemove)
            .Select(s => Zones.Path(s.Points));
        var bare = Clipper.Difference(region, new PathsD(grass), FillRule.NonZero, Precision);
        var pieces = HoleFree(bare).Where(p => Clipper.Area(Zones.Path(p)) >= 1).ToList();
        if (pieces.Count == 0)
        {
            return $"Det finns ingen ny yta för gräs {zone?.Name ?? "på tomten"}.";
        }

        foreach (var piece in pieces)
        {
            AddSurface(work, piece, article, SurfaceMaterial.Grass);
        }

        return null;
    }

    private static string? Furniture(Work work, List<CatalogArticle> pieces)
    {
        if (work.Patio is not { } patio)
        {
            return "Möbler behöver en uteplats.";
        }

        var centre = DesignProposals.Zones.Centroid(patio);
        for (var i = 0; i < Math.Min(pieces.Count, 2); i++)
        {
            var at = new Point2(centre.X + (i * 1.2) - ((Math.Min(pieces.Count, 2) - 1) * 0.6), centre.Y);
            work.Added.Add(new PointElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, pieces[i].Id, at, 0));
        }

        return null;
    }

    /// <summary>
    /// A shape as outlines without holes. A scene surface has one outline, so a hole (the house, a kept tree, a patio
    /// already placed) would otherwise be covered. Each hole is opened by cutting a 2 mm slit through it, top to bottom.
    /// </summary>
    internal static List<Point2[]> HoleFree(PathsD shape)
    {
        var current = Clipper.Union(shape, FillRule.EvenOdd);
        for (var guard = 0; guard < 100; guard++)
        {
            var hole = current.FirstOrDefault(r => Clipper.Area(r) < 0);
            if (hole is null)
            {
                break;
            }

            var bounds = Clipper.GetBounds(current);
            var x = DesignProposals.Zones.Centroid([hole]).X;
            PathD slit = Zones.Path([new(x - 0.001, bounds.top - 1), new(x + 0.001, bounds.top - 1), new(x + 0.001, bounds.bottom + 1), new(x - 0.001, bounds.bottom + 1)]);
            current = Clipper.Difference(current, [slit], FillRule.NonZero, Precision);
        }

        return [.. current.Where(r => Clipper.Area(r) >= LawnSplit.MinPiece).Select(Zones.ToPoints)];
    }

    /// <summary>Places a plant when it is at least half its width from everything; it then keeps that circle.</summary>
    private static bool TryPlant(Work work, Point2 point, CatalogArticle article, double width)
    {
        if (Clearance(work.Free, point) < (width / 2) - 1e-6)
        {
            return false;
        }

        work.Added.Add(new PointElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, article.Id, point, 0));
        var circle = Circle(point, width / 2);
        work.Used.Add(Zones.ToPoints(circle));
        work.Free = Clipper.Difference(work.Free, [circle], FillRule.NonZero, Precision);
        return true;
    }

    private static void AddSurface(Work work, Point2[] points, CatalogArticle article, SurfaceMaterial material)
    {
        work.Added.Add(NewSurface(points, article, material));
        Take(work, [Zones.Path(points)]);
    }

    private static SurfaceElement NewSurface(Point2[] points, CatalogArticle article, SurfaceMaterial material) =>
        new(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, article.Id, points, material);

    private static void Take(Work work, PathsD shape)
    {
        work.Used.AddRange(shape.Where(r => Clipper.Area(r) > 0).Select(Zones.ToPoints));
        work.Free = Clipper.Difference(work.Free, shape, FillRule.NonZero, Precision);
    }

    private static SurfaceMaterial Material(CatalogArticle article) => article.Material switch
    {
        "decking" => SurfaceMaterial.Decking,
        "gravel" => SurfaceMaterial.Gravel,
        "grass" => SurfaceMaterial.Grass,
        "bed" => SurfaceMaterial.Bed,
        _ => SurfaceMaterial.Paving,
    };

    private static bool InsideFree(Work work, IEnumerable<Point2[]> shape) =>
        DesignProposals.Zones.Area(Clipper.Difference(new PathsD(shape.Select(Zones.Path)), work.Free, FillRule.NonZero, Precision)) <= Slack;

    /// <summary>The part of a zone that is still free.</summary>
    private static PathsD Usable(Work work, Zone zone) =>
        Clipper.Intersect(new PathsD(zone.Outline.Select(Zones.Path)), work.Free, FillRule.EvenOdd, Precision);

    private static Point2? ZonePoint(Work work, string? key) =>
        key is not null && work.Zones.TryGetValue(key, out var zone) ? Pole(Usable(work, zone), work.Free, 0) : null;

    /// <summary>The grid point in the region that lies farthest from everything taken, if that is at least <paramref name="clearance"/>.</summary>
    private static Point2? Pole(PathsD region, PathsD free, double clearance)
    {
        var centre = DesignProposals.Zones.Centroid(region);
        var best = Grid(region)
            .Select(p => (Point: p, Clearance: Clearance(free, p)))
            .OrderByDescending(c => Math.Round(c.Clearance, 1))
            .ThenBy(c => c.Point.DistanceTo(centre))
            .FirstOrDefault();
        return best.Clearance >= clearance && best.Clearance > 0 ? best.Point : null;
    }

    /// <summary>Points every half meter inside the region (even-odd, so holes are left out).</summary>
    private static IEnumerable<Point2> Grid(PathsD region)
    {
        if (region.Count == 0)
        {
            yield break;
        }

        var bounds = Clipper.GetBounds(region);
        for (var y = Math.Ceiling(bounds.top / GridStep) * GridStep; y <= bounds.bottom; y += GridStep)
        {
            for (var x = Math.Ceiling(bounds.left / GridStep) * GridStep; x <= bounds.right; x += GridStep)
            {
                var point = new Point2(x, y);
                if (Inside(region, point))
                {
                    yield return point;
                }
            }
        }
    }

    /// <summary>How far the point is from the edge of the free ground; zero when it is not on free ground.</summary>
    private static double Clearance(PathsD free, Point2 point) =>
        Inside(free, point) ? free.Min(ring => DistanceToRing(ring, point)) : 0;

    private static bool Inside(PathsD shape, Point2 point) =>
        shape.Count(ring => Clipper.PointInPolygon(new PointD(point.X, point.Y), ring, Precision) == PointInPolygonResult.IsInside) % 2 == 1;

    private static double DistanceToRing(PathD ring, Point2 point)
    {
        var nearest = double.MaxValue;
        for (var i = 0; i < ring.Count; i++)
        {
            var a = new Point2(ring[i].x, ring[i].y);
            var b = new Point2(ring[(i + 1) % ring.Count].x, ring[(i + 1) % ring.Count].y);
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var lengthSquared = (dx * dx) + (dy * dy);
            var t = lengthSquared == 0 ? 0 : Math.Clamp((((point.X - a.X) * dx) + ((point.Y - a.Y) * dy)) / lengthSquared, 0, 1);
            nearest = Math.Min(nearest, point.DistanceTo(new Point2(a.X + (t * dx), a.Y + (t * dy))));
        }

        return nearest;
    }

    private static PathD Circle(Point2 centre, double radius) =>
        new(Enumerable.Range(0, 16).Select(i => new PointD(centre.X + (radius * Math.Cos(i * Math.PI / 8)), centre.Y + (radius * Math.Sin(i * Math.PI / 8)))));

    private static Point2[] CounterClockwise(IReadOnlyList<Point2> points)
    {
        var ring = points.ToArray();
        return Clipper.Area(Zones.Path(ring)) < 0 ? [.. ring.Reverse()] : ring;
    }

    private static IEnumerable<(Point2 A, Point2 B)> Edges(Point2[] ring) =>
        ring.Select((p, i) => (p, ring[(i + 1) % ring.Length]));

    private static (double X, double Y) Unit(Point2 a, Point2 b)
    {
        var length = a.DistanceTo(b);
        return length == 0 ? (0, 0) : ((b.X - a.X) / length, (b.Y - a.Y) / length);
    }

    private static string Number(double value) => value.ToString("0.#", Swedish);
}
