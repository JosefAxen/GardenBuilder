using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Identity.Infrastructure;

/// <summary>ASP.NET Core Identity's tables, renamed so they say they are about staff, and the two roles.</summary>
internal static class StaffConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StaffUser>(b =>
        {
            b.ToTable("staff_users");
            b.Property(u => u.Name).HasMaxLength(200);
        });
        modelBuilder.Entity<StaffRoleRecord>(b =>
        {
            b.ToTable("staff_roles");
            b.HasData(
                new StaffRoleRecord { Id = StaffRoles.SalesId, Name = StaffRoles.Sales, NormalizedName = "SALES", ConcurrencyStamp = "sales" },
                new StaffRoleRecord { Id = StaffRoles.AdminId, Name = StaffRoles.Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "admin" });
        });
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("staff_user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("staff_user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("staff_user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("staff_user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("staff_role_claims");
    }
}
