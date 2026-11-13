using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Design.Infrastructure;

internal sealed class DesignDbContext(DbContextOptions<DesignDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DesignModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DesignDbContext).Assembly);
    }
}
