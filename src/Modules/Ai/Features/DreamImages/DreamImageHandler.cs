using System.Text.Json;
using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Providers;

namespace GardenBuilder.Modules.Ai.Features.DreamImages;

/// <summary>The job's input: the design and view the render was made from, the design's revision, and the photo choice.</summary>
internal sealed record DreamImageInput(Guid DesignId, Guid ViewId, int Revision, bool SendPhotos);

/// <summary>What the gallery shows next to the image. <paramref name="Test"/> is true when the test model made it.</summary>
internal sealed record DreamImageResult(string ViewName, int Revision, bool Test);

/// <summary>
/// Turns the render the customer saw into a photo-like image (spec "Drömbild"). The render comes from the store, never
/// from the request; the customer's photos go along only when they allowed it. The answer is checked before it is stored.
/// </summary>
internal sealed class DreamImageHandler(
    DreamImageStore store, IDesignWorkingCopies designs, IGardenPhotos photos, ICatalogPrices catalog) : IAiJobHandler
{
    public const string Failed = "Det gick inte att skapa en drömbild just nu.";

    /// <summary>The name shown when the view was removed after the job was queued.</summary>
    public const string RemovedView = "Vy";

    public const int Width = 1536;
    public const int Height = 1024;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Kind => AiJobKinds.DreamImage;

    public async Task<JsonElement> RunAsync(AiJob job, IAiCalls calls, CancellationToken cancellationToken)
    {
        var input = job.Input.Deserialize<DreamImageInput>(Json) ?? throw new AiProviderException(Failed, retryable: false);
        var gardenId = job.GardenId ?? throw new AiProviderException(Failed, retryable: false);
        var render = await store.ReadAsync(DreamImageStore.RenderBlob(gardenId, job.Id), cancellationToken)
            ?? throw new AiProviderException(Failed, retryable: false);

        var design = await designs.GetAsync(input.DesignId, cancellationToken);
        var scene = design?.Scene ?? new SceneDocument(SceneDocument.CurrentFormatVersion, []);
        var view = scene.ViewsOrEmpty.FirstOrDefault(v => v.Id == input.ViewId);

        var references = await PhotosAsync(gardenId, view, input.SendPhotos, cancellationToken);
        var prompt = DreamPrompt.Text(await ItemsAsync(scene, cancellationToken), references.Count);
        var response = await calls.ImageAsync(
            DreamPrompt.Version, new ImageRequest(prompt, render, references, Width, Height), cancellationToken);
        if (!DreamImageCheck.Accept(response.Jpeg))
        {
            throw new AiProviderException(Failed, retryable: false);
        }

        await store.UploadAsync(DreamImageStore.ImageBlob(gardenId, job.Id), response.Jpeg, cancellationToken);
        var result = new DreamImageResult(view?.Name ?? RemovedView, input.Revision, response.Model == TestImageModel.ModelName);
        return JsonSerializer.SerializeToElement(result, Json);
    }

    /// <summary>
    /// The chosen photos' bytes. Without the view (it was removed) there is no direction to compare, so the photos are
    /// taken as if none had a pose.
    /// </summary>
    private async Task<IReadOnlyList<byte[]>> PhotosAsync(Guid gardenId, SceneView? view, bool sendPhotos, CancellationToken cancellationToken)
    {
        if (!sendPhotos)
        {
            return [];
        }

        var all = await photos.ListAsync(gardenId, cancellationToken);
        var chosen = view is null
            ? [.. all.Take(DreamPhotos.Max).Select(p => p.Id)]
            : DreamPhotos.Choose(view, all, sendPhotos);
        var bytes = new List<byte[]>();
        foreach (var id in chosen)
        {
            if (await photos.ReadAsync(id, cancellationToken) is { } photo)
            {
                bytes.Add(photo);
            }
        }

        return bytes;
    }

    /// <summary>Every article that is new or kept, as "name × count", in the order it first appears.</summary>
    private async Task<IReadOnlyList<(string Name, int Count)>> ItemsAsync(SceneDocument scene, CancellationToken cancellationToken)
    {
        var counts = scene.Elements
            .Where(e => e.Status is ElementStatus.New or ElementStatus.ExistingKeep)
            .Select(ArticleOf)
            .OfType<Guid>()
            .GroupBy(id => id)
            .Select(g => (Id: g.Key, Count: g.Count()))
            .ToList();
        if (counts.Count == 0)
        {
            return [];
        }

        var names = (await catalog.GetAsync([.. counts.Select(c => c.Id)], cancellationToken)).ToDictionary(a => a.Id, a => a.Name);
        return [.. counts.Where(c => names.ContainsKey(c.Id)).Select(c => (names[c.Id], c.Count))];
    }

    private static Guid? ArticleOf(SceneElement element) => element switch
    {
        PointElement p => p.ArticleId,
        LineElement l => l.ArticleId,
        SurfaceElement s => s.ArticleId,
        _ => null,
    };
}
