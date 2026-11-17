using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>The AI endpoints in admin: only administrators may start the connection test or read and change the settings.</summary>
public sealed class AiAdminEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await db.Set<AiJob>().ExecuteDeleteAsync(Ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ai.settings", Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<StaffBrowser> SignedInAsync(StaffRole role)
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, role);
        var browser = new StaffBrowser(factory);
        await browser.FirstLoginAsync(email, temporary.Password);
        return browser;
    }

    private async Task<int> JobCountAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AiDbContext>().Set<AiJob>().CountAsync(Ct);
    }

    private static Uri Url(string path) => new("/api/ai/" + path, UriKind.Relative);

    private static object Settings(decimal budget = 500, int photo = 3, bool dreamImage = true) => new
    {
        dailyBudget = budget,
        quotas = new Dictionary<string, int> { ["photo-interpretation"] = photo, ["design-proposal"] = 5, ["dream-image"] = 5 },
        enabled = new Dictionary<string, bool> { ["photo-interpretation"] = true, ["design-proposal"] = true, ["dream-image"] = dreamImage },
    };

    /// <summary>Each endpoint with the request a caller would send.</summary>
    private static IEnumerable<Func<HttpClient, Task<HttpResponseMessage>>> AllEndpoints() =>
    [
        c => c.PostAsync(Url("connection-test"), null, Ct),
        c => c.GetAsync(Url($"jobs/{Ids.New()}"), Ct),
        c => c.GetAsync(Url("settings"), Ct),
        c => c.PutAsJsonAsync(Url("settings"), Settings(budget: 1), Ct),
    ];

    [Fact]
    public async Task Admin_starts_a_connection_test_and_reads_it_back()
    {
        using var admin = await SignedInAsync(StaffRole.Admin);

        using var started = await admin.Client.PostAsync(Url("connection-test"), null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var id = (await started.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        var job = await admin.Client.GetFromJsonAsync<JsonElement>(Url($"jobs/{id}"), Ct);
        Assert.Equal("connection-test", job.GetProperty("kind").GetString());
        Assert.Equal("queued", job.GetProperty("status").GetString());
        Assert.Equal(0, job.GetProperty("attempts").GetInt32());
    }

    [Fact]
    public async Task Sales_staff_get_403_and_nothing_is_queued_or_changed()
    {
        using var sales = await SignedInAsync(StaffRole.Sales);
        var before = await JobCountAsync();

        foreach (var call in AllEndpoints())
        {
            using var response = await call(sales.Client);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Equal(before, await JobCountAsync());
        using var admin = await SignedInAsync(StaffRole.Admin);
        Assert.Equal(500m, (await admin.Client.GetFromJsonAsync<JsonElement>(Url("settings"), Ct)).GetProperty("dailyBudget").GetDecimal());
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        using var anonymous = IdentityTestHelpers.HttpsClient(factory);

        foreach (var call in AllEndpoints())
        {
            using var response = await call(anonymous);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_customer_cookie_gets_401()
    {
        using var customer = IdentityTestHelpers.HttpsClient(factory);
        var scene = new SceneDocument(SceneDocument.CurrentFormatVersion,
        [
            new PlotElement(Ids.New(), ElementStatus.ExistingKeep, MeasurementSource.Customer,
                [new Point2(0, 0), new Point2(10, 0), new Point2(10, 10), new Point2(0, 10)], Sloped: false),
        ]);
        using var body = new StringContent(SceneJson.Serialize(scene), Encoding.UTF8, "application/json");
        using var created = await customer.PostAsync(new Uri("/api/designs", UriKind.Relative), body, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        foreach (var call in AllEndpoints())
        {
            using var response = await call(customer);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Settings_round_trip_with_todays_cost()
    {
        using var admin = await SignedInAsync(StaffRole.Admin);

        using var saved = await admin.Client.PutAsJsonAsync(Url("settings"), Settings(budget: 300, photo: 2, dreamImage: false), Ct);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var read = await admin.Client.GetFromJsonAsync<JsonElement>(Url("settings"), Ct);
        Assert.Equal(300m, read.GetProperty("dailyBudget").GetDecimal());
        Assert.Equal(2, read.GetProperty("quotas").GetProperty("photo-interpretation").GetInt32());
        Assert.False(read.GetProperty("enabled").GetProperty("dream-image").GetBoolean());
        Assert.True(read.GetProperty("enabled").GetProperty("photo-interpretation").GetBoolean());
        Assert.Equal(0m, read.GetProperty("todayCost").GetDecimal());
    }

    [Fact]
    public async Task Invalid_settings_are_400_with_swedish_problems()
    {
        using var admin = await SignedInAsync(StaffRole.Admin);

        using var response = await admin.Client.PutAsJsonAsync(Url("settings"), Settings(budget: -1, photo: 101), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("invalid-settings", body.GetProperty("code").GetString());
        var problems = body.GetProperty("problems").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains("Dagsbudgeten ska vara mellan 0 och 100 000 kr.", problems);
        Assert.Contains("Kvoten för fototolkning ska vara mellan 0 och 100.", problems);
    }

    [Fact]
    public async Task A_connection_test_while_the_budget_is_spent_is_409()
    {
        using var admin = await SignedInAsync(StaffRole.Admin);
        using var zero = await admin.Client.PutAsJsonAsync(Url("settings"), Settings(budget: 0), Ct);

        using var response = await admin.Client.PostAsync(Url("connection-test"), null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("budget-reached", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_job_is_404()
    {
        using var admin = await SignedInAsync(StaffRole.Admin);

        using var response = await admin.Client.GetAsync(Url($"jobs/{Ids.New()}"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
