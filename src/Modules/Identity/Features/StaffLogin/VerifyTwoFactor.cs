using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Identity.Features.StaffLogin;

/// <summary>Later logins, step two: the code from the app, or one of the recovery codes.</summary>
internal static class VerifyTwoFactor
{
    /// <param name="Recovery">True when <paramref name="Code"/> is a recovery code.</param>
    public sealed record VerifyRequest(string? Code, bool Recovery);

    public static RouteGroupBuilder MapVerifyTwoFactor(this RouteGroupBuilder group)
    {
        group.MapPost("/two-factor", HandleAsync).WithName("StaffVerifyTwoFactor");
        return group;
    }

    private static async Task<IResult> HandleAsync(VerifyRequest request, SignInManager<StaffUser> signIn)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is not { } user)
        {
            return StaffAuthentication.SessionExpired();
        }

        // Identity checks the lockout for the app code but not for a recovery code, so a person blocked after their
        // password could still get in with one. Checked here for both.
        if (await signIn.UserManager.IsLockedOutAsync(user))
        {
            return StaffAuthentication.Error("locked-out");
        }

        var code = StaffAuthentication.CleanCode(request.Code, request.Recovery);
        var result = request.Recovery
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(code)
            : await signIn.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);

        if (result.Succeeded)
        {
            return TypedResults.NoContent();
        }

        return StaffAuthentication.Error(result.IsLockedOut ? "locked-out" : "invalid-code");
    }
}
