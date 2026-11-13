namespace GardenBuilder.Contracts;

/// <summary>Names of shared resources, matching the resource names in the Aspire AppHost.</summary>
public static class ResourceNames
{
    /// <summary>Connection string name for the PostgreSQL database. Each module uses its own schema.</summary>
    public const string Database = "gardenbuilder";

    /// <summary>Connection string name for Blob Storage (photos, dream images, 3D models).</summary>
    public const string Blobs = "blobs";
}
