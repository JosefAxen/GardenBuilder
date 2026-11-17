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
}

/// <summary>Claude through Anthropic's official C# SDK. The queue retries, so the SDK's own retries are off.</summary>
internal sealed class ClaudeTextModel(IOptions<AnthropicOptions> options) : ITextModel, IDisposable
{
    public const string NoKey = "Ingen API-nyckel för Claude är inställd.";

    private readonly AnthropicOptions _options = options.Value;
    private AnthropicClient? _client;

    public string Provider => "anthropic";

    public string Model => _options.Model;

    public async Task<TextResponse> CompleteAsync(TextRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AiProviderException(NoKey, retryable: false);
        }

        _client ??= new AnthropicClient { ApiKey = _options.ApiKey, MaxRetries = 0 };
        MessageCreateParamsSystem? system = request.System is { } instructions ? (MessageCreateParamsSystem)instructions : null;
        Message message;
        try
        {
            message = await _client.Messages.Create(
                new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = request.MaxTokens,
                    System = system,
                    Messages = [new UserMessageParam { Content = request.Prompt }],
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
