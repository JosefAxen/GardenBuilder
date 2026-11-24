using GardenBuilder.Modules.Garden.Features.Photos;
using ImageMagick;

namespace GardenBuilder.UnitTests.Garden;

public sealed class PhotoEncoderTests
{
    private static MemoryStream Image(uint width, uint height, MagickFormat format, Action<MagickImage>? change = null)
    {
        using var image = new MagickImage(MagickColors.ForestGreen, width, height);
        change?.Invoke(image);
        var stream = new MemoryStream();
        image.Write(stream, format);
        stream.Position = 0;
        return stream;
    }

    private static MagickImage Read(EncodedPhoto photo) => new(photo.Jpeg);

    [Fact]
    public void A_4000x3000_jpeg_becomes_1568x1176()
    {
        var photo = PhotoEncoder.Encode(Image(4000, 3000, MagickFormat.Jpeg));

        Assert.NotNull(photo);
        Assert.Equal((1568, 1176), (photo.Width, photo.Height));
        using var read = Read(photo);
        Assert.Equal(MagickFormat.Jpeg, read.Format);
        Assert.Equal((1568u, 1176u), (read.Width, read.Height));
    }

    [Fact]
    public void Gps_and_exif_are_removed()
    {
        var input = Image(800, 600, MagickFormat.Jpeg, image =>
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.GPSLatitude, [new Rational(59, 1), new Rational(20, 1), new Rational(0, 1)]);
            exif.SetValue(ExifTag.Make, "Telefon");
            image.SetProfile(exif);
            image.SetAttribute("comment", "Hemma hos oss");
        });

        var photo = PhotoEncoder.Encode(input)!;

        using var read = Read(photo);
        Assert.Null(read.GetExifProfile());
        Assert.Null(read.GetXmpProfile());
        Assert.Null(read.GetIptcProfile());
        Assert.Null(read.GetAttribute("comment"));
    }

    [Fact]
    public void Orientation_6_comes_out_upright()
    {
        // Orientation 6: the phone was held upright, the pixels are stored lying down.
        var input = Image(300, 200, MagickFormat.Jpeg, image =>
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Orientation, (ushort)6);
            image.SetProfile(exif);
            image.Orientation = Orientation.RightTop;
        });

        var photo = PhotoEncoder.Encode(input)!;

        Assert.Equal((200, 300), (photo.Width, photo.Height));
        using var read = Read(photo);
        Assert.NotEqual(Orientation.RightTop, read.Orientation);
    }

    [Fact]
    public void An_image_over_the_pixel_limit_is_refused_before_it_is_decoded()
    {
        // A small file can declare a huge image (30 000 × 30 000 pixels is gigabytes once decoded).
        Assert.Null(PhotoEncoder.Encode(Image(2000, 1000, MagickFormat.Png), maxPixels: 1_000_000));
        Assert.NotNull(PhotoEncoder.Encode(Image(1000, 1000, MagickFormat.Png), maxPixels: 1_000_000));
        Assert.Equal(50_000_000, PhotoEncoder.MaxPixels);
    }

    [Fact]
    public void Small_images_are_not_enlarged()
    {
        var photo = PhotoEncoder.Encode(Image(640, 480, MagickFormat.Jpeg))!;

        Assert.Equal((640, 480), (photo.Width, photo.Height));
    }

    /// <summary>
    /// iPhones save HEIC. Magick.NET reads it but cannot write it, so no HEIC file can be made here; this checks that the
    /// decoder is present on the platform the tests run on (Windows locally, Linux in CI).
    /// </summary>
    [Fact]
    public void Heic_can_be_read_on_this_platform()
    {
        Assert.True(MagickFormatInfo.Create(MagickFormat.Heic)?.SupportsReading);
    }

    [Theory]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.WebP)]
    public void Png_and_webp_are_accepted(MagickFormat format)
    {
        var photo = PhotoEncoder.Encode(Image(400, 300, format));

        Assert.NotNull(photo);
        Assert.Equal((400, 300), (photo.Width, photo.Height));
    }

    [Fact]
    public void Text_renamed_to_jpg_is_refused()
    {
        using var text = new MemoryStream("Det här är inget foto."u8.ToArray());

        Assert.Null(PhotoEncoder.Encode(text));
    }

    [Fact]
    public void A_gif_is_refused()
    {
        Assert.Null(PhotoEncoder.Encode(Image(100, 100, MagickFormat.Gif)));
    }

    // ImageMagick's readers for SVG and MVG can open local files and URLs, so such uploads must be turned away by plain
    // code before ImageMagick sees a single byte.
    private const string Svg =
        """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><image href="file:///etc/passwd"/></svg>""";

    private const string Mvg = "push graphic-context\nviewbox 0 0 10 10\nimage over 0,0 10,10 'https://example.com/x.png'\npop graphic-context\n";

    [Theory]
    [InlineData(Svg)]
    [InlineData(Mvg)]
    [InlineData("<?xml version=\"1.0\"?>" + Svg)]
    public void Svg_and_mvg_are_not_recognised_as_photos(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        Assert.Null(PhotoEncoder.DetectFormat(bytes));
        Assert.Null(PhotoEncoder.Encode(new MemoryStream(bytes)));
    }

    public static TheoryData<byte[]> TruncatedSignatures =>
    new()
    {
        Array.Empty<byte>(),
        new byte[] { 0xFF, 0xD8 },
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A },
        "RIFF\0\0\0\0WEB"u8.ToArray(),
        "\0\0\0\u0018ftyphei"u8.ToArray(),
    };

    [Theory]
    [MemberData(nameof(TruncatedSignatures))]
    public void A_truncated_signature_is_not_recognised(byte[] bytes)
    {
        Assert.Null(PhotoEncoder.DetectFormat(bytes));
        Assert.Null(PhotoEncoder.Encode(new MemoryStream(bytes)));
    }

    [Theory]
    [InlineData("RIFF\0\0\0\0WAVE")]
    [InlineData("\0\0\0\u0018ftypqt  ")]
    [InlineData("GIF89a")]
    public void Lookalike_containers_are_not_recognised(string content)
    {
        Assert.Null(PhotoEncoder.DetectFormat(System.Text.Encoding.Latin1.GetBytes(content)));
    }

    [Theory]
    [InlineData(MagickFormat.Jpeg, MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Png, MagickFormat.Png)]
    [InlineData(MagickFormat.WebP, MagickFormat.WebP)]
    public void Real_images_are_recognised_by_their_signature(MagickFormat written, MagickFormat detected)
    {
        using var stream = Image(20, 20, written);

        Assert.Equal(detected, PhotoEncoder.DetectFormat(stream.ToArray()));
    }

    [Theory]
    [InlineData("heic", MagickFormat.Heic)]
    [InlineData("heix", MagickFormat.Heic)]
    [InlineData("mif1", MagickFormat.Heic)]
    [InlineData("avif", MagickFormat.Avif)]
    public void Heif_brands_are_recognised(string brand, MagickFormat detected)
    {
        var bytes = System.Text.Encoding.Latin1.GetBytes("\0\0\0\u0018ftyp" + brand + "\0\0\0\0mif1");

        Assert.Equal(detected, PhotoEncoder.DetectFormat(bytes));
    }

    [Fact]
    public void A_png_signature_in_front_of_an_svg_is_refused()
    {
        // The decoder is pinned to PNG, so the SVG after the signature is never interpreted as SVG.
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. System.Text.Encoding.UTF8.GetBytes(Svg)];

        Assert.Null(PhotoEncoder.Encode(new MemoryStream(bytes)));
    }

    [Fact]
    public void ImageMagick_resource_limits_are_set()
    {
        PhotoEncoder.ApplyResourceLimits();

        Assert.Equal(PhotoEncoder.MemoryLimit, ResourceLimits.Memory);
        Assert.Equal((ulong)PhotoEncoder.MaxPixels, ResourceLimits.Area);
    }
}
