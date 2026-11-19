using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Pricing;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Features.DesignProposals;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Ai;

public sealed class ProposalCheckTests
{
    private static Point2[] Rect(double x0, double y0, double x1, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

    private static readonly PlotElement Plot = new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer, Rect(0, 0, 20, 20), Sloped: false);
    private static readonly BuildingElement House = new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer, Rect(5, 12, 15, 18), 5, RoofShape.Gable);
    private static readonly PointElement Birch = new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, new Point2(17, 4), 0, Form: "tree-large", Width: 3, Height: 10);
    private static readonly SceneDocument Current = new(SceneDocument.CurrentFormatVersion, [Plot, House, Birch]);
    private static readonly IReadOnlyList<Zone> Zones_ = Zones.Divide(Current);

    private static CatalogArticle Plant(string name, int zone, params string[] light) =>
        new(Ids.New(), name, "piece", Money.InSek(200m), "shrub-round", "shrub", "point", null, zone, 0.5, null, 1, 1.0, light);

    private static readonly CatalogArticle Slabs = new(Ids.New(), "Betongplatta", "square-meter", Money.InSek(450m), "surface-paving", "surface-paving", "surface", "paving", null, null, null, null, null, []);
    private static readonly CatalogArticle Gravel = new(Ids.New(), "Grus", "square-meter", Money.InSek(150m), "surface-gravel", "surface-gravel", "surface", "gravel", null, null, null, null, null, []);
    private static readonly CatalogArticle Bench = new(Ids.New(), "Bänk", "piece", Money.InSek(3000m), "furniture", "furniture", "point", null, null, null, null, null, null, []);
    private static readonly CatalogArticle Hardy = Plant("Spirea", 5, "sun", "part-shade", "shade");

    private static readonly ProposalWishes Wishes = new("natural", "50k-150k", "medium", 3, null);
    private static readonly PriceSpan Cheap = new(Money.InSek(60_000m), Money.InSek(80_000m));

    private static Dictionary<Guid, CatalogArticle> Selection(params CatalogArticle[] articles) => articles.ToDictionary(a => a.Id);

    private static Block Shrubs(string zone, CatalogArticle plant) => new("shrubs", zone, null, null, null, null, null, null, null, [plant.Id], 2);

    private static IReadOnlyList<BlockError> Check(Block[] blocks, Dictionary<Guid, CatalogArticle> selection, ProposalWishes? wishes = null,
        PriceSpan? price = null, SceneDocument? replaceScene = null, IReadOnlyList<SceneProblem>? problems = null)
    {
        var layout = LayoutEngine.Build(Current, Zones_, blocks, selection);
        if (replaceScene is not null)
        {
            layout = layout with { Scene = replaceScene };
        }

        return ProposalCheck.Errors(Current, layout, blocks, selection, Zones_, wishes ?? Wishes, problems ?? [], price ?? Cheap);
    }

    [Fact]
    public void A_good_proposal_has_no_errors()
    {
        Assert.Empty(Check([Shrubs("middle", Hardy)], Selection(Hardy)));
    }

    [Fact]
    public void Article_outside_the_selection_is_an_error()
    {
        var stranger = Plant("Okänd", 5, "sun");
        var layout = LayoutEngine.Build(Current, Zones_, [Shrubs("middle", Hardy)], Selection(Hardy));
        var swapped = layout.Scene.Elements.Select(e => e is PointElement { Status: ElementStatus.New } p ? p with { ArticleId = stranger.Id } : e).ToList();

        var errors = ProposalCheck.Errors(Current, layout with { Scene = layout.Scene with { Elements = swapped } }, [Shrubs("middle", Hardy)],
            Selection(Hardy), Zones_, Wishes, [], Cheap);

        Assert.Contains(errors, e => e.Block == 0 && e.Message == "Artikeln finns inte i urvalet.");
    }

    [Fact]
    public void Plant_too_tender_for_the_zone_is_an_error()
    {
        var tender = Plant("Kamelia", 2, "sun", "part-shade", "shade");

        var errors = Check([Shrubs("middle", tender)], Selection(tender), Wishes with { Zone = 4 });

        Assert.Contains(errors, e => e.Block == 0 && e.Message == "Kamelia klarar inte växtzon 4.");
    }

    [Fact]
    public void Sun_plant_in_a_shade_zone_is_an_error()
    {
        var sunny = Plant("Lavendel", 5, "sun");

        var errors = Check([Shrubs("house-north", sunny)], Selection(sunny));

        Assert.Contains(errors, e => e.Block == 0 && e.Message == "Lavendel trivs inte i skugga norr om huset.");
    }

    [Fact]
    public void A_moved_or_missing_kept_element_is_an_error()
    {
        var moved = new SceneDocument(SceneDocument.CurrentFormatVersion, [Plot, House, Birch with { Position = new Point2(10, 5) }]);
        var missing = new SceneDocument(SceneDocument.CurrentFormatVersion, [Plot, House]);

        Assert.Contains(Check([], Selection(), replaceScene: moved), e => e.Block == -1 && e.Message == "Något som ska stå kvar har ändrats.");
        Assert.Contains(Check([], Selection(), replaceScene: missing), e => e.Block == -1 && e.Message == "Något som ska stå kvar har ändrats.");
    }

    [Fact]
    public void Over_budget_is_an_error_with_both_amounts()
    {
        var errors = Check([Shrubs("middle", Hardy)], Selection(Hardy), price: new PriceSpan(Money.InSek(200_000m), Money.InSek(220_000m)));

        var error = Assert.Single(errors);
        Assert.Equal(-1, error.Block);
        Assert.Equal("Förslaget kostar cirka 210 000 kr men budgeten är högst 150 000 kr.", error.Message);
        Assert.True(ProposalCheck.IsBudget(error));
    }

    [Fact]
    public void No_ceiling_means_no_budget_error()
    {
        Assert.Empty(Check([Shrubs("middle", Hardy)], Selection(Hardy), Wishes with { Budget = "over-300k" },
            new PriceSpan(Money.InSek(900_000m), Money.InSek(1_000_000m))));
    }

    [Fact]
    public void Scene_problems_point_at_the_block_that_made_the_element()
    {
        var layout = LayoutEngine.Build(Current, Zones_, [Shrubs("middle", Hardy)], Selection(Hardy));
        var shrub = layout.Scene.Elements.First(e => e.Status == ElementStatus.New);

        var errors = ProposalCheck.Errors(Current, layout, [Shrubs("middle", Hardy)], Selection(Hardy), Zones_, Wishes,
            [new SceneProblem("outside-plot", shrub.Id), new SceneProblem("plot-count", null)], Cheap);

        Assert.Contains(errors, e => e.Block == 0 && e.Message == "Förslaget bryter mot regeln \"outside-plot\".");
        Assert.Contains(errors, e => e.Block == -1 && e.Message == "Förslaget bryter mot regeln \"plot-count\".");
    }

    [Fact]
    public void A_tender_plant_the_customer_placed_does_not_fail_the_proposal()
    {
        var tender = Plant("Kamelia", 2, "sun");
        var ownPlant = new PointElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, tender.Id, new Point2(3, 8), 0);
        var current = Current with { Elements = [.. Current.Elements, ownPlant] };
        Block[] blocks = [Shrubs("middle", Hardy)];
        var layout = LayoutEngine.Build(current, Zones.Divide(current), blocks, Selection(Hardy));

        var errors = ProposalCheck.Errors(current, layout, blocks, Selection(Hardy), Zones.Divide(current), Wishes with { Zone = 5 }, [], Cheap);

        Assert.Empty(errors);
    }

    [Fact]
    public void A_new_surface_over_something_that_stays_is_an_error()
    {
        var layout = LayoutEngine.Build(Current, Zones_, [Shrubs("middle", Hardy)], Selection(Hardy));
        var overHouse = new SurfaceElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, Slabs.Id, Rect(6, 13, 9, 16), SurfaceMaterial.Paving);
        var bad = layout with
        {
            Scene = layout.Scene with { Elements = [.. layout.Scene.Elements, overHouse] },
            MadeBy = new Dictionary<Guid, int>(layout.MadeBy) { [overHouse.Id] = 0 },
        };

        var errors = ProposalCheck.Errors(Current, bad, [Shrubs("middle", Hardy)], Selection(Hardy, Slabs), Zones_, Wishes, [], Cheap);

        Assert.Contains(errors, e => e.Block == 0 && e.Message == "Betongplatta täcker något som ska stå kvar.");
    }

    [Fact]
    public void Layout_errors_are_included()
    {
        var errors = Check([Shrubs("on-the-roof", Hardy)], Selection(Hardy));

        Assert.Contains(errors, e => e.Block == 0 && e.Message == "Zonen \"on-the-roof\" finns inte.");
    }

    [Fact]
    public void Dropping_a_patio_drops_its_path_and_furniture()
    {
        Block[] blocks =
        [
            new("patio", "house-south", 20, null, null, null, null, null, null, [Slabs.Id], null),
            new("path", null, null, null, null, null, null, "patio", "middle", [Gravel.Id], null),
            new("furniture", null, null, null, null, null, null, null, null, [Bench.Id], null),
            Shrubs("middle", Hardy),
            Shrubs("boundary-west", Hardy),
            Shrubs("boundary-north", Hardy),
        ];

        var left = ProposalCheck.WithoutFailed(blocks, [new BlockError(0, "Uteplatsen får inte plats.")]);

        Assert.NotNull(left);
        Assert.Equal(["shrubs", "shrubs", "shrubs"], left.Select(b => b.Type));
    }

    [Fact]
    public void Fewer_than_half_left_gives_null()
    {
        Block[] blocks = [Shrubs("middle", Hardy), Shrubs("boundary-west", Hardy), Shrubs("boundary-north", Hardy)];

        Assert.Null(ProposalCheck.WithoutFailed(blocks, [new BlockError(0, "x"), new BlockError(1, "y")]));
        Assert.Equal(2, ProposalCheck.WithoutFailed(blocks, [new BlockError(2, "z"), new BlockError(-1, "budget")])!.Count);
    }
}
