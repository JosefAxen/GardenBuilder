using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GardenBuilder.Modules.Ai.Features.ProcessJobs;

/// <summary>
/// Runs only in the Worker host. Every two seconds it runs the jobs that are due, one at a time
/// (docs/superpowers/specs/2026-11-17-ai-jobs-design.md, "Jobben").
/// </summary>
internal sealed partial class AiJobProcessor(
    IServiceScopeFactory scopes,
    IOptions<AnthropicOptions> anthropic,
    IOptions<AiPricingOptions> pricing,
    ILogger<AiJobProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger, PollInterval);
        if (string.IsNullOrWhiteSpace(anthropic.Value.ApiKey))
        {
            LogNoKey(logger);
        }

        foreach (var model in anthropic.Value.AllModels.Where(m => !pricing.Value.HasPrice(m)))
        {
            LogNoPrice(logger, model);
        }

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // Run what is due before waiting for the next tick.
                while (await RunOneAsync(stoppingToken))
                {
                }
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                // The database may be briefly away; the next tick tries again.
                LogTickFailed(logger, e);
            }
        }
    }

    private async Task<bool> RunOneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<JobRunner>().RunNextAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "AI job processor started, polling every {Interval}")]
    private static partial void LogStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No API key for Claude is set (Anthropic:ApiKey); AI jobs will fail until it is")]
    private static partial void LogNoKey(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The model {Model} has no price in Ai:Prices; AI jobs will fail until it has")]
    private static partial void LogNoPrice(ILogger logger, string model);

    [LoggerMessage(Level = LogLevel.Error, Message = "Running AI jobs failed")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
