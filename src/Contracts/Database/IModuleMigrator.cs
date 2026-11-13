namespace GardenBuilder.Contracts.Database;

/// <summary>
/// Applies one module's database migrations. Each module registers one; the Migrator host runs them all
/// as a separate step before new code starts (docs/design.md, "Driftsättning").
/// </summary>
public interface IModuleMigrator
{
    string ModuleName { get; }

    Task MigrateAsync(CancellationToken cancellationToken);
}
