using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Ai.Infrastructure;

internal sealed class AiDbContext(DbContextOptions<AiDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AiModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiDbContext).Assembly);
    }
}
