using GardenBuilder.Contracts.Scenes;

namespace GardenBuilder.Modules.Ai.Features.PhotoInterpretation;

/// <summary>
/// What Claude answers for one photo: a rough map seen from above with the camera at the origin, x to the right and
/// y straight ahead, in meters. Nothing here is trusted until <see cref="PhotoPlacement"/> has checked it.
/// </summary>
internal sealed record PhotoAnswer(string? Summary, PhotoHouse? House, IReadOnlyList<PhotoThing>? Elements, IReadOnlyList<string>? Unsure);

internal sealed record PhotoHouse(bool Visible, string? WallColor, string? RoofForm, string? RoofColor, double Confidence);

/// <param name="Type">lawn, flowerbed, gravel, patio, path, hedge, fence, tree, shrub, or something we do not support.</param>
/// <param name="Shape">area, line or point.</param>
/// <param name="Points">[x, y] pairs in the camera's frame.</param>
internal sealed record PhotoThing(
    string Type, string Label, string Shape, IReadOnlyList<double[]>? Points, double? WidthM, double? HeightM, double Confidence);

/// <summary>What the photos show, placed on the plot and checked: the customer accepts or rejects each element.</summary>
/// <param name="Dropped">Swedish one-liners about what was left out and why, e.g. "Förråd (stöds inte än)".</param>
internal sealed record PhotoProposal(IReadOnlyList<ProposedElement> Elements, HouseLook? House, IReadOnlyList<string> Dropped);

internal sealed record ProposedElement(string Label, double Confidence, SceneElement Element);

/// <summary>The house's colours as "#RRGGBB" (null when the photo did not tell) and its roof.</summary>
internal sealed record HouseLook(string? WallColor, string? RoofColor, RoofShape Roof);
