namespace GardenBuilder.Contracts.Scenes;

/// <summary>One broken rule in a scene. The code is stable; the client translates it to Swedish.</summary>
/// <param name="ElementId">The element the problem is about, or null when it concerns the whole scene.</param>
public sealed record SceneProblem(string Code, Guid? ElementId);

/// <summary>Checks that a scene makes sense, with the same rules the design module uses before saving.</summary>
public interface ISceneCheck
{
    /// <summary>The broken rules. Empty when the scene is valid.</summary>
    IReadOnlyList<SceneProblem> Check(SceneDocument scene);
}
