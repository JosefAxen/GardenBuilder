using ImageMagick;

namespace GardenBuilder.Modules.Garden.Features.Photos;

/// <summary>A photo as we store it: JPEG, upright, without metadata.</summary>
internal sealed record EncodedPhoto(byte[] Jpeg, int Width, int Height);

/// <summary>
/// Re-encodes every uploaded photo on the server (docs/design.md, "Uppladdning"): the type is decided by the content,
/// the image is turned upright by its EXIF orientation, shrunk to what the AI uses, and written as a new JPEG without
/// GPS position or any other metadata. The original is never kept.
/// </summary>
internal static class PhotoEncoder
{
    public const int MaxBytes = 15 * 1024 * 1024;

    /// <summary>Claude scales larger images down to about this anyway, so more pixels only cost storage.</summary>
    public const int MaxSide = 1568;

    private const uint Quality = 85;

    private static readonly HashSet<MagickFormat> Accepted =
    [
        MagickFormat.Jpeg, MagickFormat.Jpg, MagickFormat.Pjpeg, MagickFormat.Png, MagickFormat.WebP, MagickFormat.Heic, MagickFormat.Heif, MagickFormat.Avif,
    ];

    /// <summary>The re-encoded photo, or null when the content is not an image type we accept.</summary>
    public static EncodedPhoto? Encode(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        var bytes = buffer.ToArray();

        MagickImageInfo info;
        try
        {
            info = new MagickImageInfo(bytes);
        }
        catch (MagickException)
        {
            return null;
        }

        if (!Accepted.Contains(info.Format))
        {
            return null;
        }

        try
        {
            // Pin the decoder to the detected format, so nothing else in the file can pick another one.
            using var image = new MagickImage(bytes, new MagickReadSettings { Format = info.Format });
            image.AutoOrient();
            if (image.Width > MaxSide || image.Height > MaxSide)
            {
                image.Resize(new MagickGeometry(MaxSide, MaxSide));
            }

            image.Strip();
            image.Quality = Quality;
            var jpeg = image.ToByteArray(MagickFormat.Jpeg);
            return new EncodedPhoto(jpeg, (int)image.Width, (int)image.Height);
        }
        catch (MagickException)
        {
            return null;
        }
    }
}
