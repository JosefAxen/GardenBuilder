using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

namespace GardenBuilder.Modules.Ai.Providers;

/// <summary>The "Anthropic" settings. The key comes from user secrets locally and from Key Vault in production.</summary>
internal sealed class AnthropicOptions
{
    public const string Section = "Anthropic";

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-haiku-5-5";

    /// <summary>A model per kind of job, e.g. "photo-interpretation": "claude-sonnet-5-5". Kinds not listed use <see cref="Model"/>.</summary>
    public Dictionary<string, string> Models { get; set; } = [];

    public string ModelFor(string kind) => Models.TryGetValue(kind, out var model) && !string.IsNullOrWhiteSpace(model) ? model : Model;

    /// <summary>Every model a job may use, for checking at start that each has a price.</summary>
    public IEnumerable<string> AllModels => Models.Values.Append(Model).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.Ordinal);
}

/// <summary>Claude through Anthropic's official C# SDK. The queue retries, so the SDK's own retries are off.</summary>
internal sealed class ClaudeTextModel(IOptions<AnthropicOptions> options) : ITextModel, IDisposable
{
    public const string NoKey = "Ingen API-nyckel för Claude är inställd.";
    public static readonly TimeSpan CallTimeout = TimeSpan.FromMinutes(2);

    private readonly AnthropicOptions _options = options.Value;
    private AnthropicClient? _client;

    public string Provider => "anthropic";

    public string ModelFor(string kind) => _options.ModelFor(kind);

    public async Task<TextResponse> CompleteAsync(TextRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AiProviderException(NoKey, retryable: false);
        }

        // Well inside the queue's five-minute lease, so a slow call ends before another Worker may take the job.
        _client ??= new AnthropicClient { ApiKey = _options.ApiKey, MaxRetries = 0, Timeout = CallTimeout };
        MessageCreateParamsSystem? system = request.System is { } instructions ? (MessageCreateParamsSystem)instructions : null;
        var content = new List<ContentBlockParam>();
        foreach (var image in request.Images ?? [])
        {
            content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(image.Jpeg), MediaType = MediaType.ImageJpeg } });
        }

        content.Add(new TextBlockParam { Text = request.Prompt });
        var output = request.OutputSchema is { } schema
            ? new OutputConfig { Format = new JsonOutputFormat { Schema = schema.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()) } }
            : null;
        Message message;
        try
        {
            message = await _client.Messages.Create(
                new MessageCreateParams
                {
                    Model = string.IsNullOrWhiteSpace(request.Model) ? _options.Model : request.Model,
                    MaxTokens = request.MaxTokens,
                    System = system,
                    Messages = [new UserMessageParam { Content = content }],
                    OutputConfig = output,
                },
                cancellationToken);
        }
        catch (Exception e) when (e is AnthropicRateLimitException or Anthropic5xxException or AnthropicIOException
            or AnthropicRequestTimeoutException or AnthropicOverloadedException or AnthropicServiceUnavailableException
            or AnthropicGatewayTimeoutException)
        {
            throw new AiProviderException("Claude svarade inte just nu.", retryable: true, e);
        }
        catch (AnthropicUnauthorizedException e)
        {
            throw new AiProviderException("API-nyckeln för Claude godtogs inte.", retryable: false, e);
        }
        catch (AnthropicApiException e)
        {
            throw new AiProviderException("Claude kunde inte ta emot frågan.", retryable: false, e);
        }

        var text = string.Concat(message.Content.Select(b => b.TryPickText(out var t) ? t.Text : string.Empty));
        return new TextResponse(
            text, message.Model, (int)message.Usage.InputTokens, (int)message.Usage.OutputTokens, message.StopReason?.Raw() ?? string.Empty);
    }

    public void Dispose() => _client?.Dispose();
}
