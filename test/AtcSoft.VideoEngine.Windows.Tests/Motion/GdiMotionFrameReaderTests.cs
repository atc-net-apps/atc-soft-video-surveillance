namespace AtcSoft.VideoEngine.Windows.Motion;

public sealed class GdiMotionFrameReaderTests
{
    private readonly GdiMotionFrameReader sut = new();

    [Fact]
    public void A_Picture_Is_Read_As_Brightness_In_The_Asked_Size()
    {
        // Left half white, right half black, four times the asked size.
        var png = CreatePng(640, 480, (graphics, _) =>
            graphics.FillRectangle(System.Drawing.Brushes.White, 0, 0, 320, 480));

        var grayscale = sut.ReadGrayscale(png, 160, 120);

        grayscale.Should().NotBeNull().And.HaveCount(160 * 120);
        grayscale![(60 * 160) + 20].Should().BeGreaterThan(250, "the left half is white");
        grayscale[(60 * 160) + 140].Should().BeLessThan(5, "the right half is black");
    }

    [Fact]
    public void Colours_Are_Weighed_As_The_Eye_Sees_Them()
    {
        var green = sut.ReadGrayscale(CreatePng(32, 32, (g, _) => g.Clear(System.Drawing.Color.FromArgb(0, 255, 0))), 32, 32);
        var blue = sut.ReadGrayscale(CreatePng(32, 32, (g, _) => g.Clear(System.Drawing.Color.FromArgb(0, 0, 255))), 32, 32);

        green![0].Should().BeInRange(145, 152);
        blue![0].Should().BeInRange(26, 31);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
    public void Bytes_That_Are_Not_A_Picture_Give_Nothing(byte[] bytes)
        => sut.ReadGrayscale(bytes, 160, 120).Should().BeNull();

    [Fact]
    public void A_Size_Of_Nothing_Gives_Nothing()
        => sut.ReadGrayscale(CreatePng(32, 32, (g, _) => g.Clear(System.Drawing.Color.White)), 0, 120).Should().BeNull();

    private static byte[] CreatePng(
        int width,
        int height,
        Action<System.Drawing.Graphics, System.Drawing.Bitmap> draw)
    {
        using var bitmap = new System.Drawing.Bitmap(width, height);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.Clear(System.Drawing.Color.Black);
            draw(graphics, bitmap);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return stream.ToArray();
    }
}