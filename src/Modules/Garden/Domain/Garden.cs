using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Garden.Domain;

/// <summary>A customer's garden. Photos, plot and address come in later sub-projects (8 and 12).</summary>
internal sealed class Garden
{
    private Garden()
    {
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Garden Create(Guid customerId, DateTimeOffset now) =>
        new() { Id = Ids.New(), CustomerId = customerId, CreatedAt = now };
}
