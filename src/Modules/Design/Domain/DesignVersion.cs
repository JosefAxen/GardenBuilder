using GardenBuilder.Contracts.Designs;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Design.Domain;

/// <summary>A frozen copy of a design's working copy. Never changed after it is created.</summary>
internal sealed class DesignVersion
{
    private DesignVersion()
    {
    }

    public Guid Id { get; private set; }

    public Guid DesignId { get; private set; }

    /// <summary>1, 2, 3 … within the design.</summary>
    public int Number { get; private set; }

    /// <summary>The scene as JSON (jsonb).</summary>
    public string Scene { get; private set; } = "";

    public DesignVersionReason Reason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static DesignVersion Freeze(Design design, int number, DesignVersionReason reason, DateTimeOffset now) => new()
    {
        Id = Ids.New(),
        DesignId = design.Id,
        Number = number,
        Scene = design.WorkingCopy,
        Reason = reason,
        CreatedAt = now,
    };
}
