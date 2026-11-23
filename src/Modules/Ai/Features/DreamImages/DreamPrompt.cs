using System.Text;

namespace GardenBuilder.Modules.Ai.Features.DreamImages;

/// <summary>
/// The instruction to the image model, in English. The first image is the 3D render; any later images are the
/// customer's own photos. The model may make it look real but must not change what is planned.
/// </summary>
internal static class DreamPrompt
{
    public const string Version = "dream-image@1";

    public const string KeepObjects =
        "Keep every object exactly where it is in the first image, with the same size and the same count.";

    public const string AddNothing = "Do not add any plants, furniture or buildings that are not in the first image.";

    public const string LaterImages =
        "The later images are photos of the real garden. Use them only for the house, the surroundings, the light and the materials.";

    public static string Text(IReadOnlyList<(string Name, int Count)> items, int photoCount)
    {
        var text = new StringBuilder()
            .AppendLine("Turn the first image, a 3D render of a planned garden, into a realistic photo of a Swedish garden in daylight.")
            .AppendLine(KeepObjects)
            .AppendLine(AddNothing);
        if (photoCount > 0)
        {
            text.AppendLine(LaterImages);
        }

        if (items.Count > 0)
        {
            text.AppendLine("The garden contains:");
            foreach (var (name, count) in items)
            {
                text.Append("- ").Append(name).Append(" × ").Append(count).AppendLine();
            }
        }

        return text.ToString().TrimEnd();
    }
}
