using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;
using ImageMagick;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GardenBuilder.IntegrationTests;

/// <summary>A customer with a design and photos, shared by the photo and photo interpretation tests.</summary>
internal static class PhotoTestHelpers
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A new customer with a design on a 20 × 20 m plot and a house in its north-east corner.</summary>
    public static async Task<(HttpClient Client, Guid GardenId, Guid DesignId)> CustomerAsync(WebApplicationFactory<Program> app)
    {
        var client = IdentityTestHelpers.HttpsClient(app);
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion,
        [
            new PlotElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer,
                [new Point2(0, 0), new Point2(20, 0), new Point2(20, 20), new Point2(0, 20)], Sloped: false),
            new BuildingElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer,
                [new Point2(14, 16), new Point2(18, 16), new Point2(18, 19), new Point2(14, 19)], Height: 5, Roof: RoofShape.Gable),
        ]);
        using var body = new StringContent(SceneJson.Serialize(scene), Encoding.UTF8, "application/json");
        using var created = await client.PostAsync(new Uri("/api/designs", UriKind.Relative), body, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (client, json.GetProperty("gardenId").GetGuid(), json.GetProperty("id").GetGuid());
    }

    public static byte[] Jpeg(uint width = 800, uint height = 600, bool withGps = false)
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

    public static Uri Photos(Guid gardenId, string rest = "") => new($"/api/gardens/{gardenId}/photos{rest}", UriKind.Relative);

    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid gardenId, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "foto.jpg");
        return await client.PostAsync(Photos(gardenId), form, Ct);
    }

    public static async Task<Guid> UploadOkAsync(HttpClient client, Guid gardenId)
    {
        using var response = await UploadAsync(client, gardenId, Jpeg());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    /// <summary>Uploads a photo taken from the middle of the southern boundary, looking north.</summary>
    public static async Task<Guid> UploadFromSouthAsync(HttpClient client, Guid gardenId)
    {
        var id = await UploadOkAsync(client, gardenId);
        using var camera = await client.PutAsJsonAsync(Photos(gardenId, $"/{id}/camera"), new { x = 10, y = 0, heading = 0 }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, camera.StatusCode);
        return id;
    }

    public static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
