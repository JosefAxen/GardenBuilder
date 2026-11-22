using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using GardenBuilder.Contracts.Ai;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.DreamImages;
using GardenBuilder.Modules.Ai.Features.Queue;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.Modules.Ai.Providers;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Infrastructure;
using GardenBuilder.SharedKernel;
using ImageMagick;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static GardenBuilder.IntegrationTests.PhotoTestHelpers;

namespace GardenBuilder.IntegrationTests;

/// <summary>The dream-image job, from an uploaded render to a stored image, with a recording fake image model.</summary>
public sealed class DreamImageJobTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly FakeImageModel _model = new();
    private WebApplicationFactory<Program> _app = null!;
    private Guid _thuja;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddAiJobRunner();
            s.AddSingleton<IImageModel>(_model);
            s.AddSingleton<ITextModel>(new UnusedTextModel());
        }));
        using var scope = _app.Services.CreateScope();
        var ai = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await ai.Set<AiJob>().ExecuteDeleteAsync(Ct);
        await ai.Set<AiCall>().ExecuteDeleteAsync(Ct);
        await ai.Database.ExecuteSqlRawAsync("DELETE FROM ai.settings", Ct);

        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var thuja = Article.Create(new ArticleData(
            $"TU-{Ids.New():N}"[..20], "Tuja Smaragd 40–60 cm", "Tuja Smaragd", "Träd", ArticleUnit.Piece, Money.InSek(195m),
            StockStatus.InStock, TypeModel.ConiferColumn, 1, "#3f7a3a", null, HardinessZone: 5, Height: 0.5, Width: null,
            MatureHeight: 4, MatureWidth: 1.2, ClipHeight: null, GrowthCmPerYear: 20, Light.Sun, LaborHours: 0.3m));
        catalog.Add(thuja);
        await catalog.SaveChangesAsync(Ct);
        _thuja = thuja.Id;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    /// <summary>Stands south of the plot and looks north.</summary>
    private static readonly SceneView FromStreet = new(Ids.New(), "Från gatan", new Point3(10, -5, 1.7), new Point3(10, 10, 1), 50);

    /// <summary>Saves the design with three thujas and the view, as revision 2.</summary>
    private async Task SaveWithViewAsync(HttpClient client, Guid designId)
    {
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion,
        [
            new PlotElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer,
                [new Point2(0, 0), new Point2(20, 0), new Point2(20, 20), new Point2(0, 20)], Sloped: false),
            .. Enumerable.Range(0, 3).Select(i => (SceneElement)new PointElement(
                Ids.New(), ElementStatus.New, MeasurementSource.Estimated, _thuja, new Point2(4 + i, 4), 0)),
        ],
        [FromStreet]);
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/designs/{designId}/working-copy", UriKind.Relative))
        {
            Content = new StringContent(SceneJson.Serialize(scene), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        using var saved = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    /// <summary>Queues the job and uploads its render the way the endpoint will.</summary>
    private async Task<(Guid JobId, Guid GardenId)> StartAsync(Guid gardenId, Guid designId, Guid viewId, bool sendPhotos)
    {
        using var scope = _app.Services.CreateScope();
        var input = JsonSerializer.SerializeToElement(new DreamImageInput(designId, viewId, 2, sendPhotos), DreamImageHandler.Json);
        var jobId = await scope.ServiceProvider.GetRequiredService<IAiJobs>().EnqueueAsync(AiJobKinds.DreamImage, gardenId, null, input, Ct);
        var store = scope.ServiceProvider.GetRequiredService<DreamImageStore>();
        await store.UploadAsync(DreamImageStore.RenderBlob(gardenId, jobId), Jpeg(1536, 1024), Ct);
        return (jobId, gardenId);
    }

    private async Task<AiJob> RunAsync(Guid jobId)
    {
        using var scope = _app.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<JobRunner>().RunNextAsync(Ct));
        return (await scope.ServiceProvider.GetRequiredService<JobQueue>().FindAsync(jobId, Ct))!;
    }

    private async Task<byte[]?> ReadBlobAsync(string blobName)
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DreamImageStore>().ReadAsync(blobName, Ct);
    }

    [Fact]
    public async Task Two_posed_photos_are_sent_and_the_image_is_stored()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await SaveWithViewAsync(client, designId);
        await UploadFromSouthAsync(client, gardenId);
        await UploadFromSouthAsync(client, gardenId);
        await UploadFromSouthAsync(client, gardenId);

        var (jobId, _) = await StartAsync(gardenId, designId, FromStreet.Id, sendPhotos: true);
        var job = await RunAsync(jobId);

        Assert.Equal(AiJobStatus.Succeeded, job.Status);
        var request = Assert.Single(_model.Requests);
        Assert.Equal(2, request.Photos.Count);
        Assert.Contains("Tuja Smaragd 40–60 cm × 3", request.Prompt, StringComparison.Ordinal);
        Assert.Contains(DreamPrompt.LaterImages, request.Prompt, StringComparison.Ordinal);
        Assert.NotEmpty(request.Reference);
        Assert.NotNull(await ReadBlobAsync(DreamImageStore.ImageBlob(gardenId, jobId)));
        var result = job.Result!.Value;
        Assert.Equal("Från gatan", result.GetProperty("viewName").GetString());
        Assert.Equal(2, result.GetProperty("revision").GetInt32());
        Assert.True(result.GetProperty("test").GetBoolean());
    }

    [Fact]
    public async Task Unticking_the_box_sends_no_photos()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await SaveWithViewAsync(client, designId);
        await UploadFromSouthAsync(client, gardenId);

        var (jobId, _) = await StartAsync(gardenId, designId, FromStreet.Id, sendPhotos: false);

        Assert.Equal(AiJobStatus.Succeeded, (await RunAsync(jobId)).Status);
        var request = Assert.Single(_model.Requests);
        Assert.Empty(request.Photos);
        Assert.DoesNotContain(DreamPrompt.LaterImages, request.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_garden_without_photos_sends_none()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await SaveWithViewAsync(client, designId);

        var (jobId, _) = await StartAsync(gardenId, designId, FromStreet.Id, sendPhotos: true);

        Assert.Equal(AiJobStatus.Succeeded, (await RunAsync(jobId)).Status);
        Assert.Empty(Assert.Single(_model.Requests).Photos);
    }

    [Fact]
    public async Task An_answer_that_fails_the_check_fails_the_job()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await SaveWithViewAsync(client, designId);
        _model.Answer = Jpeg(1000, 1000);

        var (jobId, _) = await StartAsync(gardenId, designId, FromStreet.Id, sendPhotos: false);
        var job = await RunAsync(jobId);

        Assert.Equal(AiJobStatus.Failed, job.Status);
        Assert.Equal("Det gick inte att skapa en drömbild just nu.", job.Failure);
        Assert.Null(await ReadBlobAsync(DreamImageStore.ImageBlob(gardenId, jobId)));
    }

    [Fact]
    public async Task A_removed_view_still_runs_from_the_uploaded_render()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        await SaveWithViewAsync(client, designId);

        var (jobId, _) = await StartAsync(gardenId, designId, Ids.New(), sendPhotos: true);
        var job = await RunAsync(jobId);

        Assert.Equal(AiJobStatus.Succeeded, job.Status);
        Assert.Equal("Vy", job.Result!.Value.GetProperty("viewName").GetString());
    }

    /// <summary>The runner needs a text model; a dream image never calls it.</summary>
    private sealed class UnusedTextModel : ITextModel
    {
        public string Provider => "unused";

        public string ModelFor(string kind) => "claude-haiku-5-5";

        public Task<TextResponse> CompleteAsync(TextRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A dream image makes no text call.");
    }

    /// <summary>Records each request and answers with a landscape JPEG unless the test sets another answer.</summary>
    private sealed class FakeImageModel : IImageModel
    {
        public string Provider => "fake";

        public string ModelFor(string kind) => TestImageModel.ModelName;

        public byte[] Answer { get; set; } = Jpeg(1536, 1024);

        public ConcurrentQueue<ImageRequest> Requests { get; } = new();

        public Task<ImageResponse> EditAsync(ImageRequest request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            return Task.FromResult(new ImageResponse(Answer, request.Model));
        }
    }
}
