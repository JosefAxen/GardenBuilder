using GardenBuilder.Contracts.Database;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Garden.Infrastructure;

internal sealed class GardenMigrator(GardenDbContext db) : IModuleMigrator
{
    public string ModuleName => "Garden";

    public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
