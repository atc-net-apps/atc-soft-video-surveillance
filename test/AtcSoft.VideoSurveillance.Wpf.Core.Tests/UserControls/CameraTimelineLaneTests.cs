namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// A camera's lane on the timeline of every camera, drawn to a bitmap: grey where nothing was
/// recorded, an orange band where something went somewhere, a red line at now.
/// </summary>
public sealed class CameraTimelineLaneTests
{
    private const int Width = 100;
    private const int Height = 20;

    [Fact]
    public void A_Gap_Is_Grey_An_Event_Orange_And_Now_A_Red_Line()
    {
        byte[] pixels = [];

        var error = WpfTestThread.Run(() =>
        {
            var lane = new CameraTimelineLane
            {
                // Nothing recorded in the first fifth; then recorded and analysed.
                Stretches = [.. Enumerable.Range(0, 10).Select(i => new TimelineStretch(1, 0, IsRecorded: i >= 2, IsAnalysed: i >= 2))],
                EventBands = [new ActivityEvent(0.5, 0.6)],
                NowFraction = 0.9,
            };

            pixels = Render(lane);
        });

        error.Should().BeNull();
        Pixel(pixels, 5, 10).A.Should().BeGreaterThan(0, "nothing was recorded there");
        Pixel(pixels, 55, Height - 2).Should().Be((0xFF, 0xFF, 0x8C, 0x00));
        Pixel(pixels, 90, 2).Should().Be((0xFF, 0xE5, 0x39, 0x35));
        Pixel(pixels, 35, 2).A.Should().Be(0, "a quiet recorded stretch is not filled");
    }

    [Fact]
    public void The_Marks_Bar_Draws_Its_Labels_And_Asks_For_A_Line_Of_Text()
    {
        byte[] pixels = [];
        var height = 0d;

        var error = WpfTestThread.Run(() =>
        {
            var bar = new TimelineMarksBar
            {
                Marks = [new TimelineMark(0.25, "06:00"), new TimelineMark(0.75, "18:00")],
            };
            bar.Measure(new System.Windows.Size(Width, double.PositiveInfinity));
            height = bar.DesiredSize.Height;
            pixels = Render(bar);
        });

        error.Should().BeNull();
        height.Should().BeGreaterThan(10);
        pixels.Should().Contain(b => b != 0, "the labels are drawn");
    }

    [Fact]
    public void A_Sparkline_Draws_A_Bar_Per_Stretch_Orange_Where_Something_Moved()
    {
        byte[] pixels = [];

        var error = WpfTestThread.Run(() =>
        {
            // Ten stretches of 10 px: a flicker in the second, something moving in the eighth.
            pixels = Render(new ActivitySparklineBars { Levels = [0, 5, 0, 0, 0, 0, 0, 60, 0, 0] });
        });

        error.Should().BeNull();
        Pixel(pixels, 74, Height - 2).Should().Be((0xFF, 0xFF, 0x8C, 0x00));
        Pixel(pixels, 14, Height - 2).A.Should().BeGreaterThan(0, "a flicker is a small grey bar");
        Pixel(pixels, 14, Height - 2).R.Should().NotBe(0xFF);
        Pixel(pixels, 34, Height - 2).A.Should().Be(0, "nothing moved there");
    }

    private static byte[] Render(System.Windows.FrameworkElement element)
    {
        element.Measure(new System.Windows.Size(Width, Height));
        element.Arrange(new System.Windows.Rect(0, 0, Width, Height));
        element.UpdateLayout();

        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(Width, Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixels = new byte[Width * Height * 4];
        bitmap.CopyPixels(pixels, Width * 4, 0);
        return pixels;
    }

    private static (int A, int R, int G, int B) Pixel(
        byte[] pixels,
        int x,
        int y)
    {
        var i = ((y * Width) + x) * 4;
        return (pixels[i + 3], pixels[i + 2], pixels[i + 1], pixels[i]);
    }
}