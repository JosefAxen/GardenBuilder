using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Design.Domain;

namespace GardenBuilder.Modules.Design.Features.CheckScene;

/// <summary>Lets other modules, such as pricing, check a scene without reaching into the design module.</summary>
internal sealed class SceneCheck : ISceneCheck
{
    public IReadOnlyList<SceneProblem> Check(SceneDocument scene) =>
        [.. SceneValidator.Validate(scene).Select(e => new SceneProblem(e.Code, e.ElementId))];
}
