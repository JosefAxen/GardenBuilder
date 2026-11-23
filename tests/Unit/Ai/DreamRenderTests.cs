using System.Text;
using GardenBuilder.Modules.Ai.Features.DreamImages;
using ImageMagick;

namespace GardenBuilder.UnitTests.Ai;

public sealed class DreamRenderTests
{
    private static byte[] Png(int width, int height)
    {
        using var image = new MagickImage(MagickColors.ForestGreen, (uint)width, (uint)height);
        return image.ToByteArray(MagickFormat.Png);
    }

    [Fact]
    public void A_png_is_signed_as_one()
    {
        Assert.True(DreamRender.HasPngSignature(Png(4, 4)));
    }

    // Uploads are untrusted: ImageMagick must never be asked to guess what an SVG or MVG file is, since its readers for
    // those formats can reach files and URLs. The signature is checked first, by plain code.
    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"file:///etc/passwd\"/></svg>")]
    [InlineData("push graphic-context\nviewbox 0 0 640 480\nimage over 0,0 0,0 'https://example.invalid/x.png'\npop graphic-context")]
    [InlineData("\u0089PNG")]
    public void Anything_without_the_full_png_signature_is_not_a_png(string content)
    {
        Assert.False(DreamRender.HasPngSignature(Encoding.UTF8.GetBytes(content)));
    }

    [Fact]
    public void A_png_render_becomes_a_jpeg()
    {
        var jpeg = DreamRender.Encode(Png(64, 32));

        Assert.NotNull(jpeg);
        Assert.Equal(MagickFormat.Jpeg, new MagickImageInfo(jpeg).Format);
    }
}
