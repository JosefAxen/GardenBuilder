using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Features.DesignProposals;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Ai;

public sealed class LayoutEngineTests
{
    private static Point2[] Rect(double x0, double y0, double x1, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

    private static readonly PlotElement Plot = new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer, Rect(0, 0, 20, 20), Sloped: false);

    private static readonly BuildingElement House = new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer, Rect(5, 12, 15, 18), 5, RoofShape.Gable);

    private static readonly SurfaceElement Lawn = new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, Rect(0, 0, 20, 11), SurfaceMaterial.Grass);

    /// <summary>A kept birch in the south-east, 3 m wide.</summary>
    private static readonly PointElement Birch = new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, new Point2(17, 4), 0, Form: "tree-large", Width: 3, Height: 10);

    private static readonly SceneDocument Current = new(SceneDocument.CurrentFormatVersion, [Plot, House, Lawn, Birch]);

    private static CatalogArticle Article(string name, string typeModel, string family, string geometry, string? material = null, double? matureWidth = null, double? height = null) =>
        new(Ids.New(), name, geometry == "surface" ? "square-meter" : geometry == "line" ? "meter" : "piece", Money.InSek(100m),
            typeModel, family, geometry, material, family is "deciduous-tree" or "conifer" or "shrub" or "perennial" or "hedge" ? 5 : null,
            height, null, null, matureWidth, family is "furniture" or "surface-paving" ? [] : ["sun", "part-shade"]);

    private static readonly CatalogArticle Slabs = Article("Betongplatta", "surface-paving", "surface-paving", "surface", "paving");
    private static readonly CatalogArticle Gravel = Article("Grus", "surface-gravel", "surface-gravel", "surface", "gravel");
    private static readonly CatalogArticle Soil = Article("Rabattjord", "surface-bed", "surface-bed", "surface", "bed");
    private static readonly CatalogArticle Turf = Article("Rullgräs", "surface-grass", "surface-grass", "surface", "grass");
    private static readonly CatalogArticle Lavender = Article("Lavendel", "perennial-low", "perennial", "point", matureWidth: 0.5, height: 0.4);
    private static readonly CatalogArticle Peony = Article("Pion", "perennial-tall", "perennial", "point", matureWidth: 0.8, height: 0.8);
    private static readonly CatalogArticle Spiraea = Article("Spirea", "shrub-round", "shrub", "point", matureWidth: 1.2, height: 1);
    private static readonly CatalogArticle Privet = Article("Liguster", "hedge", "hedge", "line", height: 1.2);
    private static readonly CatalogArticle Maple = Article("Lönn", "tree-large", "deciduous-tree", "point", matureWidth: 6, height: 2.5);
    private static readonly CatalogArticle Apple = Article("Äppelträd", "tree-fruit", "deciduous-tree", "point", matureWidth: 4, height: 2);
    private static readonly CatalogArticle Bench = Article("Bänk", "furniture", "furniture", "point");

    private static readonly Dictionary<Guid, CatalogArticle> Articles =
        new[] { Slabs, Gravel, Soil, Turf, Lavender, Peony, Spiraea, Privet, Maple, Apple, Bench }.ToDictionary(a => a.Id);

    private static readonly IReadOnlyList<Zone> ZonesOfCurrent = Zones.Divide(Current);

    private static Block B(string type, string? zone = null, double? area = null, double? length = null, double? depth = null, string? side = null,
        string? from = null, string? to = null, int? count = null, params CatalogArticle[] articles) =>
        new(type, zone, area, length, depth, null, side, from, to, [.. articles.Select(a => a.Id)], count);

    private static Layout Build(params Block[] blocks) => LayoutEngine.Build(Current, ZonesOfCurrent, blocks, Articles);

    private static IEnumerable<SceneElement> New(Layout layout) => layout.Scene.Elements.Where(e => e.Status == ElementStatus.New);

    private static IEnumerable<PointElement> Plants(Layout layout, CatalogArticle article) =>
        New(layout).OfType<PointElement>().Where(p => p.ArticleId == article.Id);

    private static double Area(SurfaceElement surface) => new Polygon(surface.Points).Area.SquareMeters;

    [Fact]
    public void Patio_lies_against_the_south_facade_with_the_asked_area()
    {
        var layout = Build(B("patio", "house-south", area: 20, articles: Slabs));

        Assert.Empty(layout.Errors);
        var patio = Assert.Single(New(layout).OfType<SurfaceElement>());
        Assert.Equal(SurfaceMaterial.Paving, patio.Material);
        Assert.Equal(20, Area(patio), 0);
        Assert.Contains(patio.Points, p => Math.Abs(p.Y - 12) < 0.01);
        Assert.All(patio.Points, p => Assert.InRange(p.Y, 7.99, 12.01));
    }

    [Fact]
    public void Too_big_a_patio_is_an_error_with_the_largest_area_that_fits()
    {
        var layout = Build(B("patio", "house-south", area: 60, articles: Slabs));

        var error = Assert.Single(layout.Errors);
        Assert.Equal(0, error.Block);
        Assert.Equal("Uteplatsen på 60 m² får inte plats söder om huset, högst 40 m².", error.Message);
        Assert.Empty(New(layout));
    }

    [Fact]
    public void Path_runs_from_the_patio_to_a_zone_without_crossing_the_birch()
    {
        var layout = Build(B("patio", "house-south", area: 20, articles: Slabs), B("path", from: "patio", to: "boundary-east", articles: Gravel));

        Assert.Empty(layout.Errors);
        var path = New(layout).OfType<SurfaceElement>().Single(s => s.Material == SurfaceMaterial.Gravel);
        Assert.True(new Polygon(path.Points).DistanceTo(Birch.Position) >= 1.5, "the path cuts the birch's crown");
        Assert.Equal([0, 1], layout.Placed);
    }

    [Fact]
    public void Bed_plants_keep_their_mature_width_apart()
    {
        var layout = Build(B("bed", "boundary-west", length: 6, depth: 1.5, articles: [Lavender, Peony]));

        Assert.Empty(layout.Errors);
        Assert.Single(New(layout).OfType<SurfaceElement>(), s => s.Material == SurfaceMaterial.Bed && s.ArticleId == Soil.Id);
        var plants = New(layout).OfType<PointElement>().ToList();
        Assert.True(plants.Count >= 6);
        Assert.Contains(plants, p => p.ArticleId == Lavender.Id);
        Assert.Contains(plants, p => p.ArticleId == Peony.Id);
        foreach (var a in plants)
        {
            foreach (var b in plants.Where(b => b != a))
            {
                var limit = (Articles[a.ArticleId!.Value].MatureWidth!.Value + Articles[b.ArticleId!.Value].MatureWidth!.Value) / 2 * 0.7;
                Assert.True(a.Position.DistanceTo(b.Position) >= limit, $"{a.Position} and {b.Position} are too close");
            }
        }
    }

    [Fact]
    public void Shrubs_are_placed_in_their_zone_with_room()
    {
        var layout = Build(B("shrubs", "middle", count: 3, articles: Spiraea));

        Assert.Empty(layout.Errors);
        var shrubs = Plants(layout, Spiraea).ToList();
        Assert.Equal(3, shrubs.Count);
        Assert.All(shrubs, a => Assert.All(shrubs.Where(b => b != a), b => Assert.True(a.Position.DistanceTo(b.Position) >= 1.2 * 0.7)));
    }

    [Fact]
    public void Hedge_follows_the_boundary_half_a_meter_inside()
    {
        var layout = Build(B("hedge", side: "south", length: 8, articles: Privet));

        Assert.Empty(layout.Errors);
        var hedge = Assert.Single(New(layout).OfType<LineElement>());
        Assert.All(hedge.Points, p => Assert.Equal(0.5, p.Y, 2));
        Assert.Equal(8, hedge.Points[0].DistanceTo(hedge.Points[^1]), 1);
    }

    [Fact]
    public void Tree_keeps_half_its_crown_from_everything()
    {
        var layout = Build(B("tree", "middle", articles: Maple));

        Assert.Empty(layout.Errors);
        var tree = Assert.Single(Plants(layout, Maple));
        Assert.True(tree.Position.DistanceTo(Birch.Position) >= 3 + 1.5);
        Assert.True(new Polygon(House.Points).DistanceTo(tree.Position) >= 3);
        Assert.InRange(tree.Position.X, 3, 17);
        Assert.InRange(tree.Position.Y, 3, 17);
    }

    [Fact]
    public void Fruit_trees_stand_four_meters_apart()
    {
        var layout = Build(B("fruit-trees", "middle", count: 2, articles: Apple));

        Assert.Empty(layout.Errors);
        var trees = Plants(layout, Apple).ToList();
        Assert.Equal(2, trees.Count);
        Assert.True(trees[0].Position.DistanceTo(trees[1].Position) >= 4);
    }

    [Fact]
    public void Lawn_is_only_sown_where_there_is_no_grass_today()
    {
        var layout = Build(B("lawn", "house-west", articles: Turf));

        Assert.Empty(layout.Errors);
        var turf = Assert.Single(New(layout).OfType<SurfaceElement>());
        Assert.Equal(SurfaceMaterial.Grass, turf.Material);
        Assert.All(turf.Points, p => Assert.True(p.Y >= 10.99, $"{p} is on today's lawn"));
    }

    /// <summary>Clipper's area of overlap between two outlines, in m².</summary>
    private static double Overlap(IReadOnlyList<Point2> a, IReadOnlyList<Point2> b) =>
        Math.Abs(Clipper2Lib.Clipper.Area(Clipper2Lib.Clipper.Intersect(
            [Zones.Path(a)], [Zones.Path(b)], Clipper2Lib.FillRule.NonZero, 3)));

    [Fact]
    public void Lawn_on_the_rest_never_covers_the_house_the_birch_or_the_patio()
    {
        var bare = new SceneDocument(SceneDocument.CurrentFormatVersion, [Plot, House, Birch]);
        var layout = LayoutEngine.Build(bare, Zones.Divide(bare),
            [B("patio", "house-south", area: 20, articles: Slabs), B("lawn", "rest", articles: Turf)], Articles);

        Assert.Empty(layout.Errors);
        var patio = New(layout).OfType<SurfaceElement>().Single(s => s.Material == SurfaceMaterial.Paving);
        var turf = New(layout).OfType<SurfaceElement>().Where(s => s.Material == SurfaceMaterial.Grass).ToList();
        Assert.NotEmpty(turf);
        Assert.All(turf, t =>
        {
            Assert.True(Overlap(t.Points, House.Points) < 0.05, "turf covers the house");
            Assert.True(Overlap(t.Points, patio.Points) < 0.05, "turf covers the patio");
            Assert.False(new Polygon(t.Points).Contains(Birch.Position), "turf covers the birch");
        });
        // The plot minus the house, the patio and the birch's circle: about 400 − 60 − 20 − 28.
        Assert.InRange(turf.Sum(t => Area(t)), 285, 295);
    }

    [Fact]
    public void Gravel_around_the_birch_leaves_it_out()
    {
        var layout = Build(B("gravel", "boundary-east", area: 25, articles: Gravel));

        Assert.Empty(layout.Errors);
        var gravel = New(layout).OfType<SurfaceElement>().ToList();
        Assert.All(gravel, g => Assert.False(new Polygon(g.Points).Contains(Birch.Position), "gravel covers the birch"));
        Assert.InRange(gravel.Sum(g => Area(g)), 24.5, 40);
    }

    [Fact]
    public void Furniture_goes_on_the_patio_and_needs_one()
    {
        var with = Build(B("patio", "house-south", area: 20, articles: Slabs), B("furniture", articles: Bench));
        var without = Build(B("furniture", articles: Bench));

        var bench = Assert.Single(Plants(with, Bench));
        var patio = New(with).OfType<SurfaceElement>().Single();
        Assert.True(new Polygon(patio.Points).Contains(bench.Position));
        Assert.Equal("Möbler behöver en uteplats.", Assert.Single(without.Errors).Message);
    }

    [Fact]
    public void Unknown_zone_or_article_is_an_error_not_an_exception()
    {
        var layout = LayoutEngine.Build(Current, ZonesOfCurrent,
            [new Block("tree", "on-the-roof", null, null, null, null, null, null, null, [Maple.Id], null),
             new Block("tree", "middle", null, null, null, null, null, null, null, [Guid.NewGuid()], null),
             new Block("tree", "middle", null, null, null, null, null, null, null, [Slabs.Id], null),
             new Block("pond", "middle", null, null, null, null, null, null, null, [], null)],
            Articles);

        Assert.Equal([0, 1, 2, 3], layout.Errors.Select(e => e.Block));
        Assert.Equal("Zonen \"on-the-roof\" finns inte.", layout.Errors[0].Message);
        Assert.Equal("Artikeln finns inte i urvalet.", layout.Errors[1].Message);
        Assert.Empty(New(layout));
    }

    [Fact]
    public void Kept_things_are_copied_unchanged()
    {
        var layout = Build(B("patio", "house-south", area: 20, articles: Slabs), B("tree", "middle", articles: Maple));

        Assert.Contains(Plot, layout.Scene.Elements);
        Assert.Contains(House, layout.Scene.Elements);
        Assert.Contains(Birch, layout.Scene.Elements);
    }

    [Fact]
    public void Nothing_new_overlaps_the_birch()
    {
        var layout = Build(
            B("patio", "house-south", area: 20, articles: Slabs),
            B("shrubs", "boundary-east", count: 3, articles: Spiraea),
            B("fruit-trees", "boundary-south", count: 2, articles: Apple),
            B("bed", "middle", length: 6, depth: 1.5, articles: [Lavender, Peony]));

        foreach (var point in New(layout).OfType<PointElement>())
        {
            var width = Articles[point.ArticleId!.Value].MatureWidth ?? 1;
            Assert.True(point.Position.DistanceTo(Birch.Position) >= (Birch.Width!.Value + width) / 2, $"{point.Position} overlaps the birch");
        }

        foreach (var surface in New(layout).OfType<SurfaceElement>())
        {
            Assert.True(new Polygon(surface.Points).DistanceTo(Birch.Position) >= Birch.Width!.Value / 2);
        }
    }

    [Fact]
    public void A_patio_on_todays_lawn_cuts_the_lawn()
    {
        var layout = Build(B("bed", "middle", length: 6, depth: 1.5, articles: [Lavender]));

        var removed = Assert.Single(layout.Scene.Elements.OfType<SurfaceElement>(), s => s.Status == ElementStatus.ExistingRemove);
        Assert.Equal(SurfaceMaterial.Grass, removed.Material);
        Assert.Contains(layout.Scene.Elements, e => e.Id == Lawn.Id && e.Status == ElementStatus.ExistingKeep);
    }

    [Fact]
    public void A_garden_without_free_ground_places_nothing_and_does_not_throw()
    {
        var full = new SceneDocument(SceneDocument.CurrentFormatVersion,
            [Plot, House with { Points = Rect(0, 0, 20, 20) }]);

        var layout = LayoutEngine.Build(full, Zones.Divide(full),
            [B("patio", "house-south", area: 20, articles: Slabs), B("tree", "middle", articles: Maple), B("hedge", side: "south", length: 5, articles: Privet)],
            Articles);

        Assert.Equal(3, layout.Errors.Count);
        Assert.DoesNotContain(layout.Scene.Elements, e => e.Status == ElementStatus.New);
    }

    [Fact]
    public void Every_new_element_says_which_block_made_it()
    {
        var layout = Build(B("patio", "house-south", area: 20, articles: Slabs), B("shrubs", "middle", count: 2, articles: Spiraea));

        Assert.All(New(layout), e => Assert.True(layout.MadeBy.ContainsKey(e.Id)));
        Assert.Equal(2, layout.MadeBy.Values.Count(v => v == 1));
    }
}
