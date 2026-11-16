using GardenBuilder.SharedKernel;
using Microsoft.AspNetCore.Identity;

namespace GardenBuilder.Modules.Identity.Domain;

/// <summary>A staff member: an ASP.NET Core Identity user with a name and the temporary-password rules.</summary>
internal sealed class StaffUser : IdentityUser<Guid>
{
    public StaffUser()
    {
        Id = Ids.New();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string Name { get; set; } = string.Empty;

    /// <summary>True after the account is created or reset: the person must choose a password before anything else.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>When the temporary password stops working. Null once the person has chosen their own.</summary>
    public DateTimeOffset? TemporaryPasswordExpiresAt { get; set; }
}

/// <summary>A role row in staff_roles. The names are <see cref="StaffRoles.Sales"/> and <see cref="StaffRoles.Admin"/>.</summary>
internal sealed class StaffRoleRecord : IdentityRole<Guid>
{
}

internal static class StaffRoles
{
    public const string Sales = "Sales";

    public const string Admin = "Admin";

    /// <summary>Fixed ids, so the roles can be seeded by the migration.</summary>
    public static readonly Guid SalesId = Guid.Parse("019a8b2c-0000-7000-8000-000000000001");

    public static readonly Guid AdminId = Guid.Parse("019a8b2c-0000-7000-8000-000000000002");
}
