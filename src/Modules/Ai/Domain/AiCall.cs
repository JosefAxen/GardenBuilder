using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Domain;

/// <summary>One call to an AI provider, also a failed one: the cost log in ai.calls.</summary>
internal sealed class AiCall
{
    private AiCall()
    {
    }

    public Guid Id { get; private set; }

    public Guid? JobId { get; private set; }

    /// <summary>For example "connection-test@1".</summary>
    public string PromptVersion { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    public int DurationMs { get; private set; }

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    public Money Cost { get; private set; }

    /// <summary>"ok", "refused", "cut-off" or "error".</summary>
    public string Outcome { get; private set; } = string.Empty;

    /// <summary>The kind of error for outcome "error", for example the exception type.</summary>
    public string? ErrorType { get; private set; }

    public static AiCall Create(
        Guid? jobId, string promptVersion, string provider, string model, DateTimeOffset startedAt, int durationMs,
        int inputTokens, int outputTokens, Money cost, string outcome, string? errorType) => new()
    {
        Id = Ids.New(),
        JobId = jobId,
        PromptVersion = promptVersion,
        Provider = provider,
        Model = model,
        StartedAt = startedAt,
        DurationMs = durationMs,
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        Cost = cost,
        Outcome = outcome,
        ErrorType = errorType,
    };
}
