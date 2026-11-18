using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImageMagick;

using static GardenBuilder.IntegrationTests.PhotoTestHelpers;

namespace GardenBuilder.IntegrationTests;

/// <summary>Photo upload, listing, camera poses and removal. Runs against PostgreSQL and Azurite in Docker.</summary>
public sealed class GardenPhotoEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly HttpClient Storage = new();

    private static async Task<JsonElement[]> ListAsync(HttpClient client, Guid gardenId) =>
        [.. (await client.GetFromJsonAsync<JsonElement>(Photos(gardenId), Ct)).EnumerateArray()];

    [Fact]
    public async Task Upload_list_and_read_a_photo()
    {
        var (client, gardenId, _) = await PhotoTestHelpers.CustomerAsync(factory);

        using var uploaded = await UploadAsync(client, gardenId, Jpeg(4000, 3000, withGps: true));

        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var body = await uploaded.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(1568, body.GetProperty("width").GetInt32());
        var photo = Assert.Single(await ListAsync(client, gardenId));
        Assert.Equal(body.GetProperty("id").GetGuid(), photo.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, photo.GetProperty("camera").ValueKind);
        var bytes = await Storage.GetByteArrayAsync(new Uri(photo.GetProperty("url").GetString()!), Ct);
        using var stored = new MagickImage(bytes);
        Assert.Equal(MagickFormat.Jpeg, stored.Format);
        Assert.Null(stored.GetExifProfile());
    }

    [Fact]
    public async Task Seventh_photo_is_409_too_many_photos()
    {
        var (client, gardenId, _) = await PhotoTestHelpers.CustomerAsync(factory);
        for (var i = 0; i < 6; i++)
        {
            await UploadOkAsync(client, gardenId);
        }

        using var seventh = await UploadAsync(client, gardenId, Jpeg());

        Assert.Equal(HttpStatusCode.Conflict, seventh.StatusCode);
        Assert.Equal("too-many-photos", await CodeAsync(seventh));
        Assert.Equal(6, (await ListAsync(client, gardenId)).Length);
    }

    [Fact]
    public async Task Two_uploads_racing_for_the_sixth_slot_store_one()
    {
        var (client, gardenId, _) = await PhotoTestHelpers.CustomerAsync(factory);
        for (var i = 0; i < 5; i++)
        {
            await UploadOkAsync(client, gardenId);
        }

        var responses = await Task.WhenAll(UploadAsync(client, gardenId, Jpeg()), UploadAsync(client, gardenId, Jpeg()));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        Assert.Equal(6, (await ListAsync(client, gardenId)).Length);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task File_over_15_MB_is_413_too_large()
    {
        var (client, gardenId, _) = await PhotoTestHelpers.CustomerAsync(factory);

        using var response = await UploadAsync(client, gardenId, new byte[(15 * 1024 * 1024) + 1]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("too-large", await CodeAsync(response));
    }

    [Fact]
    public async Task Text_file_is_400_not_an_image()
    {
        var (client, gardenId, _) = await PhotoTestHelpers.CustomerAsync(factory);

        using var response = await UploadAsync(client, gardenId, "Inget foto här."u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("not-an-image", await CodeAsync(response));
        Assert.Empty(await ListAsync(client, gardenId));
    }

    [Fact]
    public async Task Another_customers_garden_is_404_for_every_endpoint()
    {
        var (owner, gardenId, _) = await PhotoTestHelpers.CustomerAsync(factory);
        var photoId = await UploadOkAsync(owner, gardenId);
        var (other, _, _) = await PhotoTestHelpers.CustomerAsync(factory);
        using var anonymous = IdentityTestHelpers.HttpsClient(factory);

        foreach (var client in new[] { other, anonymous })
        {
            using var upload = await UploadAsync(client, gardenId, Jpeg());
            using var list = await client.GetAsync(Photos(gardenId), Ct);
            using var camera = await client.PutAsJsonAsync(Photos(gardenId, $"/{photoId}/camera"), new { x = 5, y = 5, heading = 0 }, Ct);
            using var delete = await client.DeleteAsync(Photos(gardenId, $"/{photoId}"), Ct);
            Assert.All([upload, list, camera, delete], r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        }

        Assert.Single(await ListAsync(owner, gardenId));
    }

    [Fact]
    public async Task Camera_is_saved_and_listed()
    {
        var (client, gardenId, _) = await PhotoTestHelpers.CustomerAsync(factory);
        var photoId = await UploadOkAsync(client, gardenId);

        using var saved = await client.PutAsJsonAsync(Photos(gardenId, $"/{photoId}/camera"), new { x = 3.5, y = -4, heading = 270 }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        var camera = Assert.Single(await ListAsync(client, gardenId)).GetProperty("camera");
        Assert.Equal((3.5, -4.0, 270.0), (camera.GetProperty("x").GetDouble(), camera.GetProperty("y").GetDouble(), camera.GetProperty("heading").GetDouble()));
    }

    [Fact]
    public async Task Camera_six_meters_outside_the_plot_is_400()
    {
        var (client, gardenId, _) = await PhotoTestHelpers.CustomerAsync(factory);
        var photoId = await UploadOkAsync(client, gardenId);

        using var response = await client.PutAsJsonAsync(Photos(gardenId, $"/{photoId}/camera"), new { x = 26, y = 10, heading = 0 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("camera-outside-plot", await CodeAsync(response));
    }

    [Fact]
    public async Task Deleting_removes_the_blob()
    {
        var (client, gardenId, _) = await PhotoTestHelpers.CustomerAsync(factory);
        var photoId = await UploadOkAsync(client, gardenId);
        var url = new Uri(Assert.Single(await ListAsync(client, gardenId)).GetProperty("url").GetString()!);

        using var deleted = await client.DeleteAsync(Photos(gardenId, $"/{photoId}"), Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await ListAsync(client, gardenId));
        using var gone = await Storage.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }
}
