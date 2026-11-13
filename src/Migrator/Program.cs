using GardenBuilder.Contracts.Database;
using GardenBuilder.Migrator;
using GardenBuilder.Modules.Ai;
using GardenBuilder.Modules.Catalog;
using GardenBuilder.Modules.Design;
using GardenBuilder.Modules.Garden;
using GardenBuilder.Modules.Identity;
using GardenBuilder.Modules.Leads;
using GardenBuilder.Modules.Pricing;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder
    .AddIdentityModule()
    .AddGardenModule()
    .AddCatalogModule()
    .AddDesignModule()
    .AddPricingModule()
    .AddLeadsModule()
    .AddAiModule();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
using var scope = host.Services.CreateScope();
foreach (var migrator in scope.ServiceProvider.GetServices<IModuleMigrator>())
{
    MigratorLog.Migrating(logger, migrator.ModuleName);
    await migrator.MigrateAsync(CancellationToken.None);
}

MigratorLog.Done(logger);

namespace GardenBuilder.Migrator
{
    internal static partial class MigratorLog
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Migrating module {Module}")]
        public static partial void Migrating(ILogger logger, string module);

        [LoggerMessage(Level = LogLevel.Information, Message = "All migrations applied")]
        public static partial void Done(ILogger logger);
    }
}
