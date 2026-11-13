using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Leads.Infrastructure;

internal sealed class LeadsDbContext(DbContextOptions<LeadsDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(LeadsModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LeadsDbContext).Assembly);
    }
}
