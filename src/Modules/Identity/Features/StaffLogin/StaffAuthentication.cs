using System.Security.Claims;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GardenBuilder.Modules.Identity.Features.StaffLogin;

/// <summary>
/// The three staff cookies and the two policies. A staff member moves through them like this:
/// password → <see cref="SetupScheme"/> (first login: new password, then two-factor) or the two-factor cookie
/// (later logins: a code) → the application cookie, which is the only one that counts as signed in.
/// </summary>
internal static class StaffAuthentication
{
    /// <summary>First login in progress: the password was right, but a new password or two-factor is still missing.</summary>
    public const string SetupScheme = "StaffSetup";

    public const string StaffCookieName = "gb-staff";

    /// <summary>When the person signed in with two-factor. Kept when the cookie is renewed, so 12 hours is a hard limit.</summary>
    private const string SignedInAtKey = "gb.signedInAt";

    private static readonly TimeSpan SessionLength = TimeSpan.FromHours(8);
    private static readonly TimeSpan MaxSessionLength = TimeSpan.FromHours(12);

    public static void AddStaffAuthentication(this IServiceCollection services, AuthenticationBuilder authentication)
    {
        services.AddSingleton<StaffTwoFactorShortcut>();
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        authentication.AddIdentityCookies(cookies =>
        {
            cookies.ApplicationCookie!.Configure(o =>
            {
                Harden(o, StaffCookieName, SessionLength);
                o.SlidingExpiration = true;
                o.Events.OnSigningIn = context =>
                {
                    // A renewal keeps the original time; only a fresh two-factor sign-in sets it.
                    context.Properties.Items.TryAdd(SignedInAtKey, context.Options.TimeProvider!.GetUtcNow().ToString("O"));
                    return Task.CompletedTask;
                };
                o.Events.OnValidatePrincipal = async context =>
                {
                    var signedInAt = context.Properties.Items.TryGetValue(SignedInAtKey, out var text) && DateTimeOffset.TryParse(text, out var at)
                        ? at
                        : DateTimeOffset.MinValue;
                    if (context.Options.TimeProvider!.GetUtcNow() - signedInAt > MaxSessionLength)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                        return;
                    }

                    // Blocked, reset or a new password: the security stamp changed, so the session ends within a minute.
                    await SecurityStampValidator.ValidatePrincipalAsync(context);
                };
            });
            cookies.TwoFactorUserIdCookie!.Configure(o => Harden(o, "gb-staff-2fa", TimeSpan.FromMinutes(5)));
            cookies.TwoFactorRememberMeCookie!.Configure(o => Harden(o, "gb-staff-remember", TimeSpan.FromMinutes(5)));
            cookies.ExternalCookie!.Configure(o => Harden(o, "gb-staff-external", TimeSpan.FromMinutes(5)));
        });
        authentication.AddCookie(SetupScheme, o => Harden(o, "gb-staff-setup", TimeSpan.FromMinutes(15)));

