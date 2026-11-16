using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Identity.Domain;

/// <summary>An anonymous customer: no name, no e-mail, only an id that a secure cookie carries.</summary>
internal sealed class Customer
{
    /// <summary>How often "last seen" is written: once a day is enough for a 90-day retention rule.</summary>
    public static readonly TimeSpan SeenInterval = TimeSpan.FromDays(1);

    private Customer()
    {
    }

    public Guid Id { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Used later to delete what has been inactive for 90 days (docs/design.md, "GDPR").</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    public static Customer Create(DateTimeOffset now) => new() { Id = Ids.New(), CreatedAt = now, LastSeenAt = now };

    /// <summary>Records a visit. Returns true when "last seen" changed, which happens at most once a day.</summary>
    public bool Seen(DateTimeOffset now)
    {
        if (now - LastSeenAt < SeenInterval)
        {
            return false;
        }

        LastSeenAt = now;
        return true;
    }
}
