using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Pricing.Infrastructure;

internal sealed class PricingDbContext(DbContextOptions<PricingDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PricingModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PricingDbContext).Assembly);
    }
}
