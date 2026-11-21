using GardenBuilder.Modules.Ai.Providers;
using ImageMagick;
using Microsoft.Extensions.Options;

namespace GardenBuilder.UnitTests.Ai;

public sealed class TestImageModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TestImageModel Model(int failEvery = 0) =>
        new(Options.Create(new ImagesOptions { TestDelaySeconds = 0, TestFailEvery = failEvery }));

    private static byte[] Render(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.SkyBlue, width, height);
        return image.ToByteArray(MagickFormat.Png);
    }

    private static ImageRequest Request(byte[] reference) => new("En trädgård.", reference, [], 1536, 1024);

    [Fact]
    public async Task The_answer_is_a_jpeg_the_size_of_the_render()
    {
        var response = await Model().EditAsync(Request(Render(300, 200)), Ct);

        using var image = new MagickImage(response.Jpeg);
        Assert.Equal(MagickFormat.Jpeg, image.Format);
        Assert.Equal(300u, image.Width);
        Assert.Equal(200u, image.Height);
        Assert.Equal("test-image", response.Model);
    }

    [Fact]
    public async Task The_answer_has_a_thin_border()
    {
        var response = await Model().EditAsync(Request(Render(300, 200)), Ct);

        using var image = new MagickImage(response.Jpeg);
        var pixel = image.GetPixels().GetPixel(3, 3).ToColor()!;
        var border = new MagickColor(TestImageModel.BorderColor);
        Assert.InRange(Math.Abs(pixel.R - border.R), 0, 24);
        Assert.InRange(Math.Abs(pixel.G - border.G), 0, 24);
        Assert.InRange(Math.Abs(pixel.B - border.B), 0, 24);
    }

    [Fact]
    public async Task With_TestFailEvery_2_the_second_call_fails_on_purpose()
    {
        var model = Model(failEvery: 2);
        var render = Render(300, 200);

        await model.EditAsync(Request(render), Ct);
        var error = await Assert.ThrowsAsync<AiProviderException>(() => model.EditAsync(Request(render), Ct));

        Assert.Equal("Testläget misslyckades med flit.", error.Message);
        Assert.False(error.Retryable);
        await model.EditAsync(Request(render), Ct);
    }
}
