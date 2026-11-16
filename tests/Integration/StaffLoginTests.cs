using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Identity;
using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GardenBuilder.IntegrationTests;

/// <summary>Has its own database (its own fixture). Each test creates its own staff member.</summary>
public sealed class StaffLoginTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task First_login_changes_password_sets_up_two_factor_and_signs_in()
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Admin);
        using var browser = new StaffBrowser(factory);

        Assert.Equal("change-password", await StaffBrowser.NextAsync(await browser.PostAsync("login", new { email, password = temporary.Password })));
        Assert.Equal("none", (await browser.MeAsync()).GetProperty("kind").GetString());
        Assert.Equal("setup-two-factor", await StaffBrowser.NextAsync(await browser.PostAsync("password", new { newPassword = StaffBrowser.NewPassword })));

        using var setup = await browser.GetAsync("two-factor/setup");
        var body = await setup.Content.ReadAsStringAsync(Token);
        Assert.Contains("otpauth://totp/Dr%C3%B6mtr%C3%A4dg%C3%A5rd:", body, StringComparison.Ordinal);

        var sharedKey = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("sharedKey").GetString()!;
        using var confirmed = await browser.PostAsync("two-factor/setup", new { code = Totp.Code(sharedKey) });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var codes = System.Text.Json.JsonDocument.Parse(await confirmed.Content.ReadAsStringAsync(Token)).RootElement.GetProperty("recoveryCodes");
        Assert.Equal(10, codes.GetArrayLength());

        var me = await browser.MeAsync();
        Assert.Equal("staff", me.GetProperty("kind").GetString());
        Assert.Equal("admin", me.GetProperty("role").GetString());
        Assert.Equal("Test Person", me.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Password_alone_gives_no_staff_session()
    {
        var email = await ReadyStaffAsync();
        using var browser = new StaffBrowser(factory);

        Assert.Equal("two-factor", await StaffBrowser.NextAsync(await browser.PostAsync("login", new { email, password = StaffBrowser.NewPassword })));

        Assert.Equal("none", (await browser.MeAsync()).GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Wrong_code_gives_invalid_code()
    {
        var email = await ReadyStaffAsync();
        using var browser = new StaffBrowser(factory);
        await browser.PostAsync("login", new { email, password = StaffBrowser.NewPassword });

        using var response = await browser.PostAsync("two-factor", new { code = "000000", recovery = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-code", await StaffBrowser.CodeAsync(response));
        Assert.Equal("none", (await browser.MeAsync()).GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Recovery_code_works_once()
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Sales);
        string[] codes;
        using (var first = new StaffBrowser(factory))
        {
            (_, codes) = await first.FirstLoginAsync(email, temporary.Password);
        }

        using (var second = new StaffBrowser(factory))
        {
            await second.PostAsync("login", new { email, password = StaffBrowser.NewPassword });
            using var used = await second.PostAsync("two-factor", new { code = codes[0], recovery = true });
            Assert.Equal(HttpStatusCode.NoContent, used.StatusCode);
            Assert.Equal("staff", (await second.MeAsync()).GetProperty("kind").GetString());
        }

        using var third = new StaffBrowser(factory);
        await third.PostAsync("login", new { email, password = StaffBrowser.NewPassword });
        using var again = await third.PostAsync("two-factor", new { code = codes[0], recovery = true });
        Assert.Equal("invalid-code", await StaffBrowser.CodeAsync(again));
    }

    [Fact]
    public async Task Codes_are_accepted_with_spaces()
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Sales);
        string sharedKey;
        string[] codes;
        using (var first = new StaffBrowser(factory))
        {
            (sharedKey, codes) = await first.FirstLoginAsync(email, temporary.Password);
        }

        using (var app = new StaffBrowser(factory))
        {
            await app.PostAsync("login", new { email, password = StaffBrowser.NewPassword });
            var code = Totp.Code(sharedKey);
            using var response = await app.PostAsync("two-factor", new { code = $"{code[..3]} {code[3..]}", recovery = false });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        using var recovery = new StaffBrowser(factory);
        await recovery.PostAsync("login", new { email, password = StaffBrowser.NewPassword });
        using var withSpaces = await recovery.PostAsync("two-factor", new { code = $" {codes[1].ToLowerInvariant()} ", recovery = true });
        Assert.Equal(HttpStatusCode.NoContent, withSpaces.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        var email = await ReadyStaffAsync();
        using var browser = new StaffBrowser(factory);
        for (var i = 0; i < 5; i++)
        {
            await browser.PostAsync("login", new { email, password = "fel lösenord som är långt" });
        }

        using var response = await browser.PostAsync("login", new { email, password = StaffBrowser.NewPassword });

        Assert.Equal("locked-out", await StaffBrowser.CodeAsync(response));
    }

    [Fact]
    public async Task Wrong_email_and_wrong_password_give_the_same_answer()
    {
        var email = await ReadyStaffAsync();
        using var browser = new StaffBrowser(factory);

        using var wrongPassword = await browser.PostAsync("login", new { email, password = "fel lösenord som är långt" });
        using var wrongEmail = await browser.PostAsync("login", new { email = "finns.inte@firma.se", password = "fel lösenord som är långt" });

        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, wrongEmail.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(Token), await wrongEmail.Content.ReadAsStringAsync(Token));
        Assert.Equal("invalid-credentials", await StaffBrowser.CodeAsync(wrongEmail));
    }

    [Fact]
    public async Task Login_ignores_case_and_spaces_in_email()
    {
        var email = await ReadyStaffAsync();
        using var browser = new StaffBrowser(factory);

        using var response = await browser.PostAsync("login", new { email = $"  {email.ToUpperInvariant()} ", password = StaffBrowser.NewPassword });

        Assert.Equal("two-factor", await StaffBrowser.NextAsync(response));
    }

    [Fact]
    public async Task Expired_temporary_password_is_rejected()
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Sales);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
            var user = (await users.FindByIdAsync(temporary.StaffId.ToString()))!;
            user.TemporaryPasswordExpiresAt = DateTimeOffset.UtcNow.AddHours(-1);
            await users.UpdateAsync(user);
        }

        using var browser = new StaffBrowser(factory);
        using var response = await browser.PostAsync("login", new { email, password = temporary.Password });

        Assert.Equal("temporary-password-expired", await StaffBrowser.CodeAsync(response));
        Assert.Equal("none", (await browser.MeAsync()).GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Old_temporary_password_stops_working()
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Sales);
        using (var first = new StaffBrowser(factory))
        {
            await first.FirstLoginAsync(email, temporary.Password);
        }

        using var browser = new StaffBrowser(factory);
        using var response = await browser.PostAsync("login", new { email, password = temporary.Password });

        Assert.Equal("invalid-credentials", await StaffBrowser.CodeAsync(response));
    }

    [Fact]
    public async Task Setup_page_asked_twice_with_the_same_cookie_shows_the_same_key()
    {
        // The web asks for the QR code twice at once (React does in development, and a slow network can too): both
        // requests carry the same cookie. The second must not see the setup session as expired.
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Sales);
        using var client = IdentityTestHelpers.HttpsClient(factory, handleCookies: false);

        using var login = await SendAsync(client, HttpMethod.Post, "login", null, new { email, password = temporary.Password });
        using var password = await SendAsync(client, HttpMethod.Post, "password", SetupCookie(login), new { newPassword = StaffBrowser.NewPassword });
        var cookie = SetupCookie(password);

        using var first = await SendAsync(client, HttpMethod.Get, "two-factor/setup", cookie);
        using var second = await SendAsync(client, HttpMethod.Get, "two-factor/setup", cookie);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var key = (await first.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("sharedKey").GetString()!;
        Assert.Equal(key, (await second.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("sharedKey").GetString());

        using var confirmed = await SendAsync(client, HttpMethod.Post, "two-factor/setup", cookie, new { code = Totp.Code(key) });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    /// <summary>The setup cookie a response set last (a login first clears any old one, then sets the new one).</summary>
    private static string SetupCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Last(c => c.StartsWith("gb-staff-setup=", StringComparison.Ordinal)).Split(';')[0];

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? cookie, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri("/api/identity/staff/" + path, UriKind.Relative));
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, Token);
    }

    [Fact]
    public async Task Setup_steps_without_session_are_401()
    {
        using var browser = new StaffBrowser(factory);

        HttpResponseMessage[] responses =
        [
            await browser.PostAsync("password", new { newPassword = StaffBrowser.NewPassword }),
            await browser.GetAsync("two-factor/setup"),
            await browser.PostAsync("two-factor/setup", new { code = "123456" }),
            await browser.PostAsync("two-factor", new { code = "123456", recovery = false }),
        ];

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("session-expired", await StaffBrowser.CodeAsync(response));
            response.Dispose();
        }
    }

    [Fact]
    public async Task Short_new_password_is_rejected()
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Sales);
        using var browser = new StaffBrowser(factory);
        await browser.PostAsync("login", new { email, password = temporary.Password });

        using var response = await browser.PostAsync("password", new { newPassword = "elva tecken" });

        Assert.Equal("password-too-short", await StaffBrowser.CodeAsync(response));
    }

    [Fact]
    public async Task Blocked_person_loses_open_session()
    {
        using var everyRequest = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero)));
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(everyRequest, StaffRole.Sales);
        using var browser = new StaffBrowser(everyRequest);
        await browser.FirstLoginAsync(email, temporary.Password);
        Assert.Equal("staff", (await browser.MeAsync()).GetProperty("kind").GetString());

        using (var scope = everyRequest.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IStaffAccounts>().BlockAsync(email, Token);
        }

        Assert.Equal("none", (await browser.MeAsync()).GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Session_older_than_twelve_hours_is_rejected()
    {
        var clock = new ManualTimeProvider();
        using var moving = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(moving, StaffRole.Sales);
        using var browser = new StaffBrowser(moving);
        await browser.FirstLoginAsync(email, temporary.Password);

        clock.Advance(TimeSpan.FromHours(7));
        Assert.Equal("staff", (await browser.MeAsync()).GetProperty("kind").GetString()); // Renews the 8-hour cookie.
        clock.Advance(TimeSpan.FromHours(5) + TimeSpan.FromMinutes(1));

        Assert.Equal("none", (await browser.MeAsync()).GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Sales);
        using var browser = new StaffBrowser(factory);
        await browser.FirstLoginAsync(email, temporary.Password);

        using var response = await browser.PostAsync("logout", new { });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("none", (await browser.MeAsync()).GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Sales_principal_fails_admin_policy_and_passes_staff_policy()
    {
        var (_, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Sales);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<StaffUser>>()
            .CreateAsync((await users.FindByIdAsync(temporary.StaffId.ToString()))!);
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        Assert.True((await authorization.AuthorizeAsync(principal, IdentityModule.StaffPolicy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(principal, IdentityModule.AdminPolicy)).Succeeded);
    }

    [Fact]
    public async Task Staff_cookie_makes_no_customer_and_customer_cookie_is_not_staff()
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Admin);
        using var browser = new StaffBrowser(factory);
        await browser.PostAsync("login", new { email, password = temporary.Password });
        await browser.PostAsync("password", new { newPassword = StaffBrowser.NewPassword });
        var sharedKey = System.Text.Json.JsonDocument.Parse(await (await browser.GetAsync("two-factor/setup")).Content.ReadAsStringAsync(Token))
            .RootElement.GetProperty("sharedKey").GetString()!;
        using var signedIn = await browser.PostAsync("two-factor/setup", new { code = Totp.Code(sharedKey) });
        var staffCookie = signedIn.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("gb-staff=", StringComparison.Ordinal)).Split(';')[0];

        using (var asStaff = new RequestContext(factory, staffCookie))
        {
            Assert.Null(await asStaff.Customer.FindAsync(Token));
        }

        string customerCookie;
        using (var asCustomer = new RequestContext(factory))
        {
            await asCustomer.Customer.GetOrCreateAsync(Token);
            customerCookie = asCustomer.CookieSet(IdentityModule.CustomerCookieName)!;
        }

        using var client = IdentityTestHelpers.HttpsClient(factory, handleCookies: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/identity/me", UriKind.Relative));
        request.Headers.Add("Cookie", customerCookie);
        using var me = await client.SendAsync(request, Token);
        Assert.Contains("\"customer\"", await me.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_logs_contain_no_email_password_or_code()
    {
        var capture = new LogCapture();
        using var logged = factory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(capture).SetMinimumLevel(LogLevel.Debug)));
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(logged, StaffRole.Admin);
        using var browser = new StaffBrowser(logged);

        var (_, codes) = await browser.FirstLoginAsync(email, temporary.Password);
        await browser.PostAsync("logout", new { });
        await browser.PostAsync("login", new { email, password = "fel lösenord som är långt" });
        await browser.PostAsync("login", new { email, password = StaffBrowser.NewPassword });
        await browser.PostAsync("two-factor", new { code = codes[0], recovery = true });

        Assert.NotEmpty(capture.Messages);
        string[] secrets = [email, temporary.Password, StaffBrowser.NewPassword, "fel lösenord som är långt", .. codes];
        foreach (var message in capture.Messages)
        {
            foreach (var secret in secrets)
            {
                Assert.DoesNotContain(secret, message, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>A staff member who has finished the first login and has two-factor on. Their password is <see cref="StaffBrowser.NewPassword"/>.</summary>
    private async Task<string> ReadyStaffAsync()
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(factory, StaffRole.Sales);
        using var browser = new StaffBrowser(factory);
        await browser.FirstLoginAsync(email, temporary.Password);
        return email;
    }
}
