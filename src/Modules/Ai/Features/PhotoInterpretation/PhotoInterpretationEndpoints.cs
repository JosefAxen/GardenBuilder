using System.Text.Json;
using GardenBuilder.Contracts.Ai;
using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Queue;
using GardenBuilder.Modules.Ai.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Ai.Features.PhotoInterpretation;

/// <summary>
/// "Tolka foton" for the customer: start an interpretation of the garden's photos and poll its result. The garden, the
/// design and the job must all be the current customer's; anything else answers exactly like something that does not exist.
/// </summary>
internal static class PhotoInterpretationEndpoints
{
    public sealed record StartBody(Guid DesignId);

    public sealed record CodeResponse(string Code);

    public sealed record JobResponse(string Status, JsonElement? Result, string? Failure);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapPhotoInterpretation(this RouteGroupBuilder group)
    {
        group.MapPost("/{gardenId:guid}/photo-interpretations", StartAsync).WithName("StartPhotoInterpretation");
        group.MapGet("/{gardenId:guid}/photo-interpretations/{jobId:guid}", GetAsync).WithName("GetPhotoInterpretation");
        return group;
    }

    private static async Task<IResult> StartAsync(
        Guid gardenId,
        StartBody body,
        ICurrentCustomer customer,
        IDesignWorkingCopies designs,
        IGardenPhotos photos,
        IAiJobs jobs,
        CancellationToken cancellationToken)
    {
        if (await customer.FindAsync(cancellationToken) is not { } customerId
            || await designs.GetAsync(body.DesignId, cancellationToken) is not { } design
            || design.CustomerId != customerId
            || design.GardenId != gardenId)
        {
            return TypedResults.NotFound();
        }

        var list = await photos.ListAsync(gardenId, cancellationToken);
        if (list.Count == 0)
        {
            return TypedResults.Conflict(new CodeResponse("no-photos"));
        }

        if (list.Any(p => p.Camera is null))
        {
            return TypedResults.Conflict(new CodeResponse("camera-missing"));
        }

        var input = JsonSerializer.SerializeToElement(new PhotoInterpretationInput(design.DesignId, [.. list.Select(p => p.Id)]), Json);
        try
        {
            var jobId = await jobs.EnqueueAsync(AiJobKinds.PhotoInterpretation, gardenId, customerId, input, cancellationToken);
            return TypedResults.Accepted($"/api/gardens/{gardenId}/photo-interpretations/{jobId}", new { id = jobId });
        }
        catch (AiJobRefusedException e)
        {
            return TypedResults.Conflict(new CodeResponse(e.Code));
        }
    }

    private static async Task<IResult> GetAsync(
        Guid gardenId, Guid jobId, ICurrentCustomer customer, JobQueue queue, CancellationToken cancellationToken)
    {
        if (await customer.FindAsync(cancellationToken) is not { } customerId
            || await queue.FindAsync(jobId, cancellationToken) is not { } job
            || job.Kind != AiJobKinds.PhotoInterpretation
            || job.CustomerId != customerId
            || job.GardenId != gardenId)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new JobResponse(AiJobConfiguration.StatusText(job.Status), job.Result, job.Failure));
    }
}
