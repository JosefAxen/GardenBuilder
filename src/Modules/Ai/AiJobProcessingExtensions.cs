using GardenBuilder.Modules.Ai.Features.ConnectionTest;
using GardenBuilder.Modules.Ai.Features.ProcessJobs;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Ai;

public static class AiJobProcessingExtensions
{
    /// <summary>
    /// Registers the background job processor and the AI providers. Call from the Worker host only, never from the API:
    /// only the Worker calls providers and reads their keys.
    /// </summary>
    public static IHostApplicationBuilder AddAiJobProcessing(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.Section));
        builder.Services.AddSingleton<ITextModel, ClaudeTextModel>();
        builder.Services.AddAiJobRunner();
        builder.Services.AddHostedService<AiJobProcessor>();
        return builder;
    }

    /// <summary>The job runner and the handlers, without a provider or the background loop. Tests add a fake provider.</summary>
    internal static IServiceCollection AddAiJobRunner(this IServiceCollection services)
    {
        services.AddScoped<JobRunner>();
        services.AddScoped<IAiJobHandler, ConnectionTestHandler>();
        return services;
    }
}
