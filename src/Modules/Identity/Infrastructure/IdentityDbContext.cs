using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Identity.Infrastructure;

/// <summary>
/// The identity schema: customers, staff (ASP.NET Core Identity's tables) and the cookie keys. The base class has the
/// same short name as this class, so it is written out in full.
/// </summary>
internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<StaffUser, StaffRoleRecord, Guid>(options), IDataProtectionKeyContext
{
    /// <summary>
    /// The keys that encrypt the login cookies. Stored here so cookies stay valid after a restart and across instances;
    /// otherwise every anonymous customer would lose their garden on each deploy. Encrypted with Key Vault in delprojekt 14.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(IdentityModule.Schema);
        StaffConfiguration.Configure(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
