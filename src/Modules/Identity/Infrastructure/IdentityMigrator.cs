using GardenBuilder.Contracts.Database;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Identity.Infrastructure;

internal sealed class IdentityMigrator(IdentityDbContext db) : IModuleMigrator
{
    public string ModuleName => "Identity";

    public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
