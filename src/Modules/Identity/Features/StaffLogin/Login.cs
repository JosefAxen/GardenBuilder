using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Identity.Features.StaffLogin;

/// <summary>Step one: e-mail and password. Never signs anyone in by itself; it says which step comes next.</summary>
internal static class Login
{
    private static readonly StaffUser NoOne = new();

    /// <summary>A hash no password matches, made once, so an unknown e-mail costs the same check as a known one.</summary>
    private static string? _noOnesHash;

    public sealed record LoginRequest(string? Email, string? Password);

    /// <param name="Next">"two-factor", "change-password" or "setup-two-factor".</param>
    public sealed record NextResponse(string Next);

    public static RouteGroupBuilder MapLogin(this RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleAsync).WithName("StaffLogin");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        HttpContext http,
        SignInManager<StaffUser> signIn,
        UserManager<StaffUser> users,
        IPasswordHasher<StaffUser> hasher,
        TimeProvider time)
    {
        // A new login starts from scratch, whatever half-finished login this browser had.
        await http.SignOutAsync(StaffAuthentication.SetupScheme);
        await http.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);

        // A wrong e-mail and a wrong password give the same answer, so nobody can test which addresses exist.
        var user = await users.FindByEmailAsync((request.Email ?? string.Empty).Trim());
        if (user is null)
        {
            // The same work as checking a real password, so the time the answer takes does not tell either.
            _noOnesHash ??= hasher.HashPassword(NoOne, Guid.NewGuid().ToString());
            hasher.VerifyHashedPassword(NoOne, _noOnesHash, request.Password ?? string.Empty);
            return StaffAuthentication.Error("invalid-credentials");
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password ?? string.Empty, isPersistent: false, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return StaffAuthentication.Error("locked-out");
        }

        if (result == StaffSignInManager.NeedsSetup)
        {
            // Checked only after the right password, so the answer says nothing to someone guessing.
            if (user.MustChangePassword && user.TemporaryPasswordExpiresAt < time.GetUtcNow())
            {
                return StaffAuthentication.Error("temporary-password-expired");
            }

            await StaffAuthentication.StartSetupAsync(http, users, user);
            return TypedResults.Ok(new NextResponse(StaffAuthentication.NextStep(user)));
        }

        return result.RequiresTwoFactor
            ? TypedResults.Ok(new NextResponse("two-factor"))
            : StaffAuthentication.Error("invalid-credentials");
    }
}
