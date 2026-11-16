using GardenBuilder.Contracts.Identity;
using GardenBuilder.StaffAdmin;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>The staff tool's command logic against a real database. Has its own database (its own fixture).</summary>
public sealed class StaffAdminToolTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly string _secrets = Path.Combine(Path.GetTempPath(), "gardenbuilder-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_writes_the_password_to_a_file_and_not_to_the_output()
    {
        var (exit, output, _) = await RunAsync("create", "anna@firma.se", "Anna Andersson", "Administratör");

        Assert.Equal(0, exit);
        var file = Assert.Single(Directory.GetFiles(_secrets, "*.txt"));
        var password = (await File.ReadAllTextAsync(file, Token)).Trim();
        Assert.Equal(20, password.Length);
        Assert.DoesNotContain(password, output, StringComparison.Ordinal);
        Assert.StartsWith("Skapade Anna Andersson (Administratör). Det tillfälliga lösenordet finns i ", output, StringComparison.Ordinal);
        Assert.Contains(file, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_role_is_wrong_usage()
    {
        var (exit, _, error) = await RunAsync("create", "bertil@firma.se", "Bertil", "chef");

        Assert.Equal(2, exit);
        Assert.Contains("Användning:", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reset_of_unknown_email_says_so()
    {
        var (exit, _, error) = await RunAsync("reset", "ingen@firma.se");

        Assert.Equal(1, exit);
        Assert.Contains("Hittade inget konto med den e-postadressen.", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Taken_email_says_so()
    {
        await RunAsync("create", "cecilia@firma.se", "Cecilia", "säljare");

        var (exit, _, error) = await RunAsync("create", "Cecilia@Firma.se", "Cecilia", "säljare");

        Assert.Equal(1, exit);
        Assert.Contains("Det finns redan ett konto med den e-postadressen.", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reset_block_and_unblock_print_what_happened()
    {
        await RunAsync("create", "erik@firma.se", "Erik", "säljare");

        var reset = await RunAsync("reset", "erik@firma.se");
        var block = await RunAsync("block", "erik@firma.se");
        var unblock = await RunAsync("unblock", "erik@firma.se");

        Assert.Equal(0, reset.Exit);
        Assert.StartsWith("Återställde kontot. Det tillfälliga lösenordet finns i ", reset.Output, StringComparison.Ordinal);
        Assert.Equal("Spärrade kontot.", block.Output.Trim());
        Assert.Equal("Öppnade kontot.", unblock.Output.Trim());
    }

    [Fact]
    public async Task List_prints_no_email()
    {
        await RunAsync("create", "david@firma.se", "David Dahl", "säljare");

        var (exit, output, _) = await RunAsync("list");

        Assert.Equal(0, exit);
        Assert.Contains("David Dahl", output, StringComparison.Ordinal);
        Assert.Contains("Säljare", output, StringComparison.Ordinal);
        Assert.Contains("tvåfaktor: nej", output, StringComparison.Ordinal);
        Assert.DoesNotContain("@", output, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_secrets))
        {
            Directory.Delete(_secrets, recursive: true);
        }
    }

    private async Task<(int Exit, string Output, string Error)> RunAsync(params string[] args)
    {
        using var scope = factory.Services.CreateScope();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var command = new StaffAdminCommand(scope.ServiceProvider.GetRequiredService<IStaffAccounts>(), output, error);

        var exit = await command.RunAsync([.. args, "--secrets", _secrets], Token);

        return (exit, output.ToString(), error.ToString());
    }
}
