using System.Net.Http.Json;
using System.Text.Json;
using GardenBuilder.Modules.Identity;
using GardenBuilder.Modules.Identity.Domain;
using GardenBuilder.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>Has its own database (its own fixture), so customer counts are not disturbed by other tests.</summary>
public sealed class CurrentCustomerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Cookie = IdentityModule.CustomerCookieName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetOrCreate_then_Find_with_the_cookie_gives_the_same_customer()
    {
        Guid created;
        string setCookie;
        using (var first = new RequestContext(factory))
        {
            created = await first.Customer.GetOrCreateAsync(Token);
            setCookie = IdentityTestHelpers.SetCookie(first.Http.Response.Headers, Cookie)!;
        }

        Assert.NotNull(setCookie);
        var lower = setCookie.ToLowerInvariant();
        Assert.Contains("httponly", lower, StringComparison.Ordinal);
        Assert.Contains("secure", lower, StringComparison.Ordinal);
        Assert.Contains("samesite=lax", lower, StringComparison.Ordinal);
        var expiresText = setCookie[(lower.IndexOf("expires=", StringComparison.Ordinal) + "expires=".Length)..].Split(';')[0];
        var expires = DateTimeOffset.Parse(expiresText, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(expires, DateTimeOffset.UtcNow.AddDays(90).AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(90).AddMinutes(5));

        using var second = new RequestContext(factory, setCookie.Split(';')[0]);
        Assert.Equal(created, await second.Customer.FindAsync(Token));
        Assert.Equal(created, await second.Customer.GetOrCreateAsync(Token));
    }

    [Fact]
    public async Task Each_call_without_cookie_creates_a_new_customer()
    {
        var before = await CountCustomersAsync();
        using var a = new RequestContext(factory);
        using var b = new RequestContext(factory);

        var first = await a.Customer.GetOrCreateAsync(Token);
        var second = await b.Customer.GetOrCreateAsync(Token);

        Assert.NotEqual(first, second);
        Assert.Equal(before + 2, await CountCustomersAsync());
    }

    [Fact]
    public async Task Find_without_cookie_gives_null_and_creates_nothing()
    {
        var before = await CountCustomersAsync();
        using var request = new RequestContext(factory);

        Assert.Null(await request.Customer.FindAsync(Token));
        Assert.Equal(before, await CountCustomersAsync());
        Assert.Null(request.CookieSet(Cookie));
    }

    [Fact]
    public async Task Tampered_cookie_gives_null()
    {
        var cookie = await NewCustomerCookieAsync();
        var value = cookie[(Cookie.Length + 1)..];
        var tampered = $"{Cookie}={(value[10] == 'A' ? 'B' : 'A')}{value[..10]}{value[11..]}";

        using var request = new RequestContext(factory, tampered);

        Assert.Null(await request.Customer.FindAsync(Token));
    }

    [Fact]
    public async Task Cookie_for_deleted_customer_gives_null()
    {
        var cookie = await NewCustomerCookieAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var id = await db.Set<Customer>().OrderByDescending(c => c.CreatedAt).Select(c => c.Id).FirstAsync(Token);
            await db.Set<Customer>().Where(c => c.Id == id).ExecuteDeleteAsync(Token);
        }

        using var request = new RequestContext(factory, cookie);

        Assert.Null(await request.Customer.FindAsync(Token));
    }

    [Fact]
    public async Task Last_seen_moves_at_most_once_a_day_and_renews_the_cookie()
    {
        Guid id;
        string cookie;
        using (var first = new RequestContext(factory))
        {
            id = await first.Customer.GetOrCreateAsync(Token);
            cookie = first.CookieSet(Cookie)!;
        }

        var twoDaysAgo = DateTimeOffset.UtcNow.AddDays(-2);
        await SetLastSeenAsync(id, twoDaysAgo);

        string renewed;
        using (var visit = new RequestContext(factory, cookie))
        {
            Assert.Equal(id, await visit.Customer.FindAsync(Token));
            renewed = visit.CookieSet(Cookie)!;
        }

        Assert.NotNull(renewed);
        var lastSeen = await LastSeenAsync(id);
        Assert.True(lastSeen > DateTimeOffset.UtcNow.AddMinutes(-5), $"last_seen_at was {lastSeen}.");

        using var sameDay = new RequestContext(factory, renewed);
        Assert.Equal(id, await sameDay.Customer.FindAsync(Token));
        Assert.Null(sameDay.CookieSet(Cookie));
        Assert.Equal(lastSeen, await LastSeenAsync(id));
    }

    [Fact]
    public async Task Me_without_cookie_is_none_and_creates_no_customer()
    {
        var before = await CountCustomersAsync();
        using var client = IdentityTestHelpers.HttpsClient(factory);

        var me = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/identity/me", UriKind.Relative), Token);

        Assert.Equal("none", me.GetProperty("kind").GetString());
        Assert.Equal(before, await CountCustomersAsync());
    }

    [Fact]
    public async Task Me_with_customer_cookie_is_customer()
    {
        var cookie = await NewCustomerCookieAsync();
        using var client = IdentityTestHelpers.HttpsClient(factory, handleCookies: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/identity/me", UriKind.Relative));
        request.Headers.Add("Cookie", cookie);

        using var response = await client.SendAsync(request, Token);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(Token);

        Assert.Equal("customer", me.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Cookie_survives_a_new_host_with_the_same_database()
    {
        var cookie = await NewCustomerCookieAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            Assert.True(await db.DataProtectionKeys.AnyAsync(Token), "The key that protects the cookie is not stored in the database.");
        }

        // A second host is a new process as far as the API is concerned: it only shares the database.
        using var restarted = factory.WithWebHostBuilder(_ => { });
        using var request = new RequestContext(restarted, cookie);

        Assert.NotNull(await request.Customer.FindAsync(Token));
    }

    private async Task<string> NewCustomerCookieAsync()
    {
        using var request = new RequestContext(factory);
        await request.Customer.GetOrCreateAsync(Token);
        return request.CookieSet(Cookie)!;
    }

    private async Task<int> CountCustomersAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<Customer>().CountAsync(Token);
    }

    private async Task SetLastSeenAsync(Guid id, DateTimeOffset when)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<Customer>()
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastSeenAt, when), Token);
    }

    private async Task<DateTimeOffset> LastSeenAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<Customer>()
            .Where(c => c.Id == id).Select(c => c.LastSeenAt).SingleAsync(Token);
    }
}
