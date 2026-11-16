using System.Security.Claims;
using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Identity.Features.StaffLogin;

/// <summary>First login, step three: connect the authenticator app, confirm with a code, get the recovery codes.</summary>
internal static class SetUpTwoFactor
{
    private const string Issuer = "Drömträdgård";
    private const int RecoveryCodeCount = 10;

    /// <param name="SharedKey">The key in groups of four, for typing into the app when the QR code cannot be scanned.</param>
    /// <param name="AuthenticatorUri">What the QR code holds.</param>
    public sealed record SetupResponse(string SharedKey, string AuthenticatorUri);

    public sealed record CodeRequest(string? Code);

    public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

    public static RouteGroupBuilder MapSetUpTwoFactor(this RouteGroupBuilder group)
    {
        group.MapGet("/two-factor/setup", GetAsync).WithName("StaffTwoFactorSetup");
        group.MapPost("/two-factor/setup", ConfirmAsync).WithName("StaffConfirmTwoFactor");
        return group;
    }

    private static async Task<IResult> GetAsync(HttpContext http, UserManager<StaffUser> users)
    {
        if (await StaffAuthentication.SetupUserAsync(http, users) is not { MustChangePassword: false } user)
        {
            return StaffAuthentication.SessionExpired();
        }

        // Only reads: the key was made when the setup session started (see StaffAuthentication.StartSetupAsync).
        if (await users.GetAuthenticatorKeyAsync(user) is not { Length: > 0 } key)
        {
            return StaffAuthentication.SessionExpired();
        }

        var issuer = Uri.EscapeDataString(Issuer);
        var uri = $"otpauth://totp/{issuer}:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer={issuer}&digits=6";
        return TypedResults.Ok(new SetupResponse(Grouped(key), uri));
    }

    private static async Task<IResult> ConfirmAsync(CodeRequest request, HttpContext http, UserManager<StaffUser> users, SignInManager<StaffUser> signIn)
    {
        if (await StaffAuthentication.SetupUserAsync(http, users) is not { MustChangePassword: false } user)
        {
            return StaffAuthentication.SessionExpired();
        }

        var code = StaffAuthentication.CleanCode(request.Code, recovery: false);
        if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            return StaffAuthentication.Error("invalid-code");
        }

        await users.SetTwoFactorEnabledAsync(user, true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount) ?? [];

        // Done: the setup session ends and the real session starts, marked as signed in with two factors.
        await http.SignOutAsync(StaffAuthentication.SetupScheme);
        await signIn.SignInWithClaimsAsync(user, isPersistent: false, [new Claim("amr", "mfa")]);
        return TypedResults.Ok(new RecoveryCodesResponse([.. codes]));
    }

    private static string Grouped(string key) =>
        string.Join(' ', key.ToLowerInvariant().Chunk(4).Select(chunk => new string(chunk)));
}
