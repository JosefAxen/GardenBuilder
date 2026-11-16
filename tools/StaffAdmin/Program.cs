using GardenBuilder.Contracts.Identity;
using GardenBuilder.Modules.Identity;

namespace GardenBuilder.StaffAdmin;

/// <summary>
/// Entry point. A named class instead of top-level statements: those create a public Program class, which would clash
/// with the API's Program in the integration tests that reference both.
/// </summary>
internal static class StaffAdminProgram
{
    /// <summary>Usage: see <see cref="StaffAdminCommand.Usage"/>. Connection string: ConnectionStrings__gardenbuilder (set by Aspire).</summary>
    /// <returns>0 done, 1 the account could not be changed, 2 wrong usage.</returns>
    public static async Task<int> Main(string[] args)
    {
        // The command line is ours, not configuration, so the host does not get the arguments.
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.AddIdentityModule();
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        var command = new StaffAdminCommand(scope.ServiceProvider.GetRequiredService<IStaffAccounts>(), Console.Out, Console.Error);
        return await command.RunAsync(args, CancellationToken.None);
    }
}
