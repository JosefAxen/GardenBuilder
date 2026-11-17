namespace GardenBuilder.Contracts.Identity;

/// <summary>
/// The authorization policies for signed-in staff, set up by the Identity module. Other modules use these names to
/// protect their endpoints without referencing Identity.
/// </summary>
public static class StaffPolicies
{
    /// <summary>Säljare and administratörer.</summary>
    public const string Staff = "Staff";

    /// <summary>Administratörer only.</summary>
    public const string Admin = "Admin";
}
