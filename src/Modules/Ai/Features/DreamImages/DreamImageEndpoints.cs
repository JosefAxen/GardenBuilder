using System.Text.Json;
using GardenBuilder.Contracts.Ai;
using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Ai.Features.DreamImages;

/// <summary>
/// "Skapa drömbild" for the customer: upload the render of a saved view, list the garden's images with fresh links,
/// read one, and remove a failed one. The garden, the design and the job must all be the current customer's;
/// anything else answers exactly like something that does not exist.
/// </summary>
internal static class DreamImageEndpoints
{
    public sealed record CodeResponse(string Code);

    public sealed record DreamImageResponse(
        Guid Id, string Status, string ViewName, int Revision, bool Test, DateTimeOffset CreatedAt, Uri? ImageUrl, Uri? DownloadUrl, Uri RenderUrl, string? Failure);

    public static RouteGroupBuilder MapDreamImages(this RouteGroupBuilder group)
    {
        group.MapPost("/{gardenId:guid}/dream-images", StartAsync)
            .WithName("StartDreamImage")
            // Room for an 8 MB render and the form around it, so a larger one is answered "invalid-image" below.
            .WithMetadata(new RequestSizeLimitAttribute(12 * 1024 * 1024));
        group.MapGet("/{gardenId:guid}/dream-images", ListAsync).WithName("ListDreamImages");
        group.MapGet("/{gardenId:guid}/dream-images/{jobId:guid}", GetAsync).WithName("GetDreamImage");
        group.MapDelete("/{gardenId:guid}/dream-images/{jobId:guid}", DeleteAsync).WithName("DeleteDreamImage");
        return group;
    }

    private static async Task<IResult> StartAsync(
        Guid gardenId, HttpRequest request, ICurrentCustomer customer, IDesignWorkingCopies designs, AiJobs jobs, DreamImageStore store,
        CancellationToken cancellationToken)
    {
        var form = request.HasFormContentType ? await request.ReadFormAsync(cancellationToken) : null;
        if (form is null
            || !Guid.TryParse(form["designId"], out var designId)
            || await customer.FindAsync(cancellationToken) is not { } customerId
            || await designs.GetAsync(designId, cancellationToken) is not { } design
            || design.CustomerId != customerId || design.GardenId != gardenId)
        {
            return TypedResults.NotFound();
        }

        if (!Guid.TryParse(form["viewId"], out var viewId) || design.Scene.ViewsOrEmpty.All(v => v.Id != viewId))
        {
            return TypedResults.BadRequest(new CodeResponse("no-such-view"));
        }

        var file = form.Files.GetFile("render");
        if (file is null || file.Length > DreamRender.MaxBytes)
        {
            return TypedResults.BadRequest(new CodeResponse("invalid-image"));
        }

        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }

        if (DreamRender.Encode(bytes) is not { } jpeg)
        {
            return TypedResults.BadRequest(new CodeResponse("invalid-image"));
        }

        // The render is stored before the job is queued, so the Worker never finds a job without it.
        var jobId = Ids.New();
        var renderBlob = DreamImageStore.RenderBlob(gardenId, jobId);
        await store.UploadAsync(renderBlob, jpeg, cancellationToken);
        var sendPhotos = bool.TryParse(form["sendPhotos"], out var send) && send;
        var input = JsonSerializer.SerializeToElement(new DreamImageInput(designId, viewId, design.Revision, sendPhotos), DreamImageHandler.Json);
        try
        {
            await jobs.EnqueueAsync(AiJobKinds.DreamImage, gardenId, customerId, requestedByStaffId: null, input, jobId, cancellationToken);
        }
        catch (AiJobRefusedException e)
        {
            await store.DeleteAsync(renderBlob, CancellationToken.None);
            return TypedResults.Conflict(new CodeResponse(e.Code));
        }

