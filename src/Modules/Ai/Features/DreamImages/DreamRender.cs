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

    /// <summary>The render as JPEG, or null when it is not a PNG we accept.</summary>
    public static byte[]? Encode(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaxBytes)
        {
            return null;
        }

        MagickImageInfo info;
        try
        {
            info = new MagickImageInfo(bytes);
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
