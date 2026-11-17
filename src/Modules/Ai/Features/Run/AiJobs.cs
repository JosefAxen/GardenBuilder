using System.Text.Json;
using GardenBuilder.Contracts.Ai;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Queue;
using GardenBuilder.Modules.Ai.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Ai.Features.Run;

/// <summary>Queues AI jobs after the switch, budget and quota checks. Used by the API and by other modules.</summary>
internal sealed class AiJobs(JobQueue queue, AiGuards guards, AiDbContext db, TimeProvider time) : IAiJobs
{
    public Task<Guid> EnqueueAsync(string kind, Guid? gardenId, Guid? customerId, JsonElement input, CancellationToken cancellationToken) =>
        EnqueueAsync(kind, gardenId, customerId, requestedByStaffId: null, input, cancellationToken);

    public async Task<Guid> EnqueueAsync(
        string kind, Guid? gardenId, Guid? customerId, Guid? requestedByStaffId, JsonElement input, CancellationToken cancellationToken)
    {
        // Jobs that failed do not count against the quota: the customer got the fallback, not an answer.
        var refusal = await guards.RefuseEnqueueAsync(
            kind,
            gardenId,
            () => db.Set<AiJob>().CountAsync(j => j.GardenId == gardenId && j.Kind == kind && j.Status != AiJobStatus.Failed, cancellationToken),
            cancellationToken);
        if (refusal is not null)
        {
            throw new AiJobRefusedException(refusal);
        }

        var job = AiJob.Create(kind, gardenId, customerId, requestedByStaffId, input, time.GetUtcNow());
        await queue.AddAsync(job, cancellationToken);
        return job.Id;
    }
}