        return TypedResults.Accepted($"/api/gardens/{gardenId}/dream-images/{jobId}", new { id = jobId });
    }

    /// <summary>True when the garden is the current customer's, judged by its design (every garden has one).</summary>
    private static async Task<Guid?> OwnGardenAsync(Guid gardenId, ICurrentCustomer customer, IDesignWorkingCopies designs, CancellationToken cancellationToken) =>
        await customer.FindAsync(cancellationToken) is { } customerId
        && await designs.LatestForGardenAsync(gardenId, cancellationToken) is { } design
        && design.CustomerId == customerId
            ? customerId
            : null;

    private static IQueryable<AiJob> Jobs(AiDbContext db, Guid gardenId, Guid customerId) =>
        db.Set<AiJob>().AsNoTracking().Where(j => j.GardenId == gardenId && j.CustomerId == customerId && j.Kind == AiJobKinds.DreamImage);

    private static async Task<IResult> ListAsync(
        Guid gardenId, ICurrentCustomer customer, IDesignWorkingCopies designs, AiDbContext db, DreamImageStore store,
        CancellationToken cancellationToken)
    {
        if (await OwnGardenAsync(gardenId, customer, designs, cancellationToken) is not { } customerId)
        {
            return TypedResults.NotFound();
        }

        var jobs = await Jobs(db, gardenId, customerId).OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).ToListAsync(cancellationToken);
        var names = new ViewNames(designs);
        var response = new List<DreamImageResponse>(jobs.Count);
        foreach (var job in jobs)
        {
            response.Add(await ToResponseAsync(job, names, store, cancellationToken));
        }

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetAsync(
        Guid gardenId, Guid jobId, ICurrentCustomer customer, IDesignWorkingCopies designs, AiDbContext db, DreamImageStore store,
        CancellationToken cancellationToken)
    {
        if (await OwnGardenAsync(gardenId, customer, designs, cancellationToken) is not { } customerId
            || await Jobs(db, gardenId, customerId).FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken) is not { } job)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await ToResponseAsync(job, new ViewNames(designs), store, cancellationToken));
    }

    /// <summary>Removes a failed image from the gallery, with its blobs. Images that worked or are on their way stay.</summary>
    private static async Task<IResult> DeleteAsync(
        Guid gardenId, Guid jobId, ICurrentCustomer customer, IDesignWorkingCopies designs, AiDbContext db, DreamImageStore store,
        CancellationToken cancellationToken)
    {
        if (await OwnGardenAsync(gardenId, customer, designs, cancellationToken) is not { } customerId
            || await Jobs(db, gardenId, customerId).FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken) is not { } job)
        {
            return TypedResults.NotFound();
        }

        // Only a failed job is removed; the status is checked again in the delete itself.
        var removed = await db.Set<AiJob>().Where(j => j.Id == job.Id && j.Status == AiJobStatus.Failed).ExecuteDeleteAsync(cancellationToken);
        if (removed == 0)
        {
            return TypedResults.Conflict(new CodeResponse("not-failed"));
        }

        await store.DeleteAsync(DreamImageStore.RenderBlob(gardenId, job.Id), cancellationToken);
        await store.DeleteAsync(DreamImageStore.ImageBlob(gardenId, job.Id), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<DreamImageResponse> ToResponseAsync(AiJob job, ViewNames names, DreamImageStore store, CancellationToken cancellationToken)
    {
        var input = job.Input.Deserialize<DreamImageInput>(DreamImageHandler.Json);
        var result = job.Status == AiJobStatus.Succeeded ? job.Result?.Deserialize<DreamImageResult>(DreamImageHandler.Json) : null;
        var gardenId = job.GardenId!.Value;
        var viewName = result?.ViewName
            ?? (input is null ? null : await names.FindAsync(input.DesignId, input.ViewId, cancellationToken))
            ?? DreamImageHandler.RemovedView;
        return new DreamImageResponse(
            job.Id,
            AiJobConfiguration.StatusText(job.Status),
            viewName,
            result?.Revision ?? input?.Revision ?? 0,
            result?.Test ?? false,
            job.CreatedAt,
            result is null ? null : store.LinkFor(DreamImageStore.ImageBlob(gardenId, job.Id)),
            result is null ? null : store.DownloadLinkFor(DreamImageStore.ImageBlob(gardenId, job.Id)),
            store.LinkFor(DreamImageStore.RenderBlob(gardenId, job.Id)),
            job.Status == AiJobStatus.Failed ? job.Failure : null);
    }

    /// <summary>View names from the current designs, read once per design, for images that are not finished yet.</summary>
    private sealed class ViewNames(IDesignWorkingCopies designs)
    {
        private readonly Dictionary<Guid, DesignScene?> _designs = [];

        public async Task<string?> FindAsync(Guid designId, Guid viewId, CancellationToken cancellationToken)
        {
            if (!_designs.TryGetValue(designId, out var design))
            {
                design = await designs.GetAsync(designId, cancellationToken);
                _designs[designId] = design;
            }

            return design?.Scene.ViewsOrEmpty.FirstOrDefault(v => v.Id == viewId)?.Name;
        }
    }
}
