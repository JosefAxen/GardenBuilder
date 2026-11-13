namespace GardenBuilder.SharedKernel;

/// <summary>All identities in the system are UUID version 7 (time ordered).</summary>
public static class Ids
{
    public static Guid New() => Guid.CreateVersion7();
}
