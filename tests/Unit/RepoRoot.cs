namespace GardenBuilder.UnitTests;

/// <summary>Finds the repository root, for tests that read files shared with the web client.</summary>
internal static class RepoRoot
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "GardenBuilder.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not find GardenBuilder.slnx above " + AppContext.BaseDirectory);
    }
}
