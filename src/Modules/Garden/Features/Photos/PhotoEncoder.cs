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

    /// <summary>
    /// The most pixels a photo may declare. A small file can claim a huge image that takes gigabytes to decode; 50
    /// megapixels is more than any phone camera takes.
    /// </summary>
    public const long MaxPixels = 50_000_000;

    private const uint Quality = 85;

    private static readonly HashSet<MagickFormat> Accepted =
    [
        MagickFormat.Jpeg, MagickFormat.Jpg, MagickFormat.Pjpeg, MagickFormat.Png, MagickFormat.WebP, MagickFormat.Heic, MagickFormat.Heif, MagickFormat.Avif,
    ];

    /// <summary>The most memory ImageMagick may use for pixels before it falls back to disk.</summary>
    public const ulong MemoryLimit = 512UL * 1024 * 1024;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>HEIF brands (ISO/IEC 23008-12) that ImageMagick's HEIC reader decodes.</summary>
    private static readonly string[] HeicBrands = ["heic", "heix", "heim", "heis", "hevc", "hevx", "mif1", "msf1"];

    private static readonly string[] AvifBrands = ["avif", "avis"];

    /// <summary>
    /// Caps ImageMagick's memory and pixel area for the whole process. Called once at startup; the limits are global,
    /// so every module that uses ImageMagick gets them.
    /// </summary>
    public static void ApplyResourceLimits()
    {
        ResourceLimits.Memory = MemoryLimit;
        ResourceLimits.Area = (ulong)MaxPixels;
    }

    /// <summary>
    /// The photo format the bytes start with, or null for anything else. Checked by plain code before ImageMagick sees
    /// the upload, so it never has to guess the format of untrusted bytes; its readers for formats such as SVG, MVG and
    /// MSL can reach local files and URLs.
    /// </summary>
    public static MagickFormat? DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(JpegSignature))
        {
            return MagickFormat.Jpeg;
        }

        if (bytes.StartsWith(PngSignature))
        {
            return MagickFormat.Png;
        }

        // WebP: a RIFF container ("RIFF", 4 size bytes, "WEBP").
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return MagickFormat.WebP;
        }

        // HEIC and AVIF: an ISO base media file whose first box is "ftyp" followed by the major brand.
        if (bytes.Length >= 12 && bytes[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(bytes[8..12]);
            if (HeicBrands.Contains(brand, StringComparer.Ordinal))
            {
                return MagickFormat.Heic;
            }

            if (AvifBrands.Contains(brand, StringComparer.Ordinal))
            {
                return MagickFormat.Avif;
            }
        }

        return null;
    }

    /// <summary>The re-encoded photo, or null when the content is not an image type we accept or is too large.</summary>
    public static EncodedPhoto? Encode(Stream input, long maxPixels = MaxPixels)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        var bytes = buffer.ToArray();

        if (DetectFormat(bytes) is not { } format)
        {
            return null;
        }

        MagickImageInfo info;
        try
        {
            // Pinned to the detected format, so ImageMagick does not guess from the content.
            info = new MagickImageInfo(bytes, new MagickReadSettings { Format = format });
        }
        catch (MagickException)
        {
            return null;
        }

        // Checked from the header, before a single pixel is decoded.
        if (!Accepted.Contains(info.Format) || (long)info.Width * info.Height > maxPixels)
        {
            return null;
        }

        try
        {
            // Pin the decoder to the detected format, so nothing else in the file can pick another one.
            using var image = new MagickImage(bytes, new MagickReadSettings { Format = format });
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
