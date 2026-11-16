using System.Text.Json.Serialization;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Identity.Features.StaffLogin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Identity.Features.WhoAmI;

/// <summary>Who the browser is: staff, a customer, or nobody. The web asks this instead of storing anything itself.</summary>
internal static class WhoAmI
{
    /// <param name="Kind">"none", "customer" or "staff".</param>
    /// <param name="Role">For staff: "sales" or "admin".</param>
    public sealed record MeResponse(
        string Kind,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Role = null);

    public static RouteGroupBuilder MapWhoAmI(this RouteGroupBuilder group)
    {
        group.MapGet("/me", HandleAsync).WithName("WhoAmI");
        return group;
    }

    private static async Task<Ok<MeResponse>> HandleAsync(HttpContext http, ICurrentCustomer customer, CancellationToken cancellationToken)
    {
        // Only the full staff cookie counts: a right password without two-factor is still nobody.
        var staff = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (staff.Succeeded)
        {
            return TypedResults.Ok(new MeResponse("staff", staff.Principal.FindFirst(StaffClaims.Name)?.Value, StaffAuthentication.RoleKey(staff.Principal)));
        }

        return TypedResults.Ok(await customer.FindAsync(cancellationToken) is null ? new MeResponse("none") : new MeResponse("customer"));
    }
}
