using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Garden.Infrastructure;

internal sealed class GardenDbContext(DbContextOptions<GardenDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Garden> Gardens => Set<Domain.Garden>();

    public DbSet<Domain.GardenPhoto> Photos => Set<Domain.GardenPhoto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(GardenModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GardenDbContext).Assembly);
    }
}
