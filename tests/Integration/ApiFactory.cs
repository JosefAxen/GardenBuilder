using GardenBuilder.Contracts.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace GardenBuilder.IntegrationTests;

/// <summary>Runs the real API against a real PostgreSQL in Docker. Requires Docker.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Blob Storage for photos. Same image as the AppHost uses (older ones refuse the SDK's API version).</summary>
    private readonly AzuriteContainer _storage = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0").Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _storage.StartAsync());

        // Same migrators as the Migrator host, so tests run against the real schema.
        using var scope = Services.CreateScope();
        foreach (var migrator in scope.ServiceProvider.GetServices<IModuleMigrator>())
        {
            await migrator.MigrateAsync(CancellationToken.None);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
        await _storage.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:gardenbuilder", _database.GetConnectionString());
        builder.UseSetting("ConnectionStrings:blobs", _storage.GetConnectionString());
        // Tests use the real staff login; StaffTwoFactorSkipTests turn the local shortcut on themselves.
        builder.UseSetting("Identity:SkipStaffTwoFactor", "false");
    }
}
