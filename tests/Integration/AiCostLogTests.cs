using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Costs;
using GardenBuilder.Modules.Ai.Features.Settings;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>The cost log in ai.calls and the settings row in ai.settings.</summary>
public sealed class AiCostLogTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ManualTimeProvider _clock = new();
    private WebApplicationFactory<Program> _app = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(_clock)));
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await db.Set<AiCall>().ExecuteDeleteAsync(Ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ai.settings", Ct);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async Task<T> With<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var scope = _app.Services.CreateScope();
        return await work(scope.ServiceProvider);
    }

    private async Task LogAsync(DateTimeOffset at, decimal kr)
    {
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CostLog>().AddAsync(
            AiCall.Create(null, "connection-test@1", "anthropic", "claude-opus-5-5", at, 1200, 100, 50, Money.InSek(kr), "ok", null), Ct);
    }

    [Fact]
    public async Task Today_counts_from_midnight_in_Stockholm()
    {
        // 22:59 UTC on 17 November is 23:59 in Stockholm; 23:01 UTC is 00:01 on the 18th.
        await LogAsync(new DateTimeOffset(2026, 11, 17, 22, 59, 0, TimeSpan.Zero), 7m);
        await LogAsync(new DateTimeOffset(2026, 11, 17, 23, 1, 0, TimeSpan.Zero), 2.5m);
        _clock.Advance(new DateTimeOffset(2026, 11, 17, 23, 30, 0, TimeSpan.Zero) - _clock.GetUtcNow());

        var today = await With(s => s.GetRequiredService<CostLog>().TodayAsync(Ct));

        Assert.Equal(Money.InSek(2.5m), today);
    }

    [Fact]
    public async Task Today_counts_summer_time_from_local_midnight_too()
    {
        // In July Stockholm is UTC+2: 21:59 UTC is 23:59 local, 22:01 UTC is 00:01 the next day.
        await LogAsync(new DateTimeOffset(2026, 7, 1, 21, 59, 0, TimeSpan.Zero), 4m);
        await LogAsync(new DateTimeOffset(2026, 7, 1, 22, 1, 0, TimeSpan.Zero), 1m);
        _clock.Advance(new DateTimeOffset(2026, 7, 1, 23, 0, 0, TimeSpan.Zero) - _clock.GetUtcNow());

        Assert.Equal(Money.InSek(1m), await With(s => s.GetRequiredService<CostLog>().TodayAsync(Ct)));
    }

    [Fact]
    public async Task Settings_default_until_saved_and_then_read_back()
    {
        Assert.Equal(AiSettings.Defaults.DailyBudget, (await With(s => s.GetRequiredService<AiSettingsStore>().GetAsync(Ct))).DailyBudget);

        var changed = AiSettings.Defaults with
        {
            DailyBudget = Money.InSek(300m),
            Quotas = new Dictionary<string, int>(AiSettings.Defaults.Quotas) { [AiJobKinds.PhotoInterpretation] = 2 },
            Enabled = new Dictionary<string, bool>(AiSettings.Defaults.Enabled) { [AiJobKinds.DreamImage] = false },
        };
        await With(async s =>
        {
            await s.GetRequiredService<AiSettingsStore>().SaveAsync(changed, Ct);
            return 0;
        });

        var read = await With(s => s.GetRequiredService<AiSettingsStore>().GetAsync(Ct));
        Assert.Equal(Money.InSek(300m), read.DailyBudget);
        Assert.Equal(2, read.Quotas[AiJobKinds.PhotoInterpretation]);
        Assert.False(read.Enabled[AiJobKinds.DreamImage]);
        Assert.True(read.Enabled[AiJobKinds.PhotoInterpretation]);
    }
}
