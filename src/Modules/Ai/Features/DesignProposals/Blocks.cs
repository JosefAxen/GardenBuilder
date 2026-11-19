using GardenBuilder.Contracts.Scenes;

namespace GardenBuilder.Modules.Ai.Features.DesignProposals;

/// <summary>
/// One building block the model chose (docs/superpowers/specs/2026-11-18-design-proposals-design.md, section 3). Only
/// the parameters its type uses are read; the layout engine decides the exact shapes.
/// </summary>
/// <param name="Type">"patio", "path", "gravel", "bed", "shrubs", "hedge", "tree", "fruit-trees", "lawn" or "furniture".</param>
/// <param name="Zone">A zone key such as "house-south", or "rest" for a lawn on all free ground without grass.</param>
/// <param name="Side">"north", "south", "east" or "west": the boundary a hedge follows.</param>
/// <param name="From">A path's start: "patio" or a zone key.</param>
/// <param name="To">A path's end: a zone key.</param>
internal sealed record Block(
    string Type, string? Zone, double? Area, double? Length, double? Depth, double? Width, string? Side,
    string? From, string? To, IReadOnlyList<Guid>? Articles, int? Count);

/// <summary>Why a block could not be used, in Swedish: it goes back to the model on a retry. Block −1 is the whole proposal.</summary>
internal sealed record BlockError(int Block, string Message);

/// <summary>The scene the blocks made, what went wrong, which blocks were placed, and which block made each new element.</summary>
internal sealed record Layout(
    SceneDocument Scene, IReadOnlyList<BlockError> Errors, IReadOnlyList<int> Placed, IReadOnlyDictionary<Guid, int> MadeBy);
