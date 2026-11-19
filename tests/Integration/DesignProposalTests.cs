using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.Modules.Ai.Providers;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using GardenBuilder.Modules.Design.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static GardenBuilder.IntegrationTests.PhotoTestHelpers;

namespace GardenBuilder.IntegrationTests;

/// <summary>The design proposal job, from "Ge mig förslag" to choosing one, with a fake model instead of Claude.</summary>
public sealed class DesignProposalTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly FakeModel _model = new();
    private WebApplicationFactory<Program> _app = null!;
    private Guid _spiraea;
    private Guid _goldShrub;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddAiJobRunner();
            s.AddSingleton<ITextModel>(_model);
        }));
        using var scope = _app.Services.CreateScope();
        var ai = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await ai.Set<AiJob>().ExecuteDeleteAsync(Ct);
        await ai.Set<AiCall>().ExecuteDeleteAsync(Ct);
        await ai.Database.ExecuteSqlRawAsync("DELETE FROM ai.settings", Ct);

        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var spiraea = Article.Create(Shrub($"SP-{Ids.New():N}"[..20], "Spirea", 249m));
        var gold = Article.Create(Shrub($"GU-{Ids.New():N}"[..20], "Guldbuske", 40_000m));
        catalog.AddRange(spiraea, gold);
        await catalog.SaveChangesAsync(Ct);
        (_spiraea, _goldShrub) = (spiraea.Id, gold.Id);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static ArticleData Shrub(string number, string name, decimal price) => new(
        number, name, null, "Buskar", ArticleUnit.Piece, Money.InSek(price), StockStatus.InStock, TypeModel.ShrubRound, 1, "#5a8f4a", null,
        HardinessZone: 6, Height: 0.6, Width: null, MatureHeight: 1.2, MatureWidth: 1.0, ClipHeight: null, GrowthCmPerYear: 20,
        Light.Sun | Light.PartShade | Light.Shade, LaborHours: 0.3m);

    private static Uri Proposals(Guid gardenId, string rest = "") => new($"/api/gardens/{gardenId}/design-proposals{rest}", UriKind.Relative);

    private static object Wishes(Guid designId, string budget = "50k-150k", string style = "natural", int zone = 3) =>
        new { designId, style, budget, care = "medium", zone, note = "Plats för grill." };

    /// <summary>Claude's answer: two shrubs of <paramref name="article"/> in <paramref name="zone"/>.</summary>
    private static string Answer(Guid article, string zone = "middle", int count = 2) => JsonSerializer.Serialize(new
    {
        name = "Grön och lugn",
        motivation = "Buskar som ramar in trädgården.",
        blocks = new[]
        {
            new { type = "shrubs", zone, area = (double?)null, length = (double?)null, depth = (double?)null, width = (double?)null, side = (string?)null, from = (string?)null, to = (string?)null, articles = new[] { article.ToString() }, count = (int?)count },
        },
    });

    private static async Task<HttpResponseMessage> StartAsync(HttpClient client, Guid gardenId, object body) =>
        await client.PostAsJsonAsync(Proposals(gardenId), body, Ct);

    private static async Task<Guid> StartOkAsync(HttpClient client, Guid gardenId, Guid designId, string budget = "50k-150k")
    {
        using var started = await StartAsync(client, gardenId, Wishes(designId, budget));
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        return (await started.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task RunAsync()
    {
        using var scope = _app.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<JobRunner>().RunNextAsync(Ct));
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, Guid gardenId, Guid jobId) =>
        await client.GetFromJsonAsync<JsonElement>(Proposals(gardenId, $"/{jobId}"), Ct);

    private static JsonElement[] ProposalsOf(JsonElement job) => [.. job.GetProperty("result").GetProperty("proposals").EnumerateArray()];

    [Fact]
    public async Task Two_valid_proposals_are_stored_with_price_and_the_design_is_unchanged()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        _model.Answers.Enqueue(Answer(_spiraea));
        _model.Answers.Enqueue(Answer(_spiraea));

        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        var job = await ReadAsync(client, gardenId, jobId);
        Assert.Equal("succeeded", job.GetProperty("status").GetString());
        var proposals = ProposalsOf(job);
        Assert.Equal(2, proposals.Length);
        Assert.Equal("Grön och lugn", proposals[0].GetProperty("name").GetString());
        Assert.True(proposals[0].GetProperty("low").GetDecimal() > 0);
        Assert.False(proposals[0].GetProperty("overBudget").GetBoolean());
        var scene = proposals[0].GetProperty("scene");
        Assert.Equal(2, scene.GetProperty("elements").EnumerateArray().Count(e => e.GetProperty("status").GetString() == "new"));
        Assert.Equal(2, _model.Requests.Count);
        var design = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/designs/{designId}", UriKind.Relative), Ct);
        Assert.Equal(1, design.GetProperty("revision").GetInt32());
        // The round remembers which saved garden it started from, so the editor can tell when it is out of date.
        Assert.Equal(1, job.GetProperty("result").GetProperty("revision").GetInt32());
    }

    [Fact]
    public async Task An_invalid_proposal_is_retried_with_the_errors_and_passes()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        _model.Answers.Enqueue(Answer(_spiraea, zone: "on-the-roof"));
        _model.Answers.Enqueue(Answer(_spiraea));
        _model.Answers.Enqueue(Answer(_spiraea));

        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        Assert.Equal(2, ProposalsOf(await ReadAsync(client, gardenId, jobId)).Length);
        var requests = _model.Requests.ToArray();
        Assert.Equal(3, requests.Length);
        Assert.Contains("Zonen \"on-the-roof\" finns inte.", requests[1].Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Three_invalid_answers_drop_that_proposal_only()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        for (var i = 0; i < 3; i++)
        {
            _model.Answers.Enqueue(Answer(_spiraea, zone: "on-the-roof"));
        }

        _model.Answers.Enqueue(Answer(_spiraea));

        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        var proposals = ProposalsOf(await ReadAsync(client, gardenId, jobId));
        Assert.Single(proposals);
        Assert.Equal(4, _model.Requests.Count);
    }

    [Fact]
    public async Task A_provider_error_on_the_second_proposal_keeps_the_first()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        _model.Answers.Enqueue(Answer(_spiraea));
        _model.Answers.Enqueue(FakeModel.Error);

        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        var job = await ReadAsync(client, gardenId, jobId);
        Assert.Equal("succeeded", job.GetProperty("status").GetString());
        Assert.Single(ProposalsOf(job));
    }

    [Fact]
    public async Task Both_proposals_failing_fails_the_job_in_swedish()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        for (var i = 0; i < 6; i++)
        {
            _model.Answers.Enqueue(Answer(_spiraea, zone: "on-the-roof"));
        }

        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        var job = await ReadAsync(client, gardenId, jobId);
        Assert.Equal("failed", job.GetProperty("status").GetString());
        Assert.Equal("Vi kunde inte ta fram förslag just nu.", job.GetProperty("failure").GetString());
    }

    [Fact]
    public async Task Over_budget_after_retries_is_kept_and_flagged()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        for (var i = 0; i < 6; i++)
        {
            _model.Answers.Enqueue(Answer(_goldShrub, count: 3));
        }

        var jobId = await StartOkAsync(client, gardenId, designId, budget: "under-50k");
        await RunAsync();

        var proposals = ProposalsOf(await ReadAsync(client, gardenId, jobId));
        Assert.Equal(2, proposals.Length);
        Assert.All(proposals, p => Assert.True(p.GetProperty("overBudget").GetBoolean()));
        Assert.Contains("budgeten är högst 50 000 kr", _model.Requests.ToArray()[1].Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_wishes_are_400_with_swedish_problems()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);

        using var response = await StartAsync(client, gardenId, Wishes(designId, style: "gothic", zone: 9));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("invalid-wishes", body.GetProperty("code").GetString());
        var problems = body.GetProperty("problems").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains("Välj en stil i listan.", problems);
        Assert.Contains("Växtzonen ska vara mellan 1 och 8.", problems);
    }

    [Fact]
    public async Task Sixth_round_is_409_quota_reached()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        for (var i = 0; i < 5; i++)
        {
            await StartOkAsync(client, gardenId, designId);
        }

        using var sixth = await StartAsync(client, gardenId, Wishes(designId));

        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
        Assert.Equal("quota-reached", await CodeAsync(sixth));
    }

    [Fact]
    public async Task Latest_is_fetched_again_and_another_customer_gets_404()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        _model.Answers.Enqueue(Answer(_spiraea));
        _model.Answers.Enqueue(Answer(_spiraea));
        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();
        var (other, otherGarden, otherDesign) = await CustomerAsync(_app);

        var latest = await client.GetFromJsonAsync<JsonElement>(Proposals(gardenId, "/latest"), Ct);
        using var othersLatest = await other.GetAsync(Proposals(gardenId, "/latest"), Ct);
        using var othersJob = await other.GetAsync(Proposals(gardenId, $"/{jobId}"), Ct);
        using var startOnOthersGarden = await StartAsync(other, gardenId, Wishes(designId));
        using var startWithOthersDesign = await StartAsync(other, otherGarden, Wishes(designId));

        Assert.Equal(jobId, latest.GetProperty("id").GetGuid());
        Assert.All([othersLatest, othersJob, startOnOthersGarden, startWithOthersDesign], r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        _ = otherDesign;
    }

    [Fact]
    public async Task Choosing_freezes_a_version()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        _model.Answers.Enqueue(Answer(_spiraea));
        _model.Answers.Enqueue(Answer(_spiraea));
        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        using var chosen = await client.PostAsJsonAsync(Proposals(gardenId, $"/{jobId}/chosen"), new { designId, index = 1 }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, chosen.StatusCode);
        using var scope = _app.Services.CreateScope();
        var versions = await scope.ServiceProvider.GetRequiredService<DesignDbContext>().DesignVersions
            .Where(v => v.DesignId == designId).ToListAsync(Ct);
        Assert.Equal(GardenBuilder.Contracts.Designs.DesignVersionReason.AiProposal, Assert.Single(versions).Reason);
    }

    [Fact]
    public async Task Choosing_someone_elses_or_a_missing_index_is_refused()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        _model.Answers.Enqueue(Answer(_spiraea));
        _model.Answers.Enqueue(Answer(_spiraea));
        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();
        var (other, _, _) = await CustomerAsync(_app);

        using var othersChoice = await other.PostAsJsonAsync(Proposals(gardenId, $"/{jobId}/chosen"), new { designId, index = 0 }, Ct);
        using var badIndex = await client.PostAsJsonAsync(Proposals(gardenId, $"/{jobId}/chosen"), new { designId, index = 5 }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, othersChoice.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badIndex.StatusCode);
    }

    [Fact]
    public async Task Proposal_uses_the_model_for_its_kind()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        _model.Answers.Enqueue(Answer(_spiraea));
        _model.Answers.Enqueue(Answer(_spiraea));

        await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        Assert.All(_model.Requests, r => Assert.Equal("claude-sonnet-5-5", r.Model));
        Assert.All(_model.Requests, r => Assert.NotNull(r.OutputSchema));
    }

    /// <summary>Hands out the answers the test queued; design proposals run on Sonnet, like the real settings.</summary>
    private sealed class FakeModel : ITextModel
    {
        public string Provider => "fake";

        /// <summary>Queue this instead of an answer to make that call fail as when Claude does not answer.</summary>
        public const string Error = "!error";

        public ConcurrentQueue<string> Answers { get; } = new();

        public ConcurrentQueue<TextRequest> Requests { get; } = new();

        public string ModelFor(string kind) => kind == AiJobKinds.DesignProposal ? "claude-sonnet-5-5" : "claude-haiku-5-5";

        public Task<TextResponse> CompleteAsync(TextRequest request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            var text = Answers.TryDequeue(out var answer) ? answer : "{}";
            return text == Error
                ? throw new AiProviderException("Claude svarade inte.", retryable: false)
                : Task.FromResult(new TextResponse(text, request.Model, 3000, 800, "end_turn"));
        }
    }
}
