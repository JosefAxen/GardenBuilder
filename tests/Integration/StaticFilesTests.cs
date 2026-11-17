using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace GardenBuilder.IntegrationTests;

/// <summary>The API serves the built web client from wwwroot, including the editor's 3D models.</summary>
public sealed class StaticFilesTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly string _webRoot = Directory.CreateTempSubdirectory("gardenbuilder-wwwroot-").FullName;

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    [Fact]
    public async Task Glb_models_are_served_as_gltf_binary()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        await File.WriteAllBytesAsync(Path.Combine(_webRoot, "assets", "tree-large-abc123.glb"), [0x67, 0x6C, 0x54, 0x46], TestContext.Current.CancellationToken);
        using var app = factory.WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot));
        using var client = app.CreateClient();

        using var response = await client.GetAsync(new Uri("/assets/tree-large-abc123.glb", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("model/gltf-binary", response.Content.Headers.ContentType?.MediaType);
    }
}
