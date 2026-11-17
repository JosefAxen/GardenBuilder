using System.Collections.Concurrent;
using System.Text.Json;
using GardenBuilder.Contracts.Ai;
using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Costs;
using GardenBuilder.Modules.Ai.Features.Queue;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Features.Settings;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.Modules.Ai.Providers;
using GardenBuilder.SharedKernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>Running AI jobs: the guards, the cost log and the connection test, with a fake model instead of Claude.</summary>
public sealed class AiJobRunnerTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly FakeTextModel _model = new();
    private WebApplicationFactory<Program> _app = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddAiJobRunner();
            s.AddSingleton<ITextModel>(_model);
            s.AddScoped<IAiJobHandler, StepHandler>();
        }));
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await db.Set<AiJob>().ExecuteDeleteAsync(Ct);
        await db.Set<AiCall>().ExecuteDeleteAsync(Ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ai.settings", Ct);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async Task<T> With<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var scope = _app.Services.CreateScope();
        return await work(scope.ServiceProvider);
    }

    private async Task<Guid> EnqueueAsync(string kind, Guid? gardenId = null) =>
        await With(s => s.GetRequiredService<IAiJobs>().EnqueueAsync(kind, gardenId, null, JsonSerializer.SerializeToElement(new { }), Ct));

    private Task<bool> RunAsync() => With(s => s.GetRequiredService<JobRunner>().RunNextAsync(Ct));

    private Task<AiJob?> JobAsync(Guid id) => With(s => s.GetRequiredService<JobQueue>().FindAsync(id, Ct));

    private Task<List<AiCall>> CallsAsync() => With(s => s.GetRequiredService<AiDbContext>().Set<AiCall>().AsNoTracking().ToListAsync(Ct));

    private async Task SaveSettingsAsync(AiSettings settings)
    {
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AiSettingsStore>().SaveAsync(settings, Ct);
    }

    private async Task SpendAsync(decimal kr)
    {
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CostLog>().AddAsync(
            AiCall.Create(null, "earlier@1", "anthropic", "claude-opus-5-5", DateTimeOffset.UtcNow, 10, 1, 1, Money.InSek(kr), "ok", null), Ct);
    }

    private static TextResponse Answer(string text, string stopReason = "end_turn") =>
        new(text, "claude-opus-5-5", 120, 30, stopReason);

    [Fact]
    public async Task An_empty_queue_runs_nothing()
    {
        Assert.False(await RunAsync());
        Assert.Empty(_model.Requests);
    }

    [Fact]
    public async Task A_connection_test_stores_the_answer_and_logs_one_call()
    {
        _model.Responses.Enqueue(Answer("En trädgård är en plats där man odlar växter."));
        var id = await EnqueueAsync(AiJobKinds.ConnectionTest);

        Assert.True(await RunAsync());

        var job = await JobAsync(id);
        Assert.Equal(AiJobStatus.Succeeded, job?.Status);
        Assert.Equal("En trädgård är en plats där man odlar växter.", job?.Result?.GetProperty("answer").GetString());
        Assert.Equal("claude-opus-5-5", job?.Result?.GetProperty("model").GetString());
        Assert.Equal("Svara med en mening på svenska: vad är en trädgård?", Assert.Single(_model.Requests).Prompt);
        var call = Assert.Single(await CallsAsync());
        Assert.Equal("connection-test@1", call.PromptVersion);
        Assert.Equal("ok", call.Outcome);
        Assert.Equal(id, call.JobId);
        // 120 in and 30 out: (480 + 600) / 1 000 000 USD at 10.50 = 0.01 kr.
        Assert.Equal(Money.InSek(0.01m), call.Cost);
        Assert.Equal(0.01m, job?.Result?.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task A_missing_key_fails_at_once_without_a_retry()
    {
        _model.Errors.Enqueue(new AiProviderException("Ingen API-nyckel för Claude är inställd.", retryable: false));
        var id = await EnqueueAsync(AiJobKinds.ConnectionTest);

        await RunAsync();

        var job = await JobAsync(id);
        Assert.Equal(AiJobStatus.Failed, job?.Status);
        Assert.Equal("Ingen API-nyckel för Claude är inställd.", job?.Failure);
        Assert.Equal(1, job?.Attempts);
    }

    [Fact]
    public async Task A_cut_off_answer_is_logged_and_retried()
    {
        _model.Responses.Enqueue(Answer("En trädgård är", "max_tokens"));
        var id = await EnqueueAsync(AiJobKinds.ConnectionTest);

        await RunAsync();

        Assert.Equal(AiJobStatus.Queued, (await JobAsync(id))?.Status);
        var call = Assert.Single(await CallsAsync());
        Assert.Equal("cut-off", call.Outcome);
        Assert.Equal(Money.InSek(0.01m), call.Cost);
    }

    [Fact]
    public async Task A_refused_answer_is_logged_and_retried()
    {
        _model.Responses.Enqueue(Answer(string.Empty, "refusal"));
        var id = await EnqueueAsync(AiJobKinds.ConnectionTest);

        await RunAsync();

        Assert.Equal(AiJobStatus.Queued, (await JobAsync(id))?.Status);
        Assert.Equal("refused", Assert.Single(await CallsAsync()).Outcome);
    }

    [Fact]
    public async Task A_provider_error_is_logged_and_retried()
    {
        _model.Errors.Enqueue(new AiProviderException("Claude svarade inte.", retryable: true));
        var id = await EnqueueAsync(AiJobKinds.ConnectionTest);

        await RunAsync();

        Assert.Equal(AiJobStatus.Queued, (await JobAsync(id))?.Status);
        var call = Assert.Single(await CallsAsync());
        Assert.Equal("error", call.Outcome);
        Assert.Equal(Money.InSek(0m), call.Cost);
    }

    [Fact]
    public async Task The_reached_budget_fails_waiting_jobs_without_a_call()
    {
        var id = await EnqueueAsync(AiJobKinds.ConnectionTest);
        await SpendAsync(500m);

        await RunAsync();

        var job = await JobAsync(id);
        Assert.Equal(AiJobStatus.Failed, job?.Status);
        Assert.Equal("AI-stegen är pausade för i dag.", job?.Failure);
        Assert.Empty(_model.Requests);
    }

    [Fact]
    public async Task A_step_that_is_off_fails_its_waiting_jobs()
    {
        var photo = await EnqueueAsync(AiJobKinds.PhotoInterpretation, Ids.New());
        var test = await EnqueueAsync(AiJobKinds.ConnectionTest);
        await SaveSettingsAsync(AiSettings.Defaults with
        {
            Enabled = AiJobKinds.Steps.ToDictionary(step => step, _ => false),
        });
        _model.Responses.Enqueue(Answer("Ett svar."));

        await RunAsync();
        await RunAsync();

        Assert.Equal("Det här AI-steget är avstängt.", (await JobAsync(photo))?.Failure);
        Assert.Equal(AiJobStatus.Succeeded, (await JobAsync(test))?.Status);
    }

    [Fact]
    public async Task A_model_without_a_price_is_never_called()
    {
        _model.Model = "claude-x";
        var id = await EnqueueAsync(AiJobKinds.ConnectionTest);

        await RunAsync();

        Assert.Equal("Modellen claude-x saknar pris i inställningarna.", (await JobAsync(id))?.Failure);
        Assert.Empty(_model.Requests);
    }

    [Fact]
    public async Task Enqueue_refuses_when_the_quota_is_reached()
    {
        var garden = Ids.New();
        for (var i = 0; i < 3; i++)
        {
            await EnqueueAsync(AiJobKinds.PhotoInterpretation, garden);
        }

        var refused = await Assert.ThrowsAsync<AiJobRefusedException>(() => EnqueueAsync(AiJobKinds.PhotoInterpretation, garden));
        Assert.Equal("quota-reached", refused.Code);
        // Another garden has its own quota.
        await EnqueueAsync(AiJobKinds.PhotoInterpretation, Ids.New());
    }

    [Fact]
    public async Task Failed_jobs_do_not_count_against_the_quota()
    {
        var garden = Ids.New();
        await SaveSettingsAsync(AiSettings.Defaults with
        {
            Quotas = new Dictionary<string, int>(AiSettings.Defaults.Quotas) { [AiJobKinds.PhotoInterpretation] = 1 },
        });
        _model.Errors.Enqueue(new AiProviderException("Ingen API-nyckel för Claude är inställd.", retryable: false));
        await EnqueueAsync(AiJobKinds.PhotoInterpretation, garden);
        await RunAsync();

        await EnqueueAsync(AiJobKinds.PhotoInterpretation, garden);
    }

    [Fact]
    public async Task Enqueue_refuses_when_the_budget_is_reached()
    {
        await SpendAsync(500m);

        var refused = await Assert.ThrowsAsync<AiJobRefusedException>(() => EnqueueAsync(AiJobKinds.ConnectionTest));
        Assert.Equal("budget-reached", refused.Code);
    }

    [Fact]
    public async Task Enqueue_refuses_a_step_that_is_off()
    {
        await SaveSettingsAsync(AiSettings.Defaults with
        {
            Enabled = new Dictionary<string, bool>(AiSettings.Defaults.Enabled) { [AiJobKinds.DreamImage] = false },
        });

        var refused = await Assert.ThrowsAsync<AiJobRefusedException>(() => EnqueueAsync(AiJobKinds.DreamImage, Ids.New()));
        Assert.Equal("step-off", refused.Code);
    }

    /// <summary>A model whose answers and errors the test hands out.</summary>
    private sealed class FakeTextModel : ITextModel
    {
        public string Provider => "fake";

        public string Model { get; set; } = "claude-opus-5-5";

        public ConcurrentQueue<TextResponse> Responses { get; } = new();

        public ConcurrentQueue<AiProviderException> Errors { get; } = new();

        public ConcurrentQueue<TextRequest> Requests { get; } = new();

        public Task<TextResponse> CompleteAsync(TextRequest request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            if (Errors.TryDequeue(out var error))
            {
                throw error;
            }

            return Task.FromResult(Responses.TryDequeue(out var response) ? response : Answer("Ett svar."));
        }
    }

    /// <summary>Stands in for the photo interpretation of sub-project 8: one text call.</summary>
    private sealed class StepHandler : IAiJobHandler
    {
        public string Kind => AiJobKinds.PhotoInterpretation;

        public async Task<JsonElement> RunAsync(AiJob job, IAiCalls calls, CancellationToken cancellationToken)
        {
            var result = await calls.TextAsync("photo-test@1", new TextRequest(null, "Beskriv fotot.", 64), cancellationToken);
            return JsonSerializer.SerializeToElement(new { answer = result.Response.Text });
        }
    }
}
