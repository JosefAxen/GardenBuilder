namespace GardenBuilder.Modules.Catalog.Domain;

/// <summary>The light a plant thrives in. Plant lists often say "sun to part shade", so several can be set.</summary>
[Flags]
internal enum Light
{
    None = 0,
    Sun = 1,
    PartShade = 2,
    Shade = 4,
}
