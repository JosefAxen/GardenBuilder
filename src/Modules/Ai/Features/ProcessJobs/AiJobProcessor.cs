using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GardenBuilder.Modules.Ai.Features.ProcessJobs;

/// <summary>
/// Runs only in the Worker host. Picks AI jobs from the job table in PostgreSQL.
/// See docs/design.md, section "Jobbkön", for the rules this must follow:
/// FOR UPDATE SKIP LOCKED, lease with timeout, idempotent jobs, max three attempts.
/// </summary>
internal sealed partial class AiJobProcessor(ILogger<AiJobProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger, PollInterval);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // Not implemented yet (week 3): claim next job, run provider, validate, store result.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "AI job processor started, polling every {Interval}")]
    private static partial void LogStarted(ILogger logger, TimeSpan interval);
}
