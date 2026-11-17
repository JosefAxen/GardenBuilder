using GardenBuilder.Modules.Catalog;
using GardenBuilder.Modules.Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace GardenBuilder.IntegrationTests;

/// <summary>Has its own database, so it can stop the catalog's migrations halfway and add rows in the old shape.</summary>
public sealed class TypeModelMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await _database.StartAsync();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    private CatalogDbContext Context() => new(new DbContextOptionsBuilder<CatalogDbContext>()
        .UseNpgsql(_database.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, CatalogModule.Schema))
        .UseSnakeCaseNamingConvention()
        .Options);

    [Fact]
    public async Task Old_enum_names_are_rewritten()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261115173140_AddArticleLaborHours", Ct);

        string[] old = ["DeciduousTree", "Conifer", "Shrub", "Perennial", "Pot"];
        foreach (var (name, i) in old.Select((n, i) => (n, i)))
        {
#pragma warning disable EF1002 // The values are the test's own constants.
            await db.Database.ExecuteSqlRawAsync(
                $"""
                INSERT INTO catalog.articles (id, article_number, name, category, unit, stock_status, type_model, scale, color,
                    light, labor_hours, price_amount, price_currency)
                VALUES (gen_random_uuid(), 'A-{i}', 'Test {name}', 'Test', 'Piece', 'InStock', '{name}', 1, '#3f7a3a', 0, 0.5, 100, 'SEK')
                """,
                Ct);
#pragma warning restore EF1002
        }

        await migrator.MigrateAsync(cancellationToken: Ct);

        var stored = await db.Database
            .SqlQueryRaw<string>("SELECT type_model AS \"Value\" FROM catalog.articles ORDER BY article_number")
            .ToListAsync(Ct);
        Assert.Equal(["TreeLarge", "Spruce", "ShrubRound", "PerennialTall", "Pot"], stored);
    }
}
