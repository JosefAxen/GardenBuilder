using System.Text.Json;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Domain;

internal enum AiJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
}

/// <summary>One job in ai.jobs. The queue (JobQueue) moves it from queued to succeeded or failed.</summary>
internal sealed class AiJob
{
    private AiJob()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>For example "connection-test" or "photo-interpretation".</summary>
    public string Kind { get; private set; } = string.Empty;

    public AiJobStatus Status { get; private set; }

    public JsonElement Input { get; private set; }

    public JsonElement? Result { get; private set; }

    /// <summary>A short reason in Swedish when the job failed.</summary>
    public string? Failure { get; private set; }

    public Guid? GardenId { get; private set; }

    public Guid? CustomerId { get; private set; }

    /// <summary>The staff member who started a test job.</summary>
    public Guid? RequestedByStaffId { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static AiJob Create(
        string kind, Guid? gardenId, Guid? customerId, Guid? requestedByStaffId, JsonElement input, DateTimeOffset now, Guid? id = null) => new()
    {
        Id = id ?? Ids.New(),
        Kind = kind,
        Status = AiJobStatus.Queued,
        Input = input.Clone(),
        GardenId = gardenId,
        CustomerId = customerId,
        RequestedByStaffId = requestedByStaffId,
        NextAttemptAt = now,
        CreatedAt = now,
    };
}
