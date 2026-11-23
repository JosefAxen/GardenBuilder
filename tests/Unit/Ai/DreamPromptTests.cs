using GardenBuilder.Modules.Ai.Features.DreamImages;
using ImageMagick;

namespace GardenBuilder.UnitTests.Ai;

public sealed class DreamPromptTests
{
    [Fact]
    public void The_version_is_dream_image_1()
    {
        Assert.Equal("dream-image@1", DreamPrompt.Version);
    }

    [Fact]
    public void It_lists_the_items_and_asks_to_keep_every_object()
    {
        var text = DreamPrompt.Text([("Tuja Smaragd 40–60 cm", 12), ("Trädäck lärk", 1)], photoCount: 2);

        Assert.Contains("Tuja Smaragd 40–60 cm × 12", text);
        Assert.Contains("Trädäck lärk × 1", text);
        Assert.Contains(DreamPrompt.KeepObjects, text);
        Assert.Contains(DreamPrompt.AddNothing, text);
        Assert.Contains(DreamPrompt.LaterImages, text);
        Assert.Contains("Swedish garden", text);
        Assert.Contains("daylight", text);
    }

    [Fact]
    public void Without_photos_it_does_not_mention_later_images()
    {
        var text = DreamPrompt.Text([("Tuja Smaragd 40–60 cm", 12)], photoCount: 0);

        Assert.DoesNotContain(DreamPrompt.LaterImages, text);
        Assert.DoesNotContain("later image", text, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DreamImageCheckTests
{
    private static byte[] Jpeg(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.ForestGreen, width, height);
        return image.ToByteArray(MagickFormat.Jpeg);
    }

    [Theory]
    [InlineData(1536u, 1024u)]
    [InlineData(1024u, 768u)]
    [InlineData(1920u, 1080u)]
    public void Landscape_images_between_4_3_and_16_9_are_accepted(uint width, uint height)
    {
        Assert.True(DreamImageCheck.Accept(Jpeg(width, height)));
    }

    [Theory]
    [InlineData(200u, 150u)]
    [InlineData(1000u, 1000u)]
    [InlineData(2000u, 1000u)]
    [InlineData(1024u, 1536u)]
    public void Small_square_too_wide_or_upright_images_are_refused(uint width, uint height)
    {
        Assert.False(DreamImageCheck.Accept(Jpeg(width, height)));
    }

    [Fact]
    public void Bytes_that_are_not_an_image_are_refused()
    {
        Assert.False(DreamImageCheck.Accept("inte en bild"u8.ToArray()));
        Assert.False(DreamImageCheck.Accept([]));
    }
}
