using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Garden.Infrastructure;
using GardenBuilder.Modules.Identity.Domain;
using GardenBuilder.Modules.Identity.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>Has its own database (its own fixture), so the customer and garden counts here are not disturbed.</summary>
public sealed class DesignEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/designs";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A plot of size × size meters, so each scene is easy to tell apart.</summary>
    private static SceneDocument Scene(double size, params SceneElement[] more) => new(SceneDocument.CurrentFormatVersion,
    [
        new PlotElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer,
            [new Point2(0, 0), new Point2(size, 0), new Point2(size, size), new Point2(0, size)], Sloped: false),
        .. more,
    ]);

    [Fact]
    public async Task First_post_creates_customer_garden_and_design()
    {
        using var client = IdentityTestHelpers.HttpsClient(factory);
        var scene = Scene(10);

        using var created = await PostAsync(client, SceneJson.Serialize(scene));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("\"1\"", created.Headers.ETag?.ToString());
        Assert.Contains(created.Headers.GetValues("Set-Cookie"), c => c.StartsWith("gb-customer=", StringComparison.Ordinal));
        var body = await ReadAsync(created);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"{Url}/{id}", created.Headers.Location?.ToString());

        using var latest = await client.GetAsync(new Uri($"{Url}/latest", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
        Assert.Equal("\"1\"", latest.Headers.ETag?.ToString());
        var latestBody = await ReadAsync(latest);
        Assert.Equal(id, latestBody.GetProperty("id").GetGuid());
        Assert.Equal(1, latestBody.GetProperty("revision").GetInt32());
        Assert.Equal(SceneJson.Serialize(scene), SceneJson.Serialize(SceneJson.Deserialize(latestBody.GetProperty("scene").GetRawText())));
    }

    [Fact]
    public async Task Get_returns_garden_id_and_version_2_for_a_stored_version_1_scene()
    {
        using var client = IdentityTestHelpers.HttpsClient(factory);
        var id = await CreateAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GardenBuilder.Modules.Design.Infrastructure.DesignDbContext>();
            var version1 = SceneJson.Serialize(Scene(10)).Replace("\"formatVersion\":2", "\"formatVersion\":1", StringComparison.Ordinal);
            await db.Database.ExecuteSqlAsync($"UPDATE design.designs SET working_copy = {version1}::jsonb WHERE id = {id}", Ct);
        }

        var body = await GetAsync(client, id);

        Assert.NotEqual(Guid.Empty, body.GetProperty("gardenId").GetGuid());
        Assert.Equal(2, body.GetProperty("scene").GetProperty("formatVersion").GetInt32());
    }

    [Fact]
    public async Task Post_when_the_customer_has_a_design_is_409_with_it_and_creates_nothing()
    {
        using var client = IdentityTestHelpers.HttpsClient(factory);
        var id = await CreateAsync(client);
        using var saved = await PutAsync(client, id, SceneJson.Serialize(Scene(20)), "\"1\"");
        var gardens = await CountAsync<GardenDbContext, GardenBuilder.Modules.Garden.Domain.Garden>();

        using var second = await PostAsync(client, SceneJson.Serialize(Scene(30)));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await ReadAsync(second);
        Assert.Equal("design-exists", body.GetProperty("code").GetString());
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal(2, body.GetProperty("revision").GetInt32());
        Assert.Equal(gardens, await CountAsync<GardenDbContext, GardenBuilder.Modules.Garden.Domain.Garden>());
        var stored = await GetAsync(client, id);
        Assert.Equal(2, stored.GetProperty("revision").GetInt32());
    }

    [Fact]
    public async Task Latest_without_cookie_is_404_and_creates_no_customer()
    {
        var before = await CountAsync<IdentityDbContext, Customer>();
        using var client = IdentityTestHelpers.HttpsClient(factory);

        using var response = await client.GetAsync(new Uri($"{Url}/latest", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(before, await CountAsync<IdentityDbContext, Customer>());
    }

    [Fact]
    public async Task Put_with_current_revision_saves()
    {
        using var client = IdentityTestHelpers.HttpsClient(factory);
        var id = await CreateAsync(client);
        var second = Scene(20);

        using var saved = await PutAsync(client, id, SceneJson.Serialize(second), "\"1\"");

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("\"2\"", saved.Headers.ETag?.ToString());
        Assert.Equal(2, (await ReadAsync(saved)).GetProperty("revision").GetInt32());
        var stored = await GetAsync(client, id);
        Assert.Equal(2, stored.GetProperty("revision").GetInt32());
        Assert.Equal(SceneJson.Serialize(second), SceneJson.Serialize(SceneJson.Deserialize(stored.GetProperty("scene").GetRawText())));
    }

    [Fact]
    public async Task Stale_revision_is_412_with_current_revision()
    {
        using var client = IdentityTestHelpers.HttpsClient(factory);
        var id = await CreateAsync(client);
        using var first = await PutAsync(client, id, SceneJson.Serialize(Scene(20)), "\"1\"");

        using var stale = await PutAsync(client, id, SceneJson.Serialize(Scene(30)), "\"1\"");

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var body = await ReadAsync(stale);
        Assert.Equal("conflict", body.GetProperty("code").GetString());
        Assert.Equal(2, body.GetProperty("revision").GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("\"abc\"")]
    public async Task Missing_if_match_is_428(string? ifMatch)
    {
        using var client = IdentityTestHelpers.HttpsClient(factory);
        var id = await CreateAsync(client);

        using var response = await PutAsync(client, id, SceneJson.Serialize(Scene(20)), ifMatch);

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        Assert.Equal("revision-required", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Weak_if_match_is_accepted()
    {
        using var client = IdentityTestHelpers.HttpsClient(factory);
        var id = await CreateAsync(client);

        using var response = await PutAsync(client, id, SceneJson.Serialize(Scene(20)), "W/\"1\"");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Other_customer_gets_404_and_design_is_unchanged()
    {
        using var owner = IdentityTestHelpers.HttpsClient(factory);
        var id = await CreateAsync(owner);
        using var other = IdentityTestHelpers.HttpsClient(factory);
        await CreateAsync(other);

        using var get = await other.GetAsync(new Uri($"{Url}/{id}", UriKind.Relative), Ct);
        using var put = await PutAsync(other, id, SceneJson.Serialize(Scene(20)), "\"1\"");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(1, (await GetAsync(owner, id)).GetProperty("revision").GetInt32());
    }

    [Fact]
    public async Task No_cookie_gets_404()
    {
        using var owner = IdentityTestHelpers.HttpsClient(factory);
        var id = await CreateAsync(owner);
        using var anonymous = IdentityTestHelpers.HttpsClient(factory);

        using var get = await anonymous.GetAsync(new Uri($"{Url}/{id}", UriKind.Relative), Ct);
        using var put = await PutAsync(anonymous, id, SceneJson.Serialize(Scene(20)), "\"1\"");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.False(put.Headers.Contains("Set-Cookie"));
        Assert.Equal(1, (await GetAsync(owner, id)).GetProperty("revision").GetInt32());
    }

    [Fact]
    public async Task Invalid_scene_is_400_and_creates_nothing()
    {
        var customers = await CountAsync<IdentityDbContext, Customer>();
        var gardens = await CountAsync<GardenDbContext, Modules.Garden.Domain.Garden>();
        using var client = IdentityTestHelpers.HttpsClient(factory);
        var outside = new PointElement(Ids.New(), ElementStatus.New, MeasurementSource.Estimated, null, new Point2(50, 50), 0);

        using var response = await PostAsync(client, SceneJson.Serialize(Scene(10, outside)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-scene", (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.Equal(customers, await CountAsync<IdentityDbContext, Customer>());
        Assert.Equal(gardens, await CountAsync<GardenDbContext, Modules.Garden.Domain.Garden>());
    }

    [Fact]
    public async Task Unreadable_scene_is_400()
    {
        using var client = IdentityTestHelpers.HttpsClient(factory);

        using var response = await PostAsync(client, "not json");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unreadable-scene", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scene_over_1_MB_is_413()
    {
        using var client = IdentityTestHelpers.HttpsClient(factory);
        var id = await CreateAsync(client);
        var huge = "{\"formatVersion\":1,\"elements\":[]}" + new string(' ', (1024 * 1024) + 1);

        using var post = await PostAsync(client, huge);
        using var put = await PutAsync(client, id, huge, "\"1\"");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, post.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, put.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri(Url, UriKind.Relative), content, Ct);
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string body, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"{Url}/{id}/working-copy", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task<Guid> CreateAsync(HttpClient client)
    {
        using var response = await PostAsync(client, SceneJson.Serialize(Scene(10)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(new Uri($"{Url}/{id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task<int> CountAsync<TContext, TEntity>()
        where TContext : DbContext
        where TEntity : class
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TContext>().Set<TEntity>().CountAsync(Ct);
    }
}
