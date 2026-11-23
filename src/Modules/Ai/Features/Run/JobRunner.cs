using GardenBuilder.Modules.Ai.Features.Costs;
using GardenBuilder.Modules.Ai.Features.Queue;
using GardenBuilder.Modules.Ai.Features.Settings;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GardenBuilder.Modules.Ai.Features.Run;

/// <summary>Claims the next job and runs it through the handler for its kind.</summary>
internal sealed partial class JobRunner(
    JobQueue queue,
    IEnumerable<IAiJobHandler> handlers,
    ITextModel model,
    IImageModel imageModel,
    AiGuards guards,
    CostLog costs,
    AiSettingsStore settings,
    IOptions<AiPricingOptions> pricing,
    TimeProvider time,
    ILogger<JobRunner> logger)
{
    public const string UnknownKind = "Okänd sorts AI-jobb.";
    public const string Unexpected = "Något gick fel i AI-steget.";

    /// <summary>Runs one job. False when no job was due.</summary>
    public async Task<bool> RunNextAsync(CancellationToken cancellationToken)
    {
        var job = await queue.ClaimNextAsync(cancellationToken);
        if (job is null)
        {
            return false;
        }

        var handler = handlers.FirstOrDefault(h => h.Kind == job.Kind);
        if (handler is null)
        {
            await queue.FailAsync(job.Id, job.Attempts, UnknownKind, retry: false, cancellationToken);
            return true;
        }

        var calls = new AiCalls(job, queue, model, imageModel, guards, costs, settings, pricing, time, logger);
        try
        {
            var result = await handler.RunAsync(job, calls, cancellationToken);
            await queue.SucceedAsync(job.Id, job.Attempts, result, cancellationToken);
        }
        catch (AiJobStoppedException e)
        {
            await queue.FailAsync(job.Id, job.Attempts, e.Message, retry: false, cancellationToken);
        }
        catch (AiProviderException e)
        {
            LogAttemptFailed(logger, job.Id, job.Attempts, e.Message);
            await queue.FailAsync(job.Id, job.Attempts, e.Message, e.Retryable, cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            LogUnexpected(logger, e, job.Id);
            await queue.FailAsync(job.Id, job.Attempts, Unexpected, retry: true, cancellationToken);
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI job {JobId} attempt {Attempt} failed: {Reason}")]
    private static partial void LogAttemptFailed(ILogger logger, Guid jobId, int attempt, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "AI job {JobId} failed unexpectedly")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, Guid jobId);
}
