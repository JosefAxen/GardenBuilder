using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.Modules.Ai.Providers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static GardenBuilder.IntegrationTests.PhotoTestHelpers;

namespace GardenBuilder.IntegrationTests;

/// <summary>The photo interpretation job, from "Tolka foton" to the proposal, with a fake model instead of Claude.</summary>
public sealed class PhotoInterpretationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly FakeModel _model = new();
    private WebApplicationFactory<Program> _app = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddAiJobRunner();
            s.AddSingleton<ITextModel>(_model);
        }));
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await db.Set<AiJob>().ExecuteDeleteAsync(Ct);
        await db.Set<AiCall>().ExecuteDeleteAsync(Ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ai.settings", Ct);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static Uri Interpretations(Guid gardenId, string rest = "") =>
        new($"/api/gardens/{gardenId}/photo-interpretations{rest}", UriKind.Relative);

    /// <summary>Claude's answer for one photo taken from the South camera: a lawn over the whole southern half and a yellow house.</summary>
    private static string LawnAnswer(double x0 = -10, double x1 = 10) => JsonSerializer.Serialize(new
    {
        summary = "En gräsmatta framför ett gult hus.",
        house = new { visible = true, wallColor = "yellow", roofForm = "gable", roofColor = "black", confidence = 0.8 },
        elements = new object[]
        {
            new { type = "lawn", label = "Gräsmatta", shape = "area", points = new[] { new[] { x0, 1.0 }, new[] { x1, 1.0 }, new[] { x1, 10.0 }, new[] { x0, 10.0 } }, widthM = (double?)null, heightM = (double?)null, confidence = 0.9 },
        },
        unsure = Array.Empty<string>(),
    });

    private static async Task<HttpResponseMessage> StartAsync(HttpClient client, Guid gardenId, Guid designId) =>
        await client.PostAsJsonAsync(Interpretations(gardenId), new { designId }, Ct);

    private static async Task<Guid> StartOkAsync(HttpClient client, Guid gardenId, Guid designId)
    {
        using var started = await StartAsync(client, gardenId, designId);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        return (await started.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task RunAsync()
    {
        using var scope = _app.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<JobRunner>().RunNextAsync(Ct));
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, Guid gardenId, Guid jobId) =>
        await client.GetFromJsonAsync<JsonElement>(Interpretations(gardenId, $"/{jobId}"), Ct);

    private async Task<List<AiCall>> CallsAsync()
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AiDbContext>().Set<AiCall>().AsNoTracking().ToListAsync(Ct);
    }

    [Fact]
    public async Task Two_photos_give_one_merged_proposal_and_two_logged_calls()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await UploadFromSouthAsync(client, gardenId);
        await UploadFromSouthAsync(client, gardenId);
        _model.Answers.Enqueue(LawnAnswer(-10, 2));
        _model.Answers.Enqueue(LawnAnswer(-2, 10));

        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        var job = await ReadAsync(client, gardenId, jobId);
        Assert.Equal("succeeded", job.GetProperty("status").GetString());
        var result = job.GetProperty("result");
        var element = Assert.Single(result.GetProperty("elements").EnumerateArray());
        Assert.Equal("Gräsmatta", element.GetProperty("label").GetString());
        Assert.Equal("surface", element.GetProperty("element").GetProperty("kind").GetString());
        Assert.Equal("#E3B23C", result.GetProperty("house").GetProperty("wallColor").GetString());
        Assert.Equal(0, result.GetProperty("skippedPhotos").GetInt32());
        var calls = await CallsAsync();
        Assert.Equal(2, calls.Count);
        Assert.All(calls, c => Assert.Equal("photo-interpretation@1", c.PromptVersion));
        Assert.All(_model.Requests, r =>
        {
            Assert.Single(r.Images!);
            Assert.NotNull(r.OutputSchema);
        });
    }

    [Fact]
    public async Task Photo_interpretation_uses_the_model_for_its_kind()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await UploadFromSouthAsync(client, gardenId);
        _model.Answers.Enqueue(LawnAnswer());

        await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        Assert.Equal("claude-sonnet-5-5", Assert.Single(_model.Requests).Model);
        Assert.Equal("claude-sonnet-5-5", Assert.Single(await CallsAsync()).Model);
    }

    [Fact]
    public async Task Unparseable_answer_skips_that_photo()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await UploadFromSouthAsync(client, gardenId);
        await UploadFromSouthAsync(client, gardenId);
        _model.Answers.Enqueue("Det här är inte JSON.");
        _model.Answers.Enqueue(LawnAnswer());

        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        var job = await ReadAsync(client, gardenId, jobId);
        Assert.Equal("succeeded", job.GetProperty("status").GetString());
        Assert.Equal(1, job.GetProperty("result").GetProperty("skippedPhotos").GetInt32());
        Assert.Single(job.GetProperty("result").GetProperty("elements").EnumerateArray());
    }

    [Fact]
    public async Task All_photos_unparseable_fails_with_swedish_reason()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await UploadFromSouthAsync(client, gardenId);
        _model.Answers.Enqueue("{\"summary\": ");

        var jobId = await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        var job = await ReadAsync(client, gardenId, jobId);
        Assert.Equal("failed", job.GetProperty("status").GetString());
        Assert.Equal("Vi kunde inte tolka fotona.", job.GetProperty("failure").GetString());
    }

    [Fact]
    public async Task Deleted_photo_is_skipped()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await UploadFromSouthAsync(client, gardenId);
        var gone = await UploadFromSouthAsync(client, gardenId);
        _model.Answers.Enqueue(LawnAnswer());
        var jobId = await StartOkAsync(client, gardenId, designId);

        using var deleted = await client.DeleteAsync(Photos(gardenId, $"/{gone}"), Ct);
        await RunAsync();

        Assert.Single(_model.Requests);
        Assert.Equal(1, (await ReadAsync(client, gardenId, jobId)).GetProperty("result").GetProperty("skippedPhotos").GetInt32());
    }

    [Fact]
    public async Task Start_without_camera_is_409_camera_missing()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await UploadFromSouthAsync(client, gardenId);
        await UploadOkAsync(client, gardenId);

        using var response = await StartAsync(client, gardenId, designId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("camera-missing", await CodeAsync(response));
    }

    [Fact]
    public async Task Start_without_photos_is_409_no_photos()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);

        using var response = await StartAsync(client, gardenId, designId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("no-photos", await CodeAsync(response));
    }

    [Fact]
    public async Task Fourth_interpretation_is_409_quota_reached()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await UploadFromSouthAsync(client, gardenId);
        for (var i = 0; i < 3; i++)
        {
            await StartOkAsync(client, gardenId, designId);
        }

        using var fourth = await StartAsync(client, gardenId, designId);

        Assert.Equal(HttpStatusCode.Conflict, fourth.StatusCode);
        Assert.Equal("quota-reached", await CodeAsync(fourth));
    }

    [Fact]
    public async Task Another_customers_garden_design_and_job_are_404()
    {
        var (owner, gardenId, designId) = await CustomerAsync(_app);
        await UploadFromSouthAsync(owner, gardenId);
        var jobId = await StartOkAsync(owner, gardenId, designId);
        var (other, otherGarden, _) = await CustomerAsync(_app);
        using var anonymous = IdentityTestHelpers.HttpsClient(_app);

        using var read = await other.GetAsync(Interpretations(gardenId, $"/{jobId}"), Ct);
        using var readThroughOwnGarden = await other.GetAsync(Interpretations(otherGarden, $"/{jobId}"), Ct);
        using var start = await StartAsync(other, gardenId, designId);
        using var startWithOthersDesign = await StartAsync(other, otherGarden, designId);
        using var anonymousRead = await anonymous.GetAsync(Interpretations(gardenId, $"/{jobId}"), Ct);

        Assert.All([read, readThroughOwnGarden, start, startWithOthersDesign, anonymousRead], r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    [Fact]
    public async Task Result_never_changes_the_design()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await UploadFromSouthAsync(client, gardenId);
        _model.Answers.Enqueue(LawnAnswer());

        await StartOkAsync(client, gardenId, designId);
        await RunAsync();

        var design = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/designs/{designId}", UriKind.Relative), Ct);
        Assert.Equal(1, design.GetProperty("revision").GetInt32());
        Assert.Equal(2, design.GetProperty("scene").GetProperty("elements").GetArrayLength());
    }

    /// <summary>Hands out the answers the test queued; photo interpretation runs on Sonnet, like the real settings.</summary>
    private sealed class FakeModel : ITextModel
    {
        public string Provider => "fake";

        public ConcurrentQueue<string> Answers { get; } = new();

        public ConcurrentQueue<TextRequest> Requests { get; } = new();

        public string ModelFor(string kind) => kind == AiJobKinds.PhotoInterpretation ? "claude-sonnet-5-5" : "claude-haiku-5-5";

        public Task<TextResponse> CompleteAsync(TextRequest request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            var text = Answers.TryDequeue(out var answer) ? answer : "{}";
            return Task.FromResult(new TextResponse(text, request.Model, 1500, 800, "end_turn"));
        }
    }
}
