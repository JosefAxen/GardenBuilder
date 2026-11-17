using GardenBuilder.Modules.Ai.Providers;
using Microsoft.Extensions.Options;

namespace GardenBuilder.UnitTests.Ai;

public sealed class ClaudeTextModelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_key_throws_the_swedish_message_without_calling_out(string? key)
    {
        var model = new ClaudeTextModel(Options.Create(new AnthropicOptions { ApiKey = key }));

        var error = await Assert.ThrowsAsync<AiProviderException>(
            () => model.CompleteAsync(new TextRequest(null, "Hej", 16), TestContext.Current.CancellationToken));

        Assert.Equal("Ingen API-nyckel för Claude är inställd.", error.Message);
        Assert.False(error.Retryable);
    }

    [Fact]
    public void The_default_model_is_claude_haiku_5_5()
    {
        // Josef's choice 2026-11-17: Haiku until each step's model is chosen by measuring (sub-project 8).
        Assert.Equal("claude-haiku-5-5", new ClaudeTextModel(Options.Create(new AnthropicOptions())).Model);
    }
}
