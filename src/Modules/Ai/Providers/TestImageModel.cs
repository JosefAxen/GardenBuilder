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

    /// <summary>"TESTBILD" as 5×7 blocks, one string per row, so the stamp needs no font (servers often have none).</summary>
    private static readonly string[][] Letters =
    [
        ["11111", "00100", "00100", "00100", "00100", "00100", "00100"], // T
        ["11111", "10000", "10000", "11110", "10000", "10000", "11111"], // E
        ["01111", "10000", "10000", "01110", "00001", "00001", "11110"], // S
        ["11111", "00100", "00100", "00100", "00100", "00100", "00100"], // T
        ["11110", "10001", "10001", "11110", "10001", "10001", "11110"], // B
        ["11111", "00100", "00100", "00100", "00100", "00100", "11111"], // I
        ["10000", "10000", "10000", "10000", "10000", "10000", "11111"], // L
        ["11110", "10001", "10001", "10001", "10001", "10001", "11110"], // D
    ];

    private static byte[] Stamp(byte[] reference)
    {
        using var image = new MagickImage(reference);
        var width = (double)image.Width;
        var height = (double)image.Height;
        var border = Math.Max(6, Math.Min(width, height) / 100);
        var block = Math.Max(2, Math.Round(height / 120));
        var label = border * 3;

        var draw = new Drawables()
            .FillColor(new MagickColor(BorderColor))
            .Rectangle(0, 0, width, border)
            .Rectangle(0, height - border, width, height)
            .Rectangle(0, 0, border, height)
            .Rectangle(width - border, 0, width, height)
            .Rectangle(label, label, label + block * (Letters.Length * 6 + 3), label + block * 11)
            .FillColor(MagickColors.White);

        for (var letter = 0; letter < Letters.Length; letter++)
        {
            for (var row = 0; row < 7; row++)
            {
                for (var column = 0; column < 5; column++)
                {
                    if (Letters[letter][row][column] != '1') continue;
                    var x = label + block * (2 + letter * 6 + column);
                    var y = label + block * (2 + row);
                    draw.Rectangle(x, y, x + block - 1, y + block - 1);
                }
            }
        }

        draw.Draw(image);
        image.Quality = 90;
        return image.ToByteArray(MagickFormat.Jpeg);
    }
}
