using GardenBuilder.Contracts.Scenes;

namespace GardenBuilder.Contracts.Designs;

/// <summary>A design's working copy with who it belongs to.</summary>
/// <param name="Revision">The working copy's revision; it goes up with every save.</param>
public sealed record DesignScene(Guid DesignId, Guid GardenId, Guid CustomerId, SceneDocument Scene, int Revision);

/// <summary>Reads designs' working copies for other modules. Access checks are the caller's job: compare the owner.</summary>
public interface IDesignWorkingCopies
{
    Task<DesignScene?> GetAsync(Guid designId, CancellationToken cancellationToken);

    /// <summary>The garden's most recently saved design, or null when it has none.</summary>
    Task<DesignScene?> LatestForGardenAsync(Guid gardenId, CancellationToken cancellationToken);
}
