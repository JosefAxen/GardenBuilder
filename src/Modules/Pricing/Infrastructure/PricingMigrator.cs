using GardenBuilder.Contracts.Database;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Pricing.Infrastructure;

internal sealed class PricingMigrator(PricingDbContext db) : IModuleMigrator
{
    public string ModuleName => "Pricing";

    public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
