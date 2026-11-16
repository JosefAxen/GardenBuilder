using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Identity.Features.StaffLogin;

/// <summary>Removes every staff cookie in this browser. Other browsers end when the security stamp changes.</summary>
internal static class Logout
{
    public static RouteGroupBuilder MapLogout(this RouteGroupBuilder group)
    {
        group.MapPost("/logout", HandleAsync).WithName("StaffLogout");
        return group;
    }

    private static async Task<IResult> HandleAsync(HttpContext http, SignInManager<StaffUser> signIn)
    {
        await signIn.SignOutAsync();
        await http.SignOutAsync(StaffAuthentication.SetupScheme);
        return TypedResults.NoContent();
    }
}
