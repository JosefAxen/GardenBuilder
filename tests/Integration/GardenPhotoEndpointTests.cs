using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;
using ImageMagick;

namespace GardenBuilder.IntegrationTests;

/// <summary>Photo upload, listing, camera poses and removal. Runs against PostgreSQL and Azurite in Docker.</summary>
public sealed class GardenPhotoEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly HttpClient Storage = new();

    /// <summary>A new customer with a design on a 20 × 20 m plot. Returns their browser and garden.</summary>
    private async Task<(HttpClient Client, Guid GardenId)> CustomerAsync()
    {
        var client = IdentityTestHelpers.HttpsClient(factory);
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion,
        [
            new PlotElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer,
                [new Point2(0, 0), new Point2(20, 0), new Point2(20, 20), new Point2(0, 20)], Sloped: false),
        ]);
        using var body = new StringContent(SceneJson.Serialize(scene), Encoding.UTF8, "application/json");
        using var created = await client.PostAsync(new Uri("/api/designs", UriKind.Relative), body, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (client, json.GetProperty("gardenId").GetGuid());
    }

    private static byte[] Jpeg(uint width = 800, uint height = 600, bool withGps = false)
    {
        using var image = new MagickImage(MagickColors.ForestGreen, width, height);
        if (withGps)
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.GPSLatitude, [new Rational(59, 1), new Rational(20, 1), new Rational(0, 1)]);
            image.SetProfile(exif);
        }

        return image.ToByteArray(MagickFormat.Jpeg);
    }

    private static Uri Photos(Guid gardenId, string rest = "") => new($"/api/gardens/{gardenId}/photos{rest}", UriKind.Relative);

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid gardenId, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "foto.jpg");
        return await client.PostAsync(Photos(gardenId), form, Ct);
    }

    private static async Task<Guid> UploadOkAsync(HttpClient client, Guid gardenId)
    {
        using var response = await UploadAsync(client, gardenId, Jpeg());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static async Task<JsonElement[]> ListAsync(HttpClient client, Guid gardenId) =>
        [.. (await client.GetFromJsonAsync<JsonElement>(Photos(gardenId), Ct)).EnumerateArray()];

    [Fact]
    public async Task Upload_list_and_read_a_photo()
    {
        var (client, gardenId) = await CustomerAsync();

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
        var (client, gardenId) = await CustomerAsync();
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
        var (client, gardenId) = await CustomerAsync();
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
        var (client, gardenId) = await CustomerAsync();

        using var response = await UploadAsync(client, gardenId, new byte[(15 * 1024 * 1024) + 1]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("too-large", await CodeAsync(response));
    }

    [Fact]
    public async Task Text_file_is_400_not_an_image()
    {
        var (client, gardenId) = await CustomerAsync();

        using var response = await UploadAsync(client, gardenId, "Inget foto här."u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("not-an-image", await CodeAsync(response));
        Assert.Empty(await ListAsync(client, gardenId));
    }

    [Fact]
    public async Task Another_customers_garden_is_404_for_every_endpoint()
    {
        var (owner, gardenId) = await CustomerAsync();
        var photoId = await UploadOkAsync(owner, gardenId);
        var (other, _) = await CustomerAsync();
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
        var (client, gardenId) = await CustomerAsync();
        var photoId = await UploadOkAsync(client, gardenId);

        using var saved = await client.PutAsJsonAsync(Photos(gardenId, $"/{photoId}/camera"), new { x = 3.5, y = -4, heading = 270 }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        var camera = Assert.Single(await ListAsync(client, gardenId)).GetProperty("camera");
        Assert.Equal((3.5, -4.0, 270.0), (camera.GetProperty("x").GetDouble(), camera.GetProperty("y").GetDouble(), camera.GetProperty("heading").GetDouble()));
    }

    [Fact]
    public async Task Camera_six_meters_outside_the_plot_is_400()
    {
        var (client, gardenId) = await CustomerAsync();
        var photoId = await UploadOkAsync(client, gardenId);

        using var response = await client.PutAsJsonAsync(Photos(gardenId, $"/{photoId}/camera"), new { x = 26, y = 10, heading = 0 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("camera-outside-plot", await CodeAsync(response));
    }

    [Fact]
    public async Task Deleting_removes_the_blob()
    {
        var (client, gardenId) = await CustomerAsync();
        var photoId = await UploadOkAsync(client, gardenId);
        var url = new Uri(Assert.Single(await ListAsync(client, gardenId)).GetProperty("url").GetString()!);

        using var deleted = await client.DeleteAsync(Photos(gardenId, $"/{photoId}"), Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await ListAsync(client, gardenId));
        using var gone = await Storage.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }
}
