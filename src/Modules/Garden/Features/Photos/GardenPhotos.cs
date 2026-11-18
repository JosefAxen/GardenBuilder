using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Modules.Garden.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Garden.Features.Photos;

internal sealed class GardenPhotos(GardenDbContext db, PhotoStore store) : IGardenPhotos
{
    public async Task<IReadOnlyList<GardenPhotoInfo>> ListAsync(Guid gardenId, CancellationToken cancellationToken)
    {
        var photos = await db.Photos.AsNoTracking()
            .Where(p => p.GardenId == gardenId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);
        return [.. photos.Select(p => new GardenPhotoInfo(p.Id, p.GardenId, p.Width, p.Height, p.Camera))];
    }

    public async Task<byte[]?> ReadAsync(Guid photoId, CancellationToken cancellationToken)
    {
        var blobName = await db.Photos.AsNoTracking()
            .Where(p => p.Id == photoId)
            .Select(p => p.BlobName)
            .SingleOrDefaultAsync(cancellationToken);
        return blobName is null ? null : await store.ReadAsync(blobName, cancellationToken);
    }
}
