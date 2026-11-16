using GardenBuilder.Contracts.Scenes;

namespace GardenBuilder.Contracts.Designs;

/// <summary>Why a design version was frozen (docs/design.md, "Regler för data").</summary>
public enum DesignVersionReason
{
    AiProposal,
    QuoteRequest,
    StaffRevision,
}

/// <summary>A frozen version: its id and its number within the design (1, 2, 3 …).</summary>
public sealed record DesignVersionRef(Guid Id, int Number);

/// <summary>Immutable versions of a design, for modules such as AI proposals and quote requests.</summary>
public interface IDesignVersions
{
    /// <summary>Freezes the design's working copy as a new version. Null when the design does not exist.</summary>
    Task<DesignVersionRef?> CreateAsync(Guid designId, DesignVersionReason reason, CancellationToken cancellationToken);

    /// <summary>The version's scene. Null when the version does not exist.</summary>
    Task<SceneDocument?> GetAsync(Guid versionId, CancellationToken cancellationToken);
}
