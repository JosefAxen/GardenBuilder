using System.Text.Json;
using GardenBuilder.Contracts.Ai;
using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Queue;
using GardenBuilder.Modules.Ai.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Ai.Features.DesignProposals;

/// <summary>
/// "Ge mig förslag" for the customer: start a round, poll it, fetch the latest again, and choose a proposal (which
/// freezes a design version). The garden, the design and the job must all be the current customer's; anything else
/// answers exactly like something that does not exist.
/// </summary>
internal static class DesignProposalEndpoints
{
    public sealed record StartBody(Guid DesignId, string Style, string Budget, string Care, int Zone, string? Note);

    public sealed record ChosenBody(Guid DesignId, int Index);

    public sealed record CodeResponse(string Code);

    public sealed record ProblemsResponse(string Code, IReadOnlyList<string> Problems);

    public sealed record JobResponse(Guid Id, string Status, JsonElement? Result, string? Failure);

    public static RouteGroupBuilder MapDesignProposals(this RouteGroupBuilder group)
    {
        group.MapPost("/{gardenId:guid}/design-proposals", StartAsync).WithName("StartDesignProposals");
        group.MapGet("/{gardenId:guid}/design-proposals/{jobId:guid}", GetAsync).WithName("GetDesignProposals");
        group.MapGet("/{gardenId:guid}/design-proposals/latest", GetLatestAsync).WithName("GetLatestDesignProposals");
        group.MapPost("/{gardenId:guid}/design-proposals/{jobId:guid}/chosen", ChooseAsync).WithName("ChooseDesignProposal");
        return group;
    }

    private static async Task<DesignScene?> OwnDesignAsync(
        Guid gardenId, Guid designId, ICurrentCustomer customer, IDesignWorkingCopies designs, CancellationToken cancellationToken) =>
        await customer.FindAsync(cancellationToken) is { } customerId
        && await designs.GetAsync(designId, cancellationToken) is { } design
        && design.CustomerId == customerId && design.GardenId == gardenId
            ? design
            : null;

    private static async Task<IResult> StartAsync(
        Guid gardenId, StartBody body, ICurrentCustomer customer, IDesignWorkingCopies designs, IAiJobs jobs, CancellationToken cancellationToken)
    {
        if (await OwnDesignAsync(gardenId, body.DesignId, customer, designs, cancellationToken) is not { } design)
        {
            return TypedResults.NotFound();
        }

        var wishes = new ProposalWishes(body.Style, body.Budget, body.Care, body.Zone, string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim());
        if (wishes.Problems() is { Count: > 0 } problems)
        {
            return TypedResults.BadRequest(new ProblemsResponse("invalid-wishes", problems));
        }

        var input = JsonSerializer.SerializeToElement(new DesignProposalInput(design.DesignId, wishes, design.Revision), DesignProposalHandler.Json);
        try
        {
            var jobId = await jobs.EnqueueAsync(AiJobKinds.DesignProposal, gardenId, design.CustomerId, input, cancellationToken);
            return TypedResults.Accepted($"/api/gardens/{gardenId}/design-proposals/{jobId}", new { id = jobId });
        }
        catch (AiJobRefusedException e)
        {
            return TypedResults.Conflict(new CodeResponse(e.Code));
        }
    }

    private static async Task<AiJob?> OwnJobAsync(Guid gardenId, Guid jobId, ICurrentCustomer customer, JobQueue queue, CancellationToken cancellationToken) =>
        await customer.FindAsync(cancellationToken) is { } customerId
        && await queue.FindAsync(jobId, cancellationToken) is { } job
        && job.Kind == AiJobKinds.DesignProposal && job.CustomerId == customerId && job.GardenId == gardenId
            ? job
            : null;

    private static JobResponse ToResponse(AiJob job) => new(job.Id, AiJobConfiguration.StatusText(job.Status), job.Result, job.Failure);

    private static async Task<IResult> GetAsync(Guid gardenId, Guid jobId, ICurrentCustomer customer, JobQueue queue, CancellationToken cancellationToken) =>
        await OwnJobAsync(gardenId, jobId, customer, queue, cancellationToken) is { } job ? TypedResults.Ok(ToResponse(job)) : TypedResults.NotFound();

    /// <summary>The garden's most recent round, so proposals are not lost when the customer leaves the page.</summary>
    private static async Task<IResult> GetLatestAsync(Guid gardenId, ICurrentCustomer customer, AiDbContext db, CancellationToken cancellationToken)
    {
        if (await customer.FindAsync(cancellationToken) is not { } customerId
            || await db.Set<AiJob>().AsNoTracking()
                .Where(j => j.GardenId == gardenId && j.CustomerId == customerId && j.Kind == AiJobKinds.DesignProposal)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken) is not { } job)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToResponse(job));
    }

    /// <summary>The customer chose a proposal and the editor has saved it: freeze the working copy as a version.</summary>
    private static async Task<IResult> ChooseAsync(
        Guid gardenId, Guid jobId, ChosenBody body, ICurrentCustomer customer, JobQueue queue, IDesignWorkingCopies designs,
        IDesignVersions versions, CancellationToken cancellationToken)
    {
        if (await OwnJobAsync(gardenId, jobId, customer, queue, cancellationToken) is not { } job
            || await OwnDesignAsync(gardenId, body.DesignId, customer, designs, cancellationToken) is null)
        {
            return TypedResults.NotFound();
        }

        var count = job.Result is { } result && result.TryGetProperty("proposals", out var proposals) ? proposals.GetArrayLength() : 0;
        if (job.Status != AiJobStatus.Succeeded || body.Index < 0 || body.Index >= count)
        {
            return TypedResults.BadRequest(new CodeResponse("no-such-proposal"));
        }

        await versions.CreateAsync(body.DesignId, DesignVersionReason.AiProposal, cancellationToken);
        return TypedResults.NoContent();
    }
}
