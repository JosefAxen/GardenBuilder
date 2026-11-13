using GardenBuilder.Contracts.Database;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Leads.Infrastructure;

internal sealed class LeadsMigrator(LeadsDbContext db) : IModuleMigrator
{
    public string ModuleName => "Leads";

    public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
