using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>Has its own database (its own fixture). Each test uses its own e-mail addresses.</summary>
public sealed class StaffAccountsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_gives_a_temporary_password_that_must_be_changed()
    {
        using var scope = factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IStaffAccounts>();

        var temporary = await accounts.CreateAsync("anna@firma.se", "Anna Andersson", StaffRole.Admin, Token);

        Assert.True(temporary.Password.Length >= 16, $"The password has only {temporary.Password.Length} characters.");
        Assert.InRange(temporary.ExpiresAt, DateTimeOffset.UtcNow.AddHours(24).AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(24).AddMinutes(1));
        var summary = Assert.Single(await accounts.ListAsync(Token), s => s.Id == temporary.StaffId);
        Assert.Equal(new StaffSummary(temporary.StaffId, "Anna Andersson", StaffRole.Admin, Blocked: false, TwoFactorEnabled: false, MustChangePassword: true), summary);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var user = await users.FindByIdAsync(temporary.StaffId.ToString());
        Assert.True(await users.CheckPasswordAsync(user!, temporary.Password));
    }

    [Fact]
    public async Task Email_is_compared_without_case_or_spaces()
    {
        using var scope = factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IStaffAccounts>();
        await accounts.CreateAsync("bertil@firma.se", "Bertil", StaffRole.Sales, Token);

        var error = await Assert.ThrowsAsync<StaffAccountException>(() => accounts.CreateAsync("  Bertil@Firma.se ", "Bertil igen", StaffRole.Sales, Token));

        Assert.Equal("email-taken", error.Code);
        await accounts.BlockAsync(" BERTIL@firma.se", Token); // Found the same way.
    }

    [Theory]
    [InlineData("anna")]
    [InlineData("")]
    [InlineData("anna@")]
    public async Task Invalid_email_is_rejected(string email)
    {
        using var scope = factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IStaffAccounts>();

        var error = await Assert.ThrowsAsync<StaffAccountException>(() => accounts.CreateAsync(email, "Någon", StaffRole.Sales, Token));

        Assert.Equal("invalid-email", error.Code);
    }

    [Fact]
    public async Task Reset_gives_a_new_password_and_removes_two_factor()
    {
        using var scope = factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IStaffAccounts>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var created = await accounts.CreateAsync("cecilia@firma.se", "Cecilia", StaffRole.Sales, Token);
        var user = (await users.FindByIdAsync(created.StaffId.ToString()))!;
        await users.SetTwoFactorEnabledAsync(user, true);
        await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15));
        var stampBefore = await users.GetSecurityStampAsync(user);

        var reset = await accounts.ResetAsync("cecilia@firma.se", Token);

        using var check = factory.Services.CreateScope();
        var fresh = check.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var after = (await fresh.FindByIdAsync(created.StaffId.ToString()))!;
        Assert.NotEqual(created.Password, reset.Password);
        Assert.True(await fresh.CheckPasswordAsync(after, reset.Password));
        Assert.False(await fresh.CheckPasswordAsync(after, created.Password));
        Assert.False(after.TwoFactorEnabled);
        Assert.Equal(0, await fresh.CountRecoveryCodesAsync(after));
        Assert.Null(await fresh.GetAuthenticatorKeyAsync(after));
        Assert.True(after.MustChangePassword);
        Assert.False(await fresh.IsLockedOutAsync(after));
        Assert.NotEqual(stampBefore, await fresh.GetSecurityStampAsync(after));
    }

    [Fact]
    public async Task Block_and_unblock()
    {
        using var scope = factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IStaffAccounts>();
        var created = await accounts.CreateAsync("david@firma.se", "David", StaffRole.Sales, Token);
        var stampBefore = await StampAsync(created.StaffId);

        await accounts.BlockAsync("david@firma.se", Token);

        Assert.True((await SummaryAsync(created.StaffId)).Blocked);
        Assert.NotEqual(stampBefore, await StampAsync(created.StaffId));

        await accounts.UnblockAsync("david@firma.se", Token);

        Assert.False((await SummaryAsync(created.StaffId)).Blocked);
    }

    [Fact]
    public async Task Unknown_email_is_not_found()
    {
        using var scope = factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IStaffAccounts>();

        Assert.Equal("not-found", (await Assert.ThrowsAsync<StaffAccountException>(() => accounts.ResetAsync("ingen@firma.se", Token))).Code);
        Assert.Equal("not-found", (await Assert.ThrowsAsync<StaffAccountException>(() => accounts.BlockAsync("ingen@firma.se", Token))).Code);
        Assert.Equal("not-found", (await Assert.ThrowsAsync<StaffAccountException>(() => accounts.UnblockAsync("ingen@firma.se", Token))).Code);
    }

    [Fact]
    public async Task List_shows_no_email_or_password()
    {
        using var scope = factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IStaffAccounts>();
        await accounts.CreateAsync("erik@firma.se", "Erik", StaffRole.Admin, Token);

        var list = await accounts.ListAsync(Token);

        Assert.Contains(list, s => s.Name == "Erik" && s.Role == StaffRole.Admin);
        Assert.DoesNotContain(typeof(StaffSummary).GetProperties(), p => p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(list, s => s.ToString().Contains("erik@firma.se", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<StaffSummary> SummaryAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IStaffAccounts>().ListAsync(Token)).Single(s => s.Id == id);
    }

    private async Task<string?> StampAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        return await users.GetSecurityStampAsync((await users.FindByIdAsync(id.ToString()))!);
    }
}
