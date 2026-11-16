namespace GardenBuilder.Contracts.Identity;

/// <summary>Säljare or Administratör (docs/design.md, "Inloggning och åtkomst").</summary>
public enum StaffRole
{
    Sales,
    Admin,
}

/// <summary>A temporary password: it must be changed at the first login and stops working at <see cref="ExpiresAt"/>.</summary>
public sealed record TemporaryPassword(Guid StaffId, string Password, DateTimeOffset ExpiresAt);

/// <summary>One staff member as the list shows them. Deliberately without e-mail or anything secret.</summary>
public sealed record StaffSummary(Guid Id, string Name, StaffRole Role, bool Blocked, bool TwoFactorEnabled, bool MustChangePassword);

/// <summary>Why a staff account could not be changed.</summary>
/// <param name="code">"email-taken", "invalid-email" or "not-found".</param>
public sealed class StaffAccountException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>
/// Creates and manages staff accounts. Used by the staff tool (tools/StaffAdmin); nobody can register themselves.
/// E-mail addresses are compared without case and surrounding spaces.
/// </summary>
public interface IStaffAccounts
{
    Task<TemporaryPassword> CreateAsync(string email, string name, StaffRole role, CancellationToken cancellationToken);

    /// <summary>New temporary password; removes two-factor and recovery codes, unlocks the account and ends its sessions.</summary>
    Task<TemporaryPassword> ResetAsync(string email, CancellationToken cancellationToken);

    /// <summary>Stops the person from logging in, and ends their open sessions.</summary>
    Task BlockAsync(string email, CancellationToken cancellationToken);

    Task UnblockAsync(string email, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffSummary>> ListAsync(CancellationToken cancellationToken);
}
