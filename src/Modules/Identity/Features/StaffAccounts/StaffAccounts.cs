using System.Net.Mail;
using System.Security.Cryptography;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Identity.Domain;
using GardenBuilder.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GardenBuilder.Modules.Identity.Features.StaffAccounts;

/// <summary>
/// Creates and manages staff accounts for the staff tool. Logs what was done to which id, never e-mail or passwords
/// (docs/design.md: administrative actions are logged, logs hold no personal data).
/// </summary>
internal sealed partial class StaffAccounts(
    UserManager<StaffUser> users,
    IdentityDbContext db,
    TimeProvider time,
    ILogger<StaffAccounts> logger) : IStaffAccounts
{
    /// <summary>How long a temporary password works (spec: 24 hours).</summary>
    public static readonly TimeSpan TemporaryPasswordLifetime = TimeSpan.FromHours(24);

    private const int TemporaryPasswordLength = 20;

    /// <summary>No 0/O, 1/l/I: the password is read from a file and typed by hand.</summary>
    private const string PasswordCharacters = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // These are ASP.NET Core Identity's own names for where it keeps the authenticator key and the recovery codes.
    private const string IdentityTokenProvider = "[AspNetUserStore]";

    public async Task<TemporaryPassword> CreateAsync(string email, string name, StaffRole role, CancellationToken cancellationToken)
    {
        var address = ValidEmail(email);
        if (await users.FindByEmailAsync(address) is not null)
        {
            throw new StaffAccountException("email-taken");
        }

        var user = new StaffUser { UserName = address, Email = address, Name = name.Trim(), LockoutEnabled = true };
        var password = NewTemporaryPassword(user);
        Check(await users.CreateAsync(user, password.Password));
        Check(await users.AddToRoleAsync(user, RoleName(role)));
        Log.Created(logger, user.Id, role);
        return password;
    }

    public async Task<TemporaryPassword> ResetAsync(string email, CancellationToken cancellationToken)
    {
        var user = await FindAsync(email);
        var password = NewTemporaryPassword(user);
        Check(await users.RemovePasswordAsync(user));
        Check(await users.AddPasswordAsync(user, password.Password));
        Check(await users.SetTwoFactorEnabledAsync(user, false));
        Check(await users.RemoveAuthenticationTokenAsync(user, IdentityTokenProvider, "AuthenticatorKey"));
        Check(await users.RemoveAuthenticationTokenAsync(user, IdentityTokenProvider, "RecoveryCodes"));
        Check(await users.SetLockoutEndDateAsync(user, null));
        Check(await users.ResetAccessFailedCountAsync(user));
        Check(await users.UpdateSecurityStampAsync(user));
        Log.Reset(logger, user.Id);
        return password;
    }

    public async Task BlockAsync(string email, CancellationToken cancellationToken)
    {
        var user = await FindAsync(email);
        Check(await users.SetLockoutEnabledAsync(user, true));
        Check(await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue));
        Check(await users.UpdateSecurityStampAsync(user));
        Log.Blocked(logger, user.Id);
    }

    public async Task UnblockAsync(string email, CancellationToken cancellationToken)
    {
        var user = await FindAsync(email);
        Check(await users.SetLockoutEndDateAsync(user, null));
        Check(await users.ResetAccessFailedCountAsync(user));
        Log.Unblocked(logger, user.Id);
    }

    public async Task<IReadOnlyList<StaffSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            orderby user.Name
            select new { user.Id, user.Name, Role = role.Name, user.LockoutEnd, user.TwoFactorEnabled, user.MustChangePassword })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new StaffSummary(
            r.Id, r.Name, r.Role == StaffRoles.Admin ? StaffRole.Admin : StaffRole.Sales, IsBlocked(r.LockoutEnd), r.TwoFactorEnabled, r.MustChangePassword))];
    }

    /// <summary>Blocked means locked "forever"; a lockout after five wrong passwords lasts 15 minutes.</summary>
    internal static bool IsBlocked(DateTimeOffset? lockoutEnd) => lockoutEnd is { } end && end.Year >= 9999;

    internal static string RoleName(StaffRole role) => role == StaffRole.Admin ? StaffRoles.Admin : StaffRoles.Sales;

    private static string ValidEmail(string email)
    {
        var trimmed = email.Trim();
        return MailAddress.TryCreate(trimmed, out var address) && address.Address == trimmed && !string.IsNullOrEmpty(address.Host)
            ? trimmed.ToLowerInvariant()
            : throw new StaffAccountException("invalid-email");
    }

    private static void Check(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var codes = result.Errors.Select(e => e.Code).ToList();
        throw codes.Any(c => c is "DuplicateEmail" or "DuplicateUserName")
            ? new StaffAccountException("email-taken")
            : codes.Contains("InvalidEmail")
                ? new StaffAccountException("invalid-email")
                : new InvalidOperationException($"ASP.NET Core Identity refused the change: {string.Join(", ", codes)}.");
    }

    private async Task<StaffUser> FindAsync(string email) =>
        await users.FindByEmailAsync(email.Trim()) ?? throw new StaffAccountException("not-found");

    private TemporaryPassword NewTemporaryPassword(StaffUser user)
    {
        var expires = time.GetUtcNow() + TemporaryPasswordLifetime;
        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAt = expires;
        return new TemporaryPassword(user.Id, RandomNumberGenerator.GetString(PasswordCharacters, TemporaryPasswordLength), expires);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Created staff member {StaffId} with role {Role}")]
        public static partial void Created(ILogger logger, Guid staffId, StaffRole role);

        [LoggerMessage(Level = LogLevel.Information, Message = "Reset staff member {StaffId}: new temporary password, two-factor removed")]
        public static partial void Reset(ILogger logger, Guid staffId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Blocked staff member {StaffId}")]
        public static partial void Blocked(ILogger logger, Guid staffId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Unblocked staff member {StaffId}")]
        public static partial void Unblocked(ILogger logger, Guid staffId);
    }
}
