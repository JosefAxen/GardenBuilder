using System.Text.Json;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Providers;

namespace GardenBuilder.Modules.Ai.Features.ConnectionTest;

/// <summary>
/// The admin's "Testa AI-anslutningen": one short, fixed question, so the key, the queue, the call and the cost log can
/// be seen working before the real AI steps exist.
/// </summary>
internal sealed class ConnectionTestHandler : IAiJobHandler
{
    public const string PromptVersion = "connection-test@1";
    public const string Prompt = "Svara med en mening på svenska: vad är en trädgård?";

    public string Kind => AiJobKinds.ConnectionTest;

    public async Task<JsonElement> RunAsync(AiJob job, IAiCalls calls, CancellationToken cancellationToken)
    {
        var result = await calls.TextAsync(PromptVersion, new TextRequest(null, Prompt, 256), cancellationToken);
        return JsonSerializer.SerializeToElement(new
        {
            answer = result.Response.Text,
            model = result.Response.Model,
            durationMs = result.DurationMs,
            cost = result.Cost.Amount,
        });
    }
}
