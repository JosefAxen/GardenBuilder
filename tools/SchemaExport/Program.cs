using System.Text.Json;
using GardenBuilder.Contracts.Scenes;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/SchemaExport -- <output path for scene.schema.json>");
    return 1;
}

var json = SceneJson.ExportSchema().ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
File.WriteAllText(args[0], json);
Console.WriteLine($"Wrote {Path.GetFullPath(args[0])}");
return 0;
