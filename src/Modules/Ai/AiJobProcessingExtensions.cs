using GardenBuilder.Modules.Ai.Features.ProcessJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Ai;

public static class AiJobProcessingExtensions
{
    /// <summary>Registers the background job processor. Call from the Worker host only, never from the API.</summary>
    public static IHostApplicationBuilder AddAiJobProcessing(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddHostedService<AiJobProcessor>();
        return builder;
    }
}
