using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Design.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Design.Features.WorkingCopy;

/// <summary>What happened to a save of the working copy.</summary>
internal abstract record SaveResult
{
    private SaveResult()
    {
    }

    /// <summary>Saved; the design now has this revision.</summary>
    public sealed record Saved(int Revision) : SaveResult;

    /// <summary>No such design for this customer: it does not exist, or someone else owns it.</summary>
    public sealed record NotFound : SaveResult;

    /// <summary>The save was based on an older revision. Nothing was saved.</summary>
    public sealed record Conflict(int CurrentRevision) : SaveResult;
}

/// <summary>Reads and saves designs. Every read and save checks the owner, so another customer's design is "not found".</summary>
internal sealed class WorkingCopyStore(DesignDbContext db, TimeProvider time)
{
    public async Task<Domain.Design> CreateAsync(
        Guid gardenId, Guid customerId, SceneDocument scene, CancellationToken cancellationToken)
    {
        var design = Domain.Design.Create(gardenId, customerId, scene, time.GetUtcNow());
        db.Designs.Add(design);
        await db.SaveChangesAsync(cancellationToken);
        return design;
    }

    public Task<Domain.Design?> FindAsync(Guid id, Guid customerId, CancellationToken cancellationToken) =>
        db.Designs.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id && d.CustomerId == customerId, cancellationToken);

    public Task<Domain.Design?> LatestAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Designs.AsNoTracking()
            .Where(d => d.CustomerId == customerId)
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Saves only when id, owner and revision all match, in one statement, so two saves can never both win.
    /// When nothing matched, a second read tells "not found" from "someone saved in between".
    /// </summary>
    public async Task<SaveResult> SaveAsync(
        Guid id, Guid customerId, int expectedRevision, SceneDocument scene, CancellationToken cancellationToken)
    {
        var json = SceneJson.Serialize(scene);
        var now = time.GetUtcNow();
        var updated = await db.Designs
            .Where(d => d.Id == id && d.CustomerId == customerId && d.Revision == expectedRevision)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(d => d.WorkingCopy, json)
                    .SetProperty(d => d.Revision, d => d.Revision + 1)
                    .SetProperty(d => d.UpdatedAt, now),
                cancellationToken);
        if (updated == 1)
        {
            return new SaveResult.Saved(expectedRevision + 1);
        }

        var current = await db.Designs.AsNoTracking()
            .Where(d => d.Id == id && d.CustomerId == customerId)
            .Select(d => (int?)d.Revision)
            .SingleOrDefaultAsync(cancellationToken);
        return current is { } revision ? new SaveResult.Conflict(revision) : new SaveResult.NotFound();
    }
}
