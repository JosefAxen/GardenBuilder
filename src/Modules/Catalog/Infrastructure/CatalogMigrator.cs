using GardenBuilder.Contracts.Database;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Catalog.Infrastructure;

internal sealed class CatalogMigrator(CatalogDbContext db) : IModuleMigrator
{
    public string ModuleName => "Catalog";

    public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
