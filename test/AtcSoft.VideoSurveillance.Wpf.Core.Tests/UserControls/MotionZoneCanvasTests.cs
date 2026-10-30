namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

using MotionZone = AtcSoft.VideoSurveillance.Models.MotionZone;
using MotionZoneKind = AtcSoft.VideoSurveillance.Enums.MotionZoneKind;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;
using ZonePoint = AtcSoft.VideoSurveillance.Models.ZonePoint;

/// <summary>
/// The zones drawn over the picture, to a bitmap: grey where there is no picture yet, an ignored
/// zone tinted red and a watched one blue, kept to the picture's shape in the middle.
/// </summary>
public sealed class MotionZoneCanvasTests
{
    private static readonly (byte R, byte G, byte B) Grey = (0x3C, 0x3C, 0x3C);

    [Fact]
    public void An_Ignored_Zone_Is_Red_And_A_Watched_One_Blue_Over_The_Grey_Of_No_Picture()
    {
        byte[] pixels = [];

        var error = WpfTestThread.Run(() =>
        {
            // 160 x 90 is the shape of the picture while there is none, so it fills the canvas.
            pixels = Render(
                new MotionZoneCanvas
                {
                    Zones =
                    [
                        Zone(MotionZoneKind.Ignore, 0.5, 1),
                        Zone(MotionZoneKind.Watch, 0, 0.25),
                    ],
                },
                160,
                90);
        });

        error.Should().BeNull();
        var ignored = Pixel(pixels, 160, 120, 45);
        var watched = Pixel(pixels, 160, 20, 45);
        Pixel(pixels, 80, 45, 160).Should().Be(Grey);
        ignored.R.Should().BeGreaterThan(Grey.R);
        ignored.B.Should().BeLessThan(Grey.B);
        watched.B.Should().BeGreaterThan(Grey.B);
        watched.R.Should().BeLessThan(Grey.R);
    }

    [Fact]
    public void The_Picture_Keeps_Its_Shape_In_The_Middle_Of_A_Taller_Canvas()
    {
        byte[] pixels = [];

        var error = WpfTestThread.Run(() => pixels = Render(new MotionZoneCanvas(), 160, 190));

        error.Should().BeNull();
        Pixel(pixels, 160, 80, 10).Should().Be(((byte)0, (byte)0, (byte)0), "above the picture is nothing");
        Pixel(pixels, 160, 80, 95).Should().Be(Grey, "the 160 x 90 picture is in the middle");
        Pixel(pixels, 160, 80, 180).Should().Be(((byte)0, (byte)0, (byte)0), "below the picture is nothing");
    }

    private static MotionZone Zone(
        MotionZoneKind kind,
        double left,
        double right)
        => new()
        {
            Kind = kind,
            Points = [new ZonePoint(left, 0), new ZonePoint(right, 0), new ZonePoint(right, 1), new ZonePoint(left, 1)],
        };

    private static byte[] Render(
        MotionZoneCanvas canvas,
        int width,
        int height)
    {
        canvas.Measure(new Size(width, height));
        canvas.Arrange(new Rect(0, 0, width, height));
        canvas.UpdateLayout();

        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    private static (byte R, byte G, byte B) Pixel(
        byte[] pixels,
        int width,
        int x,
        int y)
    {
        var i = ((y * width) + x) * 4;
        return (pixels[i + 2], pixels[i + 1], pixels[i]);
    }
}