using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Design.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Design.Features.WorkingCopy;

internal sealed class DesignWorkingCopies(DesignDbContext db) : IDesignWorkingCopies
{
    public async Task<DesignScene?> GetAsync(Guid designId, CancellationToken cancellationToken) =>
        ToScene(await db.Designs.AsNoTracking().SingleOrDefaultAsync(d => d.Id == designId, cancellationToken));

    public async Task<DesignScene?> LatestForGardenAsync(Guid gardenId, CancellationToken cancellationToken) =>
        ToScene(await db.Designs.AsNoTracking()
            .Where(d => d.GardenId == gardenId)
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken));

    private static DesignScene? ToScene(Domain.Design? design) => design is null
        ? null
        : new DesignScene(design.Id, design.GardenId, design.CustomerId, SceneJson.Deserialize(design.WorkingCopy), design.Revision);
}
