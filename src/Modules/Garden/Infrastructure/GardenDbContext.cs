using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Garden.Infrastructure;

internal sealed class GardenDbContext(DbContextOptions<GardenDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(GardenModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GardenDbContext).Assembly);
    }
}