        services.AddAuthorizationBuilder()
            .AddPolicy(IdentityModule.StaffPolicy, p => p
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser()
                .RequireRole(StaffRoles.Sales, StaffRoles.Admin))
            .AddPolicy(IdentityModule.AdminPolicy, p => p
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser()
                .RequireRole(StaffRoles.Admin));
    }

    /// <summary>The setup session's person, or null when there is none or it belongs to an older security stamp.</summary>
    public static async Task<StaffUser?> SetupUserAsync(HttpContext http, UserManager<StaffUser> users)
    {
        var result = await http.AuthenticateAsync(SetupScheme);
        if (!result.Succeeded || result.Principal.FindFirstValue(ClaimTypes.NameIdentifier) is not { } id)
        {
            return null;
        }

        var user = await users.FindByIdAsync(id);
        return user is not null && result.Principal.FindFirstValue(StaffClaims.SecurityStamp) == await users.GetSecurityStampAsync(user)
            ? user
            : null;
    }

    /// <summary>
    /// Starts or refreshes the setup session. Call it again after anything that changes the security stamp. Someone who
    /// has not set up two-factor gets their app key here, before the session records the stamp: the setup page then only
    /// reads the key, so asking for it twice at once cannot make the other request look expired.
    /// </summary>
    public static async Task StartSetupAsync(HttpContext http, UserManager<StaffUser> users, StaffUser user)
    {
        if (!user.TwoFactorEnabled && string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(user)))
        {
            await users.ResetAuthenticatorKeyAsync(user);
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(StaffClaims.SecurityStamp, await users.GetSecurityStampAsync(user) ?? string.Empty)],
            SetupScheme);
        await http.SignInAsync(SetupScheme, new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// "two-factor", "change-password" or "setup-two-factor": where the person goes after a right password. "signed-in"
    /// is given by the steps themselves when the development shortcut signed the person in.
    /// </summary>
    public static string NextStep(StaffUser user) => user.MustChangePassword ? "change-password" : user.TwoFactorEnabled ? "two-factor" : "setup-two-factor";

    public static BadRequest<ErrorResponse> Error(string code) => TypedResults.BadRequest(new ErrorResponse(code));

    public static JsonHttpResult<ErrorResponse> SessionExpired() => TypedResults.Json(new ErrorResponse("session-expired"), statusCode: StatusCodes.Status401Unauthorized);

    /// <summary>App codes are typed or pasted as "123 456"; recovery codes as " abcde-12345 ".</summary>
    public static string CleanCode(string? code, bool recovery) =>
        recovery
            ? (code ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()
            : (code ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

    public static string RoleKey(ClaimsPrincipal principal) => principal.IsInRole(StaffRoles.Admin) ? "admin" : "sales";

    public static StaffRole Role(ClaimsPrincipal principal) => principal.IsInRole(StaffRoles.Admin) ? StaffRole.Admin : StaffRole.Sales;

    private static void Harden(CookieAuthenticationOptions o, string name, TimeSpan lifetime)
    {
        o.Cookie.Name = name;
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.IsEssential = true;
        o.ExpireTimeSpan = lifetime;

        // An API answers 401 and 403; it never redirects to a login page.
        o.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }
}

/// <summary>An error the web turns into Swedish text, like the price endpoint's.</summary>
internal sealed record ErrorResponse(string Code);

internal static class StaffClaims
{
    public const string Name = "gb:name";

    public const string SecurityStamp = "gb:stamp";
}

/// <summary>Adds the display name, so "who am I" can show it without a database call.</summary>
internal sealed class StaffClaimsFactory(UserManager<StaffUser> users, RoleManager<StaffRoleRecord> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<StaffUser, StaffRoleRecord>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(StaffUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(StaffClaims.Name, user.Name));
        return identity;
    }
}

/// <summary>
/// ASP.NET Core Identity's sign-in, with one change: a right password does not sign in a person who must still choose a
/// password or set up two-factor. They get <see cref="NeedsSetup"/> instead, and the login endpoint starts the setup.
/// </summary>
internal sealed class StaffSignInManager(
    UserManager<StaffUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<StaffUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<StaffUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<StaffUser> confirmation,
    StaffTwoFactorShortcut shortcut)
    : SignInManager<StaffUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public static readonly SignInResult NeedsSetup = new NeedsSetupResult();

    /// <summary>
    /// A temporary password must always be changed first. After that, two-factor is asked for or set up, except with the
    /// local development shortcut, which signs the person in with the password alone.
    /// </summary>
    protected override Task<SignInResult> SignInOrTwoFactorAsync(StaffUser user, bool isPersistent, string? loginProvider = null, bool bypassTwoFactor = false)
    {
        if (user.MustChangePassword)
        {
            return Task.FromResult(NeedsSetup);
        }

        if (shortcut.Skip)
        {
            return base.SignInOrTwoFactorAsync(user, isPersistent, loginProvider, bypassTwoFactor: true);
        }

        return user.TwoFactorEnabled
            ? base.SignInOrTwoFactorAsync(user, isPersistent, loginProvider, bypassTwoFactor)
            : Task.FromResult(NeedsSetup);
    }

    private sealed class NeedsSetupResult : SignInResult
    {
    }
}
