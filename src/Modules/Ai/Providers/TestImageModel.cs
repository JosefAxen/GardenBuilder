using ImageMagick;
using ImageMagick.Drawing;
using Microsoft.Extensions.Options;

namespace GardenBuilder.Modules.Ai.Providers;

/// <summary>
/// Stands in for a real image model until keys exist: returns the render with a thin border and the stamp "Testbild",
/// after a pause, at no cost. It can also fail on purpose so the error path can be tried.
/// </summary>
internal sealed class TestImageModel(IOptions<ImagesOptions> options) : IImageModel
{
    public const string ProviderName = "test";
    public const string ModelName = "test-image";
    public const string FailedOnPurpose = "Testläget misslyckades med flit.";

    /// <summary>Terracotta, the app's accent, so the test image is never mistaken for a real one.</summary>
    public const string BorderColor = "#BC522B";

    private readonly ImagesOptions _options = options.Value;
    private int _calls;

    public string Provider => ProviderName;

    public string ModelFor(string kind) => _options.ModelFor(kind);

    public async Task<ImageResponse> EditAsync(ImageRequest request, CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref _calls);
        if (_options.TestDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.TestDelaySeconds), cancellationToken);
        }

        if (_options.TestFailEvery > 0 && call % _options.TestFailEvery == 0)
        {
            throw new AiProviderException(FailedOnPurpose, retryable: false);
        }

        return new ImageResponse(Stamp(request.Reference), string.IsNullOrEmpty(request.Model) ? ModelName : request.Model);
    }

    private static byte[] Stamp(byte[] reference)
    {
        using var image = new MagickImage(reference);
        var width = (double)image.Width;
        var height = (double)image.Height;
        var border = Math.Max(6, Math.Min(width, height) / 100);
        var accent = new MagickColor(BorderColor);
        var fontSize = Math.Max(14, height / 18);
        var label = border * 3;

        new Drawables()
            .FillColor(accent)
            .Rectangle(0, 0, width, border)
            .Rectangle(0, height - border, width, height)
            .Rectangle(0, 0, border, height)
            .Rectangle(width - border, 0, width, height)
            .Rectangle(label, label, label + fontSize * 5, label + fontSize * 1.6)
            .FillColor(MagickColors.White)
            .FontPointSize(fontSize)
            .Text(label + fontSize * 0.5, label + fontSize * 1.15, "Testbild")
            .Draw(image);

        image.Quality = 90;
        return image.ToByteArray(MagickFormat.Jpeg);
    }
}
