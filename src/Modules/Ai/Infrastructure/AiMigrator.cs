using GardenBuilder.Contracts.Database;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Ai.Infrastructure;

internal sealed class AiMigrator(AiDbContext db) : IModuleMigrator
{
    public string ModuleName => "Ai";

    public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
