using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace GardenBuilder.Modules.Identity.Features.StaffLogin;

/// <summary>First login, step two: replace the temporary password with the person's own.</summary>
internal static class ChangePassword
{
    public sealed record PasswordRequest(string? NewPassword);

    public static RouteGroupBuilder MapChangePassword(this RouteGroupBuilder group)
    {
        group.MapPost("/password", HandleAsync).WithName("StaffChangePassword");
        return group;
    }

    private static async Task<IResult> HandleAsync(PasswordRequest request, HttpContext http, UserManager<StaffUser> users)
    {
        if (await StaffAuthentication.SetupUserAsync(http, users) is not { } user)
        {
            return StaffAuthentication.SessionExpired();
        }

        if (user.MustChangePassword)
        {
            // The setup session already proves the temporary password, so it is not asked for again.
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var result = await users.ResetPasswordAsync(user, token, request.NewPassword ?? string.Empty);
            if (!result.Succeeded)
            {
                return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordTooShort))
                    ? StaffAuthentication.Error("password-too-short")
                    : throw new InvalidOperationException($"The new password was refused: {string.Join(", ", result.Errors.Select(e => e.Code))}.");
            }

            user.MustChangePassword = false;
            user.TemporaryPasswordExpiresAt = null;
            await users.UpdateAsync(user);

            // The new password changed the security stamp; the setup session follows it.
            await StaffAuthentication.StartSetupAsync(http, users, user);
        }

        return TypedResults.Ok(new Login.NextResponse(StaffAuthentication.NextStep(user)));
    }
}
