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

    public Task<Guid> EnqueueAsync(
        string kind, Guid? gardenId, Guid? customerId, Guid? requestedByStaffId, JsonElement input, CancellationToken cancellationToken) =>
        EnqueueAsync(kind, gardenId, customerId, requestedByStaffId, input, jobId: null, cancellationToken);

    /// <param name="jobId">Chosen by the caller when something must be stored under the job's id before it is queued.</param>
    public async Task<Guid> EnqueueAsync(
        string kind, Guid? gardenId, Guid? customerId, Guid? requestedByStaffId, JsonElement input, Guid? jobId,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (gardenId is not null)
            {
                // One enqueue at a time per garden and kind, so two clicks at once cannot both take the last place.
                var key = $"ai-job:{gardenId}:{kind}";
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            }

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

            var job = AiJob.Create(kind, gardenId, customerId, requestedByStaffId, input, time.GetUtcNow(), jobId);
            await queue.AddAsync(job, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return job.Id;
        });
    }
}
