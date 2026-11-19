using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Features.DesignProposals;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Ai;

public sealed class LawnSplitTests
{
    private static Point2[] Rect(double x0, double y0, double x1, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

    private static readonly SurfaceElement Lawn =
        new(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, Rect(0, 0, 20, 10), SurfaceMaterial.Grass);

    private static double Area(SceneElement element) => new Polygon(Assert.IsType<SurfaceElement>(element).Points).Area.SquareMeters;

    [Fact]
    public void Cut_part_becomes_existing_remove_and_kept_part_keeps_its_id()
    {
        var result = LawnSplit.Apply([Lawn], [Rect(0, 0, 5, 10)]);

        var kept = Assert.Single(result, e => e.Status == ElementStatus.ExistingKeep);
        var removed = Assert.Single(result, e => e.Status == ElementStatus.ExistingRemove);
        Assert.Equal(Lawn.Id, kept.Id);
        Assert.Equal(150, Area(kept), 2);
        Assert.Equal(50, Area(removed), 2);
        Assert.NotEqual(Lawn.Id, removed.Id);
        Assert.Equal(SurfaceMaterial.Grass, Assert.IsType<SurfaceElement>(removed).Material);
    }

    [Fact]
    public void Pieces_under_half_a_square_meter_are_dropped()
    {
        var result = LawnSplit.Apply([Lawn], [Rect(0, 0, 0.04, 10)]);

        Assert.Same(Lawn, Assert.Single(result));
    }

    [Fact]
    public void A_lawn_cut_in_two_gives_two_kept_lawns()
    {
        var result = LawnSplit.Apply([Lawn], [Rect(9, 0, 11, 10)]);

        var kept = result.Where(e => e.Status == ElementStatus.ExistingKeep).ToList();
        Assert.Equal(2, kept.Count);
        Assert.Contains(kept, e => e.Id == Lawn.Id);
        Assert.Equal(20, Area(Assert.Single(result, e => e.Status == ElementStatus.ExistingRemove)), 2);
    }

    [Fact]
    public void A_lawn_not_touched_is_unchanged()
    {
        var patio = new SurfaceElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, null, Rect(0, 12, 4, 15), SurfaceMaterial.Paving);

        var result = LawnSplit.Apply([Lawn, patio], [Rect(0, 12, 4, 15)]);

        Assert.Equal([Lawn, patio], result);
    }

    [Fact]
    public void Removed_lawns_and_other_surfaces_are_left_alone()
    {
        var removed = Lawn with { Status = ElementStatus.ExistingRemove };

        Assert.Same(removed, Assert.Single(LawnSplit.Apply([removed], [Rect(0, 0, 5, 10)])));
    }
}
