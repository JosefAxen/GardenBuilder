#pragma warning disable ASPIREINTERACTION001 // The dashboard's input dialogs; checked at build time below if still experimental.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.AppHost;

/// <summary>
/// The staff tool in the dashboard: buttons that ask for e-mail, name and role, then run tools/StaffAdmin with them.
/// Pressing the resource's own Start runs "list". The temporary password goes to .secrets/staff, never to the console.
/// </summary>
internal static class StaffAdminResource
{
    public static IResourceBuilder<ProjectResource> AddStaffAdmin(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<PostgresDatabaseResource> database,
        IResourceBuilder<ProjectResource> migrator)
    {
        var secrets = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", ".secrets", "staff"));

        // What the next start should do. Set by a button just before it starts the tool; "list" for a plain Start.
        string[] next = ["list"];

        IResourceBuilder<ProjectResource> staffAdmin = null!;
        staffAdmin = builder.AddProject<Projects.GardenBuilder_StaffAdmin>("staff-admin")
            .WithReference(database).WaitForCompletion(migrator)
            .WithArgs(context =>
            {
                foreach (var arg in next)
                {
                    context.Args.Add(arg);
                }

                context.Args.Add("--secrets");
                context.Args.Add(secrets);
                next = ["list"];
            })
            .WithExplicitStart();

        staffAdmin
            .WithCommand("staff-create", "Skapa personal", context => RunAsync(context, "Skapa personal",
                [
                    Text("email", "E-post"),
                    Text("name", "Namn"),
                    new InteractionInput
                    {
                        Name = "role",
                        Label = "Roll",
                        InputType = InputType.Choice,
                        Required = true,
                        Options = [new("säljare", "Säljare"), new("administratör", "Administratör")],
                    },
                ],
                inputs => ["create", inputs["email"].Value!, inputs["name"].Value!, inputs["role"].Value!]))
            .WithCommand("staff-reset", "Återställ personal", context => RunAsync(context, "Återställ personal", [Text("email", "E-post")], inputs => ["reset", inputs["email"].Value!]))
            .WithCommand("staff-block", "Spärra personal", context => RunAsync(context, "Spärra personal", [Text("email", "E-post")], inputs => ["block", inputs["email"].Value!]))
            .WithCommand("staff-unblock", "Öppna personal", context => RunAsync(context, "Öppna personal", [Text("email", "E-post")], inputs => ["unblock", inputs["email"].Value!]));

        return staffAdmin;

        async Task<ExecuteCommandResult> RunAsync(
            ExecuteCommandContext context,
            string title,
            IReadOnlyList<InteractionInput> fields,
            Func<InteractionInputCollection, string[]> toArgs)
        {
            var interaction = context.Services.GetRequiredService<IInteractionService>();
            var answer = await interaction.PromptInputsAsync(title, "Svaret visas i resursens konsol. Lösenord skrivs till en fil, aldrig till konsolen.", fields, cancellationToken: context.CancellationToken);
            if (answer.Canceled)
            {
                return CommandResults.Canceled();
            }

            next = toArgs(answer.Data!);
            await context.Services.GetRequiredService<ResourceCommandService>()
                .ExecuteCommandAsync(staffAdmin.Resource, KnownResourceCommands.StartCommand, context.CancellationToken);
            return CommandResults.Success();
        }
    }

    private static InteractionInput Text(string name, string label) =>
        new() { Name = name, Label = label, InputType = InputType.Text, Required = true };
}
