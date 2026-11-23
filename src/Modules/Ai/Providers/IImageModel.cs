namespace GardenBuilder.Modules.Ai.Providers;

/// <summary>
/// A request to turn a 3D render into a photo-like image. <paramref name="Reference"/> is the render (PNG or JPEG);
/// <paramref name="Photos"/> are the customer's own JPEGs, sent as extra references, possibly none.
/// </summary>
/// <param name="Model">Set by AiCalls from <see cref="IImageModel.ModelFor"/>; handlers leave it empty.</param>
internal sealed record ImageRequest(string Prompt, byte[] Reference, IReadOnlyList<byte[]> Photos, int Width, int Height, string Model = "");

/// <summary>The finished image as JPEG and the model that made it.</summary>
internal sealed record ImageResponse(byte[] Jpeg, string Model);

/// <summary>
/// An image model that edits a render, behind our own interface so providers can be compared and swapped
/// (Google, BFL or the test model). Only the Worker calls it.
/// </summary>
internal interface IImageModel
{
    /// <summary>For example "test" or "google".</summary>
    string Provider { get; }

    /// <summary>The model a job of this kind uses, so its price can be checked before calling.</summary>
    string ModelFor(string kind);

    Task<ImageResponse> EditAsync(ImageRequest request, CancellationToken cancellationToken);
}
