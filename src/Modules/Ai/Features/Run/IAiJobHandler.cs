using System.Text.Json;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Providers;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Features.Run;

/// <summary>
/// Runs one kind of AI job. Register one per kind; sub-projects 8–10 add theirs without touching the queue. A handler
/// must be safe to run again: a new attempt replaces the result, it never adds to it.
/// </summary>
internal interface IAiJobHandler
{
    string Kind { get; }

    Task<JsonElement> RunAsync(AiJob job, IAiCalls calls, CancellationToken cancellationToken);
}

/// <summary>An answer and what the call took and cost.</summary>
internal sealed record AiTextResult(TextResponse Response, int DurationMs, Money Cost);

/// <summary>
/// The only way a handler calls a model: it checks the guards first and logs every call in ai.calls, also failed ones.
/// </summary>
internal interface IAiCalls
{
    Task<AiTextResult> TextAsync(string promptVersion, TextRequest request, CancellationToken cancellationToken);

    /// <summary>One image from the image model, with the same guards and logging as text. Tokens are logged as 0.</summary>
    Task<ImageResponse> ImageAsync(string promptVersion, ImageRequest request, CancellationToken cancellationToken);
}

/// <summary>A guard stopped the job before any call; trying again cannot help today.</summary>
internal sealed class AiJobStoppedException(string reason) : Exception(reason);
