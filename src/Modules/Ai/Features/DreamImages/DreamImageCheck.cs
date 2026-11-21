using ImageMagick;

namespace GardenBuilder.Modules.Ai.Features.DreamImages;

/// <summary>Plain checks on what the image model sent back, before the customer sees it.</summary>
internal static class DreamImageCheck
{
    public const int MinShortSide = 256;
    private const double MinAspect = 4.0 / 3.0;
    private const double MaxAspect = 16.0 / 9.0;
    private const double Tolerance = 1e-9;

    /// <summary>True when the bytes decode as an image at least 256 px on its short side, landscape between 4:3 and 16:9.</summary>
    public static bool Accept(byte[] image)
    {
        if (image.Length == 0)
        {
            return false;
        }

        MagickImageInfo info;
        try
        {
            info = new MagickImageInfo(image);
        }
        catch (MagickException)
        {
            return false;
        }

        if (Math.Min(info.Width, info.Height) < MinShortSide)
        {
            return false;
        }

        var aspect = (double)info.Width / info.Height;
        return aspect >= MinAspect - Tolerance && aspect <= MaxAspect + Tolerance;
    }
}
