using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Modules.Garden.Infrastructure;

namespace GardenBuilder.Modules.Garden.Features.CreateGarden;

internal sealed class Gardens(GardenDbContext db, TimeProvider time) : IGardens
{
    public async Task<Guid> CreateAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var garden = Domain.Garden.Create(customerId, time.GetUtcNow());
        db.Gardens.Add(garden);
        await db.SaveChangesAsync(cancellationToken);
        return garden.Id;
    }
}
