using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Design.Domain;

/// <summary>
/// A customer's design of a garden. The working copy is the scene the editor saves into; the revision goes up by one
/// on every save, so a save based on an older revision can be refused instead of overwriting a newer one.
/// </summary>
internal sealed class Design
{
    private Design()
    {
    }

    public Guid Id { get; private set; }

    public Guid GardenId { get; private set; }

    /// <summary>The owner. Kept here so every save can check access without asking the Garden module.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>The scene as JSON (jsonb), written by <see cref="SceneJson.Serialize"/>.</summary>
    public string WorkingCopy { get; private set; } = "";

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Design Create(Guid gardenId, Guid customerId, SceneDocument scene, DateTimeOffset now) => new()
    {
        Id = Ids.New(),
        GardenId = gardenId,
        CustomerId = customerId,
        WorkingCopy = SceneJson.Serialize(scene),
        Revision = 1,
        CreatedAt = now,
        UpdatedAt = now,
    };
}
