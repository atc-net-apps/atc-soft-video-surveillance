namespace Atc.Video.Engine.Capture;

/// <summary>
/// Still pictures in and out without GDI+: a PNG gives back the pixels it was made of, a JPEG
/// close to them, and a picture is read at the size asked for.
/// </summary>
public sealed class PictureCodecTests
{
    [Fact]
    public void A_Png_Gives_Back_The_Pixels_It_Was_Made_Of()
    {
        TestEngine.Require();
        var pixels = Quarters(64, 48);

        var png = PictureCodec.EncodePng(pixels, 64, 48);

        png.Should().NotBeNull();
        png!.Take(4).Should().Equal(0x89, 0x50, 0x4E, 0x47);
        var read = PictureCodec.Decode(png, (width, height) => (width, height));
        read.Should().NotBeNull();
        read!.Width.Should().Be(64);
        read.Height.Should().Be(48);
        read.Pixels.Should().Equal(pixels);
    }

    [Fact]
    public void A_Jpeg_Is_Close_To_The_Pixels_It_Was_Made_Of()
    {
        TestEngine.Require();
        var pixels = Quarters(64, 48);

        var jpeg = PictureCodec.EncodeJpeg(pixels, 64, 48, quality: 75);

        jpeg.Should().NotBeNull();
        jpeg!.Take(3).Should().Equal(0xFF, 0xD8, 0xFF);
        var read = PictureCodec.Decode(jpeg, (width, height) => (width, height))!;
        Pixel(read, 8, 8).Should().BeEquivalentTo(Red, options => options.Using<byte>(c => c.Subject.Should().BeCloseTo(c.Expectation, 24)).WhenTypeIs<byte>());
        Pixel(read, 56, 40).Should().BeEquivalentTo(Blue, options => options.Using<byte>(c => c.Subject.Should().BeCloseTo(c.Expectation, 24)).WhenTypeIs<byte>());
    }

    [Fact]
    public void A_Better_Jpeg_Is_A_Larger_File()
    {
        TestEngine.Require();
        var pixels = Noise(160, 120);

        var small = PictureCodec.EncodeJpeg(pixels, 160, 120, quality: 10)!;
        var large = PictureCodec.EncodeJpeg(pixels, 160, 120, quality: 95)!;

        large.Length.Should().BeGreaterThan(small.Length);
    }

    [Fact]
    public void A_Picture_Is_Read_At_The_Size_Asked_For()
    {
        TestEngine.Require();
        var png = PictureCodec.EncodePng(Quarters(640, 480), 640, 480)!;

        var read = PictureCodec.Decode(png, (width, height) => (width / 4, height / 4));

        read.Should().NotBeNull();
        read!.Width.Should().Be(160);
        read.Height.Should().Be(120);
        Pixel(read, 20, 15).Should().Equal(Red);
        Pixel(read, 140, 105).Should().Equal(Blue);
    }

    [Fact]
    public void Bytes_That_Are_No_Picture_Are_Not_Read()
    {
        TestEngine.Require();

        PictureCodec.Decode([1, 2, 3, 4, 5, 6, 7, 8, 9], (width, height) => (width, height)).Should().BeNull();
    }

    private static readonly byte[] Red = [0, 0, 255, 255];

    private static readonly byte[] Blue = [255, 0, 0, 255];

    // BGRA: red top left, green top right, white bottom left, blue bottom right.
    private static byte[] Quarters(
        int width,
        int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var colour = (x < width / 2, y < height / 2) switch
                {
                    (true, true) => Red,
                    (false, true) => new byte[] { 0, 255, 0, 255 },
                    (true, false) => new byte[] { 255, 255, 255, 255 },
                    _ => Blue,
                };
                colour.CopyTo(pixels, ((y * width) + x) * 4);
            }
        }

        return pixels;
    }

    private static byte[] Noise(
        int width,
        int height)
    {
        // Busy detail without a random generator: a multiplicative hash of the position.
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(unchecked((uint)i * 2654435761u) >> 24);
        }

        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
        }

        return pixels;
    }

    private static byte[] Pixel(
        PictureFrame picture,
        int x,
        int y)
        => picture.Pixels.AsSpan(((y * picture.Width) + x) * 4, 4).ToArray();
}