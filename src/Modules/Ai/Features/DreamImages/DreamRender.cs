using ImageMagick;

namespace GardenBuilder.Modules.Ai.Features.DreamImages;

/// <summary>
/// The 3D render the editor uploads. Only a PNG is accepted, decided by its content, at most 8 MB and 4096 px a side;
/// it is re-encoded to JPEG before it is stored, so nothing else in the file survives.
/// </summary>
internal static class DreamRender
{
    public const int MaxBytes = 8 * 1024 * 1024;
    public const int MaxSide = 4096;
    private const uint Quality = 90;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Whether the bytes start like a PNG. Checked by plain code before ImageMagick sees the upload, so it never has to
    /// guess the format of untrusted bytes; its readers for formats such as SVG and MVG can reach files and URLs.
    /// </summary>
    public static bool HasPngSignature(ReadOnlySpan<byte> bytes) => bytes.StartsWith(PngSignature);

    /// <summary>The render as JPEG, or null when it is not a PNG we accept.</summary>
    public static byte[]? Encode(byte[] bytes)
    {
        if (bytes.Length > MaxBytes || !HasPngSignature(bytes))
        {
            return null;
        }

        MagickImageInfo info;
        try
        {
            info = new MagickImageInfo(bytes, new MagickReadSettings { Format = MagickFormat.Png });
        }
        catch (MagickException)
        {
            return null;
        }

        // Checked from the header, before a single pixel is decoded.
        if (info.Format != MagickFormat.Png || info.Width is 0 or > MaxSide || info.Height is 0 or > MaxSide)
        {
            return null;
        }

        try
        {
            using var image = new MagickImage(bytes, new MagickReadSettings { Format = MagickFormat.Png });
            image.Strip();
            image.Quality = Quality;
            return image.ToByteArray(MagickFormat.Jpeg);
        }
        catch (MagickException)
        {
            return null;
        }
    }
}
