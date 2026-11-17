using System.Text.Json;

namespace GardenBuilder.Contracts.Ai;

/// <summary>
/// Puts AI jobs in the queue the Worker runs (docs/superpowers/specs/2026-11-17-ai-jobs-design.md). The job is saved in
/// its own transaction, so every kind of job must be safe to run again.
/// </summary>
public interface IAiJobs
{
    /// <summary>Queues a job and returns its id. Throws <see cref="AiJobRefusedException"/> when the job may not start.</summary>
    Task<Guid> EnqueueAsync(string kind, Guid? gardenId, Guid? customerId, JsonElement input, CancellationToken cancellationToken);
}

/// <summary>Why a job was not queued.</summary>
/// <param name="code">"step-off", "budget-reached" or "quota-reached".</param>
public sealed class AiJobRefusedException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
