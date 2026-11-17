using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GardenBuilder.Modules.Identity.Features.StaffLogin;

/// <summary>
/// A shortcut for local development only: with Identity:SkipStaffTwoFactor set, staff sign in with their password
/// alone. It works only when the app runs in Development, whatever the setting says elsewhere, so production always
/// asks for the code. Josef's decision 2026-11-17; two-factor is turned back on before launch (see the roadmap).
/// </summary>
internal sealed class StaffTwoFactorShortcut(IConfiguration configuration, IHostEnvironment environment)
{
    public const string Setting = "Identity:SkipStaffTwoFactor";

    public bool Skip => environment.IsDevelopment() && configuration.GetValue<bool>(Setting);
}
