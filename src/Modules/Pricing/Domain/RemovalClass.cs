namespace GardenBuilder.Modules.Pricing.Domain;

/// <summary>
/// What kind of thing is removed, which decides how long the removal takes. Existing elements often have no article
/// (a lawn found in a photo), so the class comes from the surface material or, when there is one, the article.
/// </summary>
internal enum RemovalClass
{
    DeciduousTree,
    Conifer,
    ShrubOrOther,
    Hedge,
    Fence,
    Grass,
    Paving,
    Gravel,
    Decking,
    Bed,
}

internal static class RemovalClasses
{
    /// <summary>The Swedish name used in the line label, e.g. "Borttagning: gräsmatta".</summary>
    public static string Name(RemovalClass removalClass) => removalClass switch
    {
        RemovalClass.DeciduousTree => "lövträd",
        RemovalClass.Conifer => "barrträd",
        RemovalClass.ShrubOrOther => "buske",
        RemovalClass.Hedge => "häck",
        RemovalClass.Fence => "staket",
        RemovalClass.Grass => "gräsmatta",
        RemovalClass.Paving => "plattor",
        RemovalClass.Gravel => "grus",
        RemovalClass.Decking => "trädäck",
        RemovalClass.Bed => "rabatt",
        _ => throw new ArgumentOutOfRangeException(nameof(removalClass), removalClass, null),
    };
}
