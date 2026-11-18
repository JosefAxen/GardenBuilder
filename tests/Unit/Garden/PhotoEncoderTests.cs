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
}
