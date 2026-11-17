using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Design.Infrastructure;

internal sealed class DesignDbContext(DbContextOptions<DesignDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Design> Designs => Set<Domain.Design>();

    public DbSet<Domain.DesignVersion> DesignVersions => Set<Domain.DesignVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DesignModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DesignDbContext).Assembly);
    }
}
