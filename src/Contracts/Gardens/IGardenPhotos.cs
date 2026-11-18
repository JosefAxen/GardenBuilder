using GardenBuilder.SharedKernel;

namespace GardenBuilder.Contracts.Gardens;

/// <summary>Where a photo was taken: a point on the plot in scene meters, and the direction the camera pointed.</summary>
/// <param name="HeadingDegrees">0 is north, 90 east (clockwise seen from above).</param>
public sealed record CameraPose(Point2 Position, double HeadingDegrees);

/// <summary>One of a garden's photos, already re-encoded. <see cref="Camera"/> is null until the customer has placed it.</summary>
public sealed record GardenPhotoInfo(Guid Id, Guid GardenId, int Width, int Height, CameraPose? Camera);

/// <summary>A garden's photos, for modules such as photo interpretation. Only the Garden module reads the storage.</summary>
public interface IGardenPhotos
{
    Task<IReadOnlyList<GardenPhotoInfo>> ListAsync(Guid gardenId, CancellationToken cancellationToken);

    /// <summary>The re-encoded JPEG, or null when the photo no longer exists.</summary>
    Task<byte[]?> ReadAsync(Guid photoId, CancellationToken cancellationToken);
}
