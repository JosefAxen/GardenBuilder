using System.Text.Json;
using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Providers;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Features.PhotoInterpretation;

/// <summary>The job's input: the design whose plot and house the photos are placed on, and the photos to interpret.</summary>
internal sealed record PhotoInterpretationInput(Guid DesignId, IReadOnlyList<Guid> PhotoIds);

/// <summary>One proposed element as stored in the job's result.</summary>
internal sealed record ProposedElementResult(string Label, double Confidence, JsonElement Element);

internal sealed record PhotoInterpretationResult(
    IReadOnlyList<ProposedElementResult> Elements, HouseLook? House, IReadOnlyList<string> Dropped, int SkippedPhotos);

/// <summary>
/// Interprets a garden's photos: one call to Claude per photo, then plain code places the answers on the plot and every
/// proposed element must pass the same scene check a saved design does. The design itself is never changed; the
/// customer takes what they want from the result. Safe to run again: a new attempt starts over and replaces the result.
/// </summary>
internal sealed class PhotoInterpretationHandler(IDesignWorkingCopies designs, IGardenPhotos photos, ISceneCheck sceneCheck) : IAiJobHandler
{
    public const string NothingUsable = "Vi kunde inte tolka fotona.";
    public const string DesignGone = "Designen finns inte längre.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Kind => AiJobKinds.PhotoInterpretation;

    public async Task<JsonElement> RunAsync(AiJob job, IAiCalls calls, CancellationToken cancellationToken)
    {
        var input = job.Input.Deserialize<PhotoInterpretationInput>(Json) ?? throw new AiProviderException(NothingUsable, retryable: false);
        var design = await designs.GetAsync(input.DesignId, cancellationToken) ?? throw new AiProviderException(DesignGone, retryable: false);
        var plot = design.Scene.Elements.OfType<PlotElement>().FirstOrDefault()?.Points ?? throw new AiProviderException(NothingUsable, retryable: false);
        var house = design.Scene.Elements.OfType<BuildingElement>().FirstOrDefault()?.Points ?? [];
        var known = (await photos.ListAsync(design.GardenId, cancellationToken)).ToDictionary(p => p.Id);

        var answers = new List<(PhotoAnswer, CameraPose)>();
        var skipped = 0;
        foreach (var photoId in input.PhotoIds)
        {
            // A photo removed or moved after the job was queued is skipped, not guessed.
            if (!known.TryGetValue(photoId, out var info) || info.Camera is not { } camera
                || await photos.ReadAsync(photoId, cancellationToken) is not { } jpeg)
            {
                skipped++;
                continue;
            }

            var result = await calls.TextAsync(
                PhotoPrompt.Version,
                new TextRequest(null, PhotoPrompt.Text, PhotoPrompt.MaxTokens, Images: [new ImageInput(jpeg)], OutputSchema: PhotoPrompt.Schema),
                cancellationToken);
            if (Parse(result.Response.Text) is { } answer)
            {
                answers.Add((answer, camera));
            }
            else
            {
                skipped++;
            }
        }

        if (answers.Count == 0)
        {
            throw new AiProviderException(NothingUsable, retryable: false);
        }

        var proposal = PhotoPlacement.Place(answers, plot, house);
        var (accepted, refused) = Check(design.Scene, proposal.Elements);
        var stored = new PhotoInterpretationResult(
            [.. accepted.Select(e => new ProposedElementResult(e.Label, e.Confidence, JsonSerializer.SerializeToElement(e.Element, SceneJson.Options)))],
            proposal.House,
            [.. proposal.Dropped, .. refused],
            skipped);
        return JsonSerializer.SerializeToElement(stored, Json);
    }

    private static PhotoAnswer? Parse(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<PhotoAnswer>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Adds the proposals one by one to the design's scene; any the scene check refuses are left out.</summary>
    private (List<ProposedElement> Accepted, List<string> Refused) Check(SceneDocument scene, IReadOnlyList<ProposedElement> proposed)
    {
        var accepted = new List<ProposedElement>();
        var refused = new List<string>();
        foreach (var element in proposed)
        {
            var trial = scene with { Elements = [.. scene.Elements, .. accepted.Select(a => a.Element), element.Element] };
            if (sceneCheck.Check(trial).Any(p => p.ElementId == element.Element.Id))
            {
                refused.Add($"{element.Label} (passar inte på tomten)");
            }
            else
            {
                accepted.Add(element);
            }
        }

        return (accepted, refused);
    }
}
