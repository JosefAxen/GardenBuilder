using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Ai.Features.DreamImages;

/// <summary>
/// Which of the customer's photos go along with a dream image as references: the ones taken most nearly in the view's
/// direction, so the image model sees the real house and surroundings from about the same side.
/// </summary>
internal static class DreamPhotos
{
    public const int Max = 2;

    /// <summary>A photo looking more than this far away from the view shows another side of the garden.</summary>
    public const double MaxHeadingDifference = 120;

    /// <summary>
    /// Up to two photo ids, best first. Only photos with a camera pose are used when any has one; when none has, the first
    /// two uploaded. Nothing when the customer unticked "Skicka med mina foton".
    /// </summary>
    public static IReadOnlyList<Guid> Choose(SceneView view, IReadOnlyList<GardenPhotoInfo> photos, bool sendPhotos)
    {
        if (!sendPhotos)
        {
            return [];
        }

        var posed = photos.Where(p => p.Camera is not null).ToList();
        if (posed.Count == 0)
        {
            return [.. photos.Take(Max).Select(p => p.Id)];
        }

        var heading = Heading(view);
        var eye = new Point2(view.Eye.X, view.Eye.Y);
        return
        [
            .. posed
                .Select(p => (p.Id, Difference: HeadingDifference(heading, p.Camera!.HeadingDegrees), Distance: eye.DistanceTo(p.Camera.Position)))
                .Where(p => p.Difference <= MaxHeadingDifference)
                .OrderBy(p => p.Difference)
                .ThenBy(p => p.Distance)
                .Take(Max)
                .Select(p => p.Id),
        ];
    }

    /// <summary>The compass direction from the eye to the target: 0 is north, 90 east.</summary>
    public static double Heading(SceneView view)
    {
        var degrees = Math.Atan2(view.Target.X - view.Eye.X, view.Target.Y - view.Eye.Y) * 180 / Math.PI;
        return (degrees + 360) % 360;
    }

    /// <summary>The smaller angle between two compass directions, 0–180.</summary>
    private static double HeadingDifference(double a, double b)
    {
        var difference = Math.Abs(a - b) % 360;
        return difference > 180 ? 360 - difference : difference;
    }
}
