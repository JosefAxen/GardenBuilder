using GardenBuilder.Contracts.Gardens;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Garden.Domain;

/// <summary>One re-encoded photo of a garden, stored as a blob, and where on the plot it was taken.</summary>
internal sealed class GardenPhoto
{
    /// <summary>The most photos one garden may have (docs/design.md, "Kundens flöde").</summary>
    public const int MaxPerGarden = 6;

    private GardenPhoto()
    {
    }

    public Guid Id { get; private set; }

    public Guid GardenId { get; private set; }

    public string BlobName { get; private set; } = string.Empty;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public double? CameraX { get; private set; }

    public double? CameraY { get; private set; }

    public double? CameraHeading { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public CameraPose? Camera => CameraX is { } x && CameraY is { } y && CameraHeading is { } heading
        ? new CameraPose(new Point2(x, y), heading)
        : null;

    public static GardenPhoto Create(Guid id, Guid gardenId, int width, int height, DateTimeOffset now) => new()
    {
        Id = id,
        GardenId = gardenId,
        BlobName = $"{gardenId}/{id}.jpg",
        Width = width,
        Height = height,
        CreatedAt = now,
    };

    /// <summary>Sets where the photo was taken. The heading is kept between 0 and 360 degrees.</summary>
    public void PlaceCamera(CameraPose camera)
    {
        CameraX = camera.Position.X;
        CameraY = camera.Position.Y;
        CameraHeading = ((camera.HeadingDegrees % 360) + 360) % 360;
    }
}
