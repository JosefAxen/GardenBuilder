using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Design.Features.CheckScene;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Design;

public sealed class SceneCheckTests
{
    private static readonly PlotElement Plot = new(
        Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, [new(-10, -10), new(10, -10), new(10, 10), new(-10, 10)], Sloped: false);

    private static readonly BuildingElement House = new(
        Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Estimated, [new(-4, 2), new(4, 2), new(4, 8), new(-4, 8)], Height: 5, Roof: RoofShape.Gable);

    private readonly SceneCheck _check = new();

    [Fact]
    public void Valid_scene_has_no_problems()
    {
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion, [Plot, House]);

        Assert.Empty(_check.Check(scene));
    }

    [Fact]
    public void Scene_without_plot_reports_plot_count()
    {
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion, [House]);

        Assert.Contains(new SceneProblem("plot-count", null), _check.Check(scene));
    }
}
