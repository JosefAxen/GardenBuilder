using System.Net.Http.Json;
using System.Text.Json;
using GardenBuilder.Contracts.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GardenBuilder.IntegrationTests;

/// <summary>
/// Identity:SkipStaffTwoFactor lets staff sign in with only their password while the app runs locally in Development.
/// It never works anywhere else. Two-factor is turned back on before launch (roadmap).
/// </summary>
public sealed class StaffTwoFactorSkipTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private WebApplicationFactory<Program> App(string environment, bool skip) => factory.WithWebHostBuilder(b =>
    {
        b.UseEnvironment(environment);
        b.UseSetting("Identity:SkipStaffTwoFactor", skip ? "true" : "false");
    });

    /// <summary>A staff member who has set up two-factor and signed out again.</summary>
    private static async Task<string> StaffWithTwoFactorAsync(WebApplicationFactory<Program> app)
    {
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(app, StaffRole.Admin);
        using var first = new StaffBrowser(app);
        await first.FirstLoginAsync(email, temporary.Password);
        return email;
    }

    private static async Task<string?> LoginAsync(StaffBrowser browser, string email, string password) =>
        await StaffBrowser.NextAsync(await browser.PostAsync("login", new { email, password }));

    [Fact]
    public async Task In_development_the_password_alone_signs_staff_in()
    {
        var email = await StaffWithTwoFactorAsync(App("Development", skip: false));
        using var app = App("Development", skip: true);
        using var browser = new StaffBrowser(app);

        Assert.Equal("signed-in", await LoginAsync(browser, email, StaffBrowser.NewPassword));

        var me = await browser.MeAsync();
        Assert.Equal("staff", me.GetProperty("kind").GetString());
        using var settings = await browser.Client.GetAsync(new Uri("/api/ai/settings", UriKind.Relative), Ct);
        Assert.True(settings.IsSuccessStatusCode);
    }

    [Fact]
    public async Task In_development_a_new_account_is_signed_in_after_choosing_a_password()
    {
        using var app = App("Development", skip: true);
        var (email, temporary) = await StaffBrowser.CreateStaffAsync(app, StaffRole.Sales);
        using var browser = new StaffBrowser(app);

        Assert.Equal("change-password", await LoginAsync(browser, email, temporary.Password));
        var changed = await browser.PostAsync("password", new { newPassword = StaffBrowser.NewPassword });

        Assert.Equal("signed-in", await StaffBrowser.NextAsync(changed));
        Assert.Equal("staff", (await browser.MeAsync()).GetProperty("kind").GetString());
    }

    [Fact]
    public async Task A_wrong_password_is_still_refused()
    {
        var email = await StaffWithTwoFactorAsync(App("Development", skip: false));
        using var app = App("Development", skip: true);
        using var browser = new StaffBrowser(app);

        using var response = await browser.PostAsync("login", new { email, password = "fel lösenord helt och hållet" });

        Assert.Equal("invalid-credentials", await StaffBrowser.CodeAsync(response));
        Assert.Equal("none", (await browser.MeAsync()).GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Outside_development_two_factor_is_always_asked_for(string environment)
    {
        var email = await StaffWithTwoFactorAsync(App("Development", skip: false));
        using var app = App(environment, skip: true);
        using var browser = new StaffBrowser(app);

        Assert.Equal("two-factor", await LoginAsync(browser, email, StaffBrowser.NewPassword));
        Assert.NotEqual("staff", (await browser.MeAsync()).GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Without_the_setting_development_asks_for_the_code()
    {
        var email = await StaffWithTwoFactorAsync(App("Development", skip: false));
        using var browser = new StaffBrowser(App("Development", skip: false));

        Assert.Equal("two-factor", await LoginAsync(browser, email, StaffBrowser.NewPassword));
    }
}
