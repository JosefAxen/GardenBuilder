using System.Globalization;
using GardenBuilder.Contracts.Identity;

namespace GardenBuilder.StaffAdmin;

/// <summary>
/// The staff tool's commands. A temporary password is written to a file in the secrets folder and never to the output:
/// the Aspire dashboard keeps and shows everything a resource prints (identity spec, "Kommandot för personal").
/// </summary>
public sealed class StaffAdminCommand(IStaffAccounts accounts, TextWriter output, TextWriter error)
{
    public const string Usage =
        "Användning: create <e-post> <namn> <säljare|administratör> | reset <e-post> | block <e-post> | unblock <e-post> | list  [--secrets <mapp>]";

    private const string DefaultSecretsFolder = ".secrets/staff";

    private static readonly TimeZoneInfo Sweden = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    /// <returns>0 done, 1 the account could not be changed, 2 wrong usage.</returns>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        var (secrets, command) = SplitSecretsOption(args);

        try
        {
            switch (command)
            {
                case ["create", var email, var name, var roleText] when ParseRole(roleText) is { } role:
                    var created = await accounts.CreateAsync(email, name, role, cancellationToken);
                    var createdFile = await WritePasswordAsync(secrets, created, cancellationToken);
                    await output.WriteLineAsync(
                        $"Skapade {name.Trim()} ({RoleName(role)}). Det tillfälliga lösenordet finns i {createdFile}. " +
                        $"Lämna det till personen och radera filen. Det gäller till {SwedishTime(created.ExpiresAt)}.");
                    return 0;

                case ["reset", var email]:
                    var reset = await accounts.ResetAsync(email, cancellationToken);
                    var resetFile = await WritePasswordAsync(secrets, reset, cancellationToken);
                    await output.WriteLineAsync($"Återställde kontot. Det tillfälliga lösenordet finns i {resetFile}. Personen gör om första inloggningen.");
                    return 0;

                case ["block", var email]:
                    await accounts.BlockAsync(email, cancellationToken);
                    await output.WriteLineAsync("Spärrade kontot.");
                    return 0;

                case ["unblock", var email]:
                    await accounts.UnblockAsync(email, cancellationToken);
                    await output.WriteLineAsync("Öppnade kontot.");
                    return 0;

                case ["list"]:
                    await ListAsync(cancellationToken);
                    return 0;

                default:
                    await error.WriteLineAsync(Usage);
                    return 2;
            }
        }
        catch (StaffAccountException exception)
        {
            await error.WriteLineAsync(exception.Code switch
            {
                "email-taken" => "Det finns redan ett konto med den e-postadressen.",
                "invalid-email" => "E-postadressen är inte giltig.",
                _ => "Hittade inget konto med den e-postadressen.",
            });
            return 1;
        }
    }

    private static (string Secrets, string[] Command) SplitSecretsOption(string[] args)
    {
        var index = Array.IndexOf(args, "--secrets");
        return index >= 0 && index + 1 < args.Length
            ? (args[index + 1], [.. args[..index], .. args[(index + 2)..]])
            : (DefaultSecretsFolder, args);
    }

    private static StaffRole? ParseRole(string text) => text.Trim().ToLowerInvariant() switch
    {
        "säljare" => StaffRole.Sales,
        "administratör" => StaffRole.Admin,
        _ => null,
    };

    private static string RoleName(StaffRole role) => role == StaffRole.Admin ? "Administratör" : "Säljare";

    private static string SwedishTime(DateTimeOffset time) =>
        TimeZoneInfo.ConvertTime(time, Sweden).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static async Task<string> WritePasswordAsync(string folder, TemporaryPassword password, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        var file = Path.GetFullPath(Path.Combine(folder, $"{password.StaffId}.txt"));
        await File.WriteAllTextAsync(file, password.Password + Environment.NewLine, cancellationToken);
        return file;
    }

    private async Task ListAsync(CancellationToken cancellationToken)
    {
        var staff = await accounts.ListAsync(cancellationToken);
        if (staff.Count == 0)
        {
            await output.WriteLineAsync("Det finns inga personalkonton ännu.");
            return;
        }

        foreach (var person in staff)
        {
            var status = person.Blocked ? "spärrad" : "aktiv";
            var twoFactor = person.TwoFactorEnabled ? "ja" : "nej";
            await output.WriteLineAsync($"{person.Name}  {RoleName(person.Role)}  {status}  tvåfaktor: {twoFactor}");
        }
    }
}
