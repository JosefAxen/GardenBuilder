using System.Text.Json;

namespace GardenBuilder.Modules.Ai.Providers;

/// <summary>A JPEG sent along with a question.</summary>
internal sealed record ImageInput(byte[] Jpeg);

/// <summary>A question to a language model. <paramref name="MaxTokens"/> caps the answer's length.</summary>
/// <param name="Model">Set by the caller of the model (AiCalls) from <see cref="ITextModel.ModelFor"/>; handlers leave it empty.</param>
/// <param name="Images">Sent before the prompt, in order.</param>
/// <param name="OutputSchema">A JSON schema the answer must follow (structured output), or null for free text.</param>
internal sealed record TextRequest(
    string? System, string Prompt, int MaxTokens, string Model = "", IReadOnlyList<ImageInput>? Images = null, JsonElement? OutputSchema = null);

/// <summary>
/// The model's answer and what it used. <paramref name="StopReason"/> is the provider's reason for stopping, for
/// example "end_turn", "max_tokens" (cut off) or "refusal".
/// </summary>
internal sealed record TextResponse(string Text, string Model, int InputTokens, int OutputTokens, string StopReason);

/// <summary>
/// A language model that answers text, behind our own interface so another provider can be plugged in
/// (docs/design.md, "Gemensamma regler"). Only the Worker has an implementation registered.
/// </summary>
internal interface ITextModel
{
    /// <summary>For example "anthropic".</summary>
    string Provider { get; }

    /// <summary>The model a job of this kind uses, so its price can be checked before calling.</summary>
    string ModelFor(string kind);

    Task<TextResponse> CompleteAsync(TextRequest request, CancellationToken cancellationToken);
}

/// <summary>A call to a provider failed. <see cref="Retryable"/> says whether a new attempt can help.</summary>
internal sealed class AiProviderException(string message, bool retryable, Exception? inner = null) : Exception(message, inner)
{
    public bool Retryable { get; } = retryable;
}
