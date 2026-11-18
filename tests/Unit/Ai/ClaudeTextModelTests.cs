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
        Assert.Equal("claude-haiku-5-5", new ClaudeTextModel(Options.Create(new AnthropicOptions())).ModelFor("connection-test"));
    }

    [Fact]
    public void A_kind_with_its_own_model_uses_it_and_others_use_the_default()
    {
        var options = new AnthropicOptions { Models = new() { ["photo-interpretation"] = "claude-sonnet-5-5" } };
        var model = new ClaudeTextModel(Options.Create(options));

        Assert.Equal("claude-sonnet-5-5", model.ModelFor("photo-interpretation"));
        Assert.Equal("claude-haiku-5-5", model.ModelFor("connection-test"));
        Assert.Equal(["claude-sonnet-5-5", "claude-haiku-5-5"], options.AllModels);
    }
}
