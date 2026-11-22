using System.Buffers.Binary;
using System.IO.Hashing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GardenBuilder.Contracts.Ai;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.DreamImages;
using GardenBuilder.Modules.Ai.Features.Run;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.Modules.Ai.Providers;
using GardenBuilder.SharedKernel;
using ImageMagick;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static GardenBuilder.IntegrationTests.PhotoTestHelpers;

namespace GardenBuilder.IntegrationTests;

/// <summary>The dream-image endpoints: upload, gallery, one image and removing a failed one.</summary>
public sealed class DreamImageEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly SwitchableImageModel _model = new();
    private WebApplicationFactory<Program> _app = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Guid ViewId = Ids.New();

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
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static Uri Dreams(Guid gardenId, string rest = "") => new($"/api/gardens/{gardenId}/dream-images{rest}", UriKind.Relative);

    /// <summary>A customer whose design has the view "Från gatan", saved as revision 2.</summary>
    private async Task<(HttpClient Client, Guid GardenId, Guid DesignId)> CustomerWithViewAsync()
    {
        var (client, gardenId, designId) = await CustomerAsync(_app);
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion,
        [
            new PlotElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer,
                [new Point2(0, 0), new Point2(20, 0), new Point2(20, 20), new Point2(0, 20)], Sloped: false),
        ],
        [new SceneView(ViewId, "Från gatan", new Point3(10, -5, 1.7), new Point3(10, 10, 1), 50)]);
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/designs/{designId}/working-copy", UriKind.Relative))
        {
            Content = new StringContent(SceneJson.Serialize(scene), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        using var saved = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        return (client, gardenId, designId);
    }

    private static byte[] Png(uint width = 1536, uint height = 1024)
    {
        using var image = new MagickImage(MagickColors.SkyBlue, width, height);
        return image.ToByteArray(MagickFormat.Png);
    }

    /// <summary>A small PNG whose header claims another size, with a correct checksum, so only the header lies.</summary>
    private static byte[] PngClaiming(uint width, uint height)
    {
        var png = Png(16, 16);
        // Signature (8), chunk length (4), "IHDR" (4), then width and height; the CRC covers type and data (17 bytes).
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), width);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), height);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(29), Crc32.HashToUInt32(png.AsSpan(12, 17)));
        return png;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, Guid gardenId, Guid designId, byte[] render, Guid? viewId = null, bool sendPhotos = true)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(designId.ToString()), "designId" },
            { new StringContent((viewId ?? ViewId).ToString()), "viewId" },
            { new StringContent(sendPhotos ? "true" : "false"), "sendPhotos" },
        };
        var file = new ByteArrayContent(render);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "render", "vy.png");
        return await client.PostAsync(Dreams(gardenId), form, Ct);
    }

    private static async Task<Guid> PostOkAsync(HttpClient client, Guid gardenId, Guid designId)
    {
        using var response = await PostAsync(client, gardenId, designId, Png());
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task RunAsync()
    {
        using var scope = _app.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<JobRunner>().RunNextAsync(Ct));
    }

    private async Task<int> JobCountAsync(Guid gardenId)
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AiDbContext>().Set<AiJob>().CountAsync(j => j.GardenId == gardenId, Ct);
    }

    [Fact]
    public async Task The_whole_flow_gives_a_test_image_with_links()
    {
        var (client, gardenId, designId) = await CustomerWithViewAsync();

        var jobId = await PostOkAsync(client, gardenId, designId);
        var queued = await client.GetFromJsonAsync<JsonElement>(Dreams(gardenId, $"/{jobId}"), Ct);
        Assert.Equal("queued", queued.GetProperty("status").GetString());
        Assert.Equal("Från gatan", queued.GetProperty("viewName").GetString());
        await RunAsync();

        var job = await client.GetFromJsonAsync<JsonElement>(Dreams(gardenId, $"/{jobId}"), Ct);
        Assert.Equal("succeeded", job.GetProperty("status").GetString());
        Assert.True(job.GetProperty("test").GetBoolean());
        Assert.Equal(2, job.GetProperty("revision").GetInt32());
        Assert.Equal("Från gatan", job.GetProperty("viewName").GetString());
        Assert.Contains("image.jpg", job.GetProperty("imageUrl").GetString(), StringComparison.Ordinal);
        Assert.Contains("render.jpg", job.GetProperty("renderUrl").GetString(), StringComparison.Ordinal);
        var list = await client.GetFromJsonAsync<JsonElement[]>(Dreams(gardenId), Ct);
        Assert.Equal(jobId, Assert.Single(list!).GetProperty("id").GetGuid());
    }

    public static TheoryData<string> BadRenders => ["jpeg-named-png", "nine-megabytes", "claims-20000-px"];

    [Theory]
    [MemberData(nameof(BadRenders))]
    public async Task A_bad_render_is_refused_and_nothing_is_queued(string kind)
    {
        var (client, gardenId, designId) = await CustomerWithViewAsync();
        var render = kind switch
        {
            "jpeg-named-png" => Jpeg(1536, 1024),
            "nine-megabytes" => [.. Png(), .. new byte[9 * 1024 * 1024]],
            _ => PngClaiming(20_000, 20_000),
        };

        using var response = await PostAsync(client, gardenId, designId, render);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-image", await CodeAsync(response));
        Assert.Equal(0, await JobCountAsync(gardenId));
    }

    [Fact]
    public async Task An_unknown_view_is_refused()
    {
        var (client, gardenId, designId) = await CustomerWithViewAsync();

        using var response = await PostAsync(client, gardenId, designId, Png(), viewId: Ids.New());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("no-such-view", await CodeAsync(response));
    }

    [Fact]
    public async Task Two_clicks_at_once_with_one_left_give_exactly_one_image()
    {
        var (client, gardenId, designId) = await CustomerWithViewAsync();
        for (var i = 0; i < 4; i++)
        {
            await PostOkAsync(client, gardenId, designId);
        }

        var responses = await Task.WhenAll(
            PostAsync(client, gardenId, designId, Png()),
            PostAsync(client, gardenId, designId, Png()));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Accepted);
        var refused = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("quota-reached", await CodeAsync(refused));
        Assert.Equal(5, await JobCountAsync(gardenId));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task A_failed_image_shows_the_render_frees_its_place_and_can_be_removed()
    {
        var (client, gardenId, designId) = await CustomerWithViewAsync();
        for (var i = 0; i < 4; i++)
        {
            await PostOkAsync(client, gardenId, designId);
            await RunAsync();
        }

        _model.Fail = true;
        var failedId = await PostOkAsync(client, gardenId, designId);
        await RunAsync();
        _model.Fail = false;

        var failed = await client.GetFromJsonAsync<JsonElement>(Dreams(gardenId, $"/{failedId}"), Ct);
        Assert.Equal("failed", failed.GetProperty("status").GetString());
        Assert.Equal(SwitchableImageModel.FailureText, failed.GetProperty("failure").GetString());
        Assert.Contains("render.jpg", failed.GetProperty("renderUrl").GetString(), StringComparison.Ordinal);
        Assert.False(failed.TryGetProperty("imageUrl", out var imageUrl) && imageUrl.ValueKind == JsonValueKind.String);

        // The failed one does not count: a fifth image can still be made.
        var fifth = await PostOkAsync(client, gardenId, designId);
        using (var succeeded = await client.DeleteAsync(Dreams(gardenId, $"/{fifth}"), Ct))
        {
            Assert.Equal(HttpStatusCode.Conflict, succeeded.StatusCode);
            Assert.Equal("not-failed", await CodeAsync(succeeded));
        }

        using var removed = await client.DeleteAsync(Dreams(gardenId, $"/{failedId}"), Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var gone = await client.GetAsync(Dreams(gardenId, $"/{failedId}"), Ct);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        using var scope = _app.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<DreamImageStore>().ReadAsync(DreamImageStore.RenderBlob(gardenId, failedId), Ct));
    }

    [Fact]
    public async Task Another_customers_garden_or_image_answers_404()
    {
        var (owner, gardenId, designId) = await CustomerWithViewAsync();
        var jobId = await PostOkAsync(owner, gardenId, designId);
        var (stranger, strangerGarden, _) = await CustomerAsync(_app);

        using var list = await stranger.GetAsync(Dreams(gardenId), Ct);
        using var one = await stranger.GetAsync(Dreams(gardenId, $"/{jobId}"), Ct);
        using var viaOwnGarden = await stranger.GetAsync(Dreams(strangerGarden, $"/{jobId}"), Ct);
        using var post = await PostAsync(stranger, gardenId, designId, Png());
        using var delete = await stranger.DeleteAsync(Dreams(gardenId, $"/{jobId}"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, one.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, viaOwnGarden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    /// <summary>The real test model (no pause), which a test can make fail.</summary>
    private sealed class SwitchableImageModel : IImageModel
    {
        public const string FailureText = "Bildtjänsten avböjde.";

        private readonly TestImageModel _inner = new(Options.Create(new ImagesOptions { TestDelaySeconds = 0 }));

        public bool Fail { get; set; }

        public string Provider => _inner.Provider;

        public string ModelFor(string kind) => _inner.ModelFor(kind);

        public Task<ImageResponse> EditAsync(ImageRequest request, CancellationToken cancellationToken) =>
            Fail ? throw new AiProviderException(FailureText, retryable: false) : _inner.EditAsync(request, cancellationToken);
    }

    /// <summary>The runner needs a text model; a dream image never calls it.</summary>
    private sealed class UnusedTextModel : ITextModel
    {
        public string Provider => "unused";

        public string ModelFor(string kind) => "claude-haiku-5-5";

        public Task<TextResponse> CompleteAsync(TextRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A dream image makes no text call.");
    }
}
