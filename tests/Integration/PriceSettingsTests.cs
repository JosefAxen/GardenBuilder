using GardenBuilder.Modules.Pricing.Domain;
using GardenBuilder.Modules.Pricing.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

public sealed class PriceSettingsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Migrated_database_holds_the_default_settings()
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<PriceSettingsStore>();

        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        var expected = PriceSettings.Defaults;
        Assert.Equal(Money.InSek(650m), loaded.HourlyRate);
        Assert.Equal(25m, loaded.VatPercent);
        Assert.Equal((30m, 15m, 5m), (loaded.EstimatedRangePercent, loaded.CustomerRangePercent, loaded.VerifiedRangePercent));
        Assert.Equal((expected.Establishment, expected.Transport, expected.Disposal), (loaded.Establishment, loaded.Transport, loaded.Disposal));
        Assert.Equal(expected.RemovalHours.OrderBy(p => p.Key), loaded.RemovalHours.OrderBy(p => p.Key));
    }
}
