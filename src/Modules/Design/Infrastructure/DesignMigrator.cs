using GardenBuilder.Contracts.Database;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Design.Infrastructure;

internal sealed class DesignMigrator(DesignDbContext db) : IModuleMigrator
{
    public string ModuleName => "Design";

    public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
