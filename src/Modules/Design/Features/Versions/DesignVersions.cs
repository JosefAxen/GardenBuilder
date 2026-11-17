using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Design.Domain;
using GardenBuilder.Modules.Design.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GardenBuilder.Modules.Design.Features.Versions;

internal sealed class DesignVersions(DesignDbContext db, TimeProvider time) : IDesignVersions
{
    public async Task<DesignVersionRef?> CreateAsync(
        Guid designId, DesignVersionReason reason, CancellationToken cancellationToken)
    {
        try
        {
            return await FreezeAsync(designId, reason, cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        })
        {
            // Another call took the same number at the same moment. The next number is free now.
            db.ChangeTracker.Clear();
            return await FreezeAsync(designId, reason, cancellationToken);
        }
    }

    public async Task<SceneDocument?> GetAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var scene = await db.DesignVersions.AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => v.Scene)
            .SingleOrDefaultAsync(cancellationToken);
        return scene is null ? null : SceneJson.Deserialize(scene);
    }

    private async Task<DesignVersionRef?> FreezeAsync(
        Guid designId, DesignVersionReason reason, CancellationToken cancellationToken)
    {
        var design = await db.Designs.AsNoTracking().SingleOrDefaultAsync(d => d.Id == designId, cancellationToken);
        if (design is null)
        {
            return null;
        }

        var highest = await db.DesignVersions
            .Where(v => v.DesignId == designId)
            .MaxAsync(v => (int?)v.Number, cancellationToken) ?? 0;
        var version = DesignVersion.Freeze(design, highest + 1, reason, time.GetUtcNow());
        db.DesignVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        return new DesignVersionRef(version.Id, version.Number);
    }
}
