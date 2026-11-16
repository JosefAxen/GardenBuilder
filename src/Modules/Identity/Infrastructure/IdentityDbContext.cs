using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Identity.Infrastructure;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>
    /// The keys that encrypt the login cookies. Stored here so cookies stay valid after a restart and across instances;
    /// otherwise every anonymous customer would lose their garden on each deploy. Encrypted with Key Vault in delprojekt 14.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(IdentityModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
