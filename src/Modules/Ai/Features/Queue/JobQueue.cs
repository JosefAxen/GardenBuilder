using System.Text.Json;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Ai.Features.Queue;

/// <summary>
/// The job queue in ai.jobs (docs/superpowers/specs/2026-11-17-ai-jobs-design.md, "Jobben"). A claim locks the row with
/// FOR UPDATE SKIP LOCKED, so two Workers never take the same job, and leases it for five minutes. A job whose lease
/// runs out is claimed again; after the third attempt it fails.
/// </summary>
internal sealed class JobQueue(AiDbContext db, TimeProvider time)
{
    public const int MaxAttempts = 3;
    public const string AbortedThreeTimes = "Jobbet avbröts tre gånger.";
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    /// <summary>The wait before attempt 2 and attempt 3.</summary>
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60)];

    public async Task AddAsync(AiJob job, CancellationToken cancellationToken)
    {
        db.Add(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<AiJob?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<AiJob>().AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    /// <summary>Takes the oldest job that is due, or null when none is. The job comes back running with one more attempt.</summary>
    public async Task<AiJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        // The connection retries on transient errors, so the transaction runs as one retriable unit.
        var id = await db.Database.CreateExecutionStrategy().ExecuteAsync(ClaimIdAsync, cancellationToken);
        return id is { } claimed ? await FindAsync(claimed, cancellationToken) : null;
    }

    private async Task<Guid?> ClaimIdAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // A job that died on its last attempt has nothing left to try.
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE ai.jobs SET status = 'failed', failure = {AbortedThreeTimes}, locked_until = NULL, completed_at = {now}
            WHERE status = 'running' AND locked_until < {now} AND attempts >= {MaxAttempts}
            """,
            cancellationToken);

        var ids = await db.Database.SqlQuery<Guid>(
            $"""
            SELECT id AS "Value" FROM ai.jobs
            WHERE (status = 'queued' AND next_attempt_at <= {now}) OR (status = 'running' AND locked_until < {now})
            ORDER BY created_at
            LIMIT 1
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        if (ids.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var lockedUntil = now + Lease;
        await db.Database.ExecuteSqlAsync(
            $"UPDATE ai.jobs SET status = 'running', attempts = attempts + 1, locked_until = {lockedUntil} WHERE id = {ids[0]}",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ids[0];
    }

    /// <summary>
    /// Stores the result of <paramref name="attempt"/>. A Worker whose lease ran out while another took the job changes
    /// nothing: only the attempt that holds the job may finish it.
    /// </summary>
    public async Task SucceedAsync(Guid id, int attempt, JsonElement result, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var json = result.GetRawText();
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE ai.jobs SET status = 'succeeded', result = CAST({json} AS jsonb), failure = NULL, locked_until = NULL,
                completed_at = {now}
            WHERE id = {id} AND status = 'running' AND attempts = {attempt}
            """,
            cancellationToken);
    }

    /// <summary>
    /// Records a failed attempt. With <paramref name="retry"/> and attempts left, the job waits 10 or 60 seconds and is
    /// queued again; otherwise it fails for good with <paramref name="failure"/>. Like <see cref="SucceedAsync"/>, only
    /// the attempt that holds the job may change it.
    /// </summary>
    public async Task FailAsync(Guid id, int attempt, string failure, bool retry, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (retry && attempt < MaxAttempts)
        {
            var next = now + RetryDelays[Math.Clamp(attempt - 1, 0, RetryDelays.Length - 1)];
            await db.Database.ExecuteSqlAsync(
                $"""
                UPDATE ai.jobs SET status = 'queued', failure = {failure}, locked_until = NULL, next_attempt_at = {next}
                WHERE id = {id} AND status = 'running' AND attempts = {attempt}
                """,
                cancellationToken);
            return;
        }

        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE ai.jobs SET status = 'failed', failure = {failure}, locked_until = NULL, completed_at = {now}
            WHERE id = {id} AND status = 'running' AND attempts = {attempt}
            """,
            cancellationToken);
    }
}
