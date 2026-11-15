using GardenBuilder.Contracts.Catalog;
using GardenBuilder.Modules.Catalog;

// Usage:
//   import <file.xlsx>   reads the articles into the catalog (connection string: ConnectionStrings__gardenbuilder)
//   example <file.xlsx>  writes an import file with made-up example articles
// Exit codes: 0 done, 1 the file has errors, 2 wrong usage.
if (args is not ["import" or "example", var path])
{
    Console.Error.WriteLine("Användning: import <fil.xlsx> | example <fil.xlsx>");
    return 2;
}

// The command line is ours, not configuration, so the host does not get the arguments.
var builder = Host.CreateApplicationBuilder();
builder.AddServiceDefaults();
builder.AddCatalogModule();
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var import = scope.ServiceProvider.GetRequiredService<ICatalogImport>();

if (args[0] == "example")
{
    using var output = File.Create(path);
    import.WriteExample(output);
    Console.WriteLine($"Skrev exempelfilen {Path.GetFullPath(path)}.");
    return 0;
}

if (!File.Exists(path))
{
    Console.Error.WriteLine($"Filen {Path.GetFullPath(path)} finns inte.");
    return 2;
}

FileStream input;
try
{
    input = File.OpenRead(path);
}
catch (IOException)
{
    // Most often the file is open in Excel, which locks it.
    Console.Error.WriteLine($"Filen {Path.GetFullPath(path)} går inte att öppna. Är den öppen i Excel? Stäng den och försök igen.");
    return 2;
}

await using var disposeInput = input;
var result = await import.ImportAsync(input, CancellationToken.None);
if (!result.Succeeded)
{
    Console.Error.WriteLine($"Ingenting lästes in. Filen har {result.Errors.Count} fel:");
    foreach (var error in result.Errors)
    {
        var column = error.Column.Length > 0 ? $", {error.Column}" : string.Empty;
        Console.Error.WriteLine($"  Rad {error.Row}{column}: {error.Message}");
    }

    return 1;
}

Console.WriteLine($"Klart: {result.Created} nya, {result.Updated} ändrade, {result.Discontinued} utgångna.");
return 0;
