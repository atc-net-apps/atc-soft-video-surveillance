namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

using Translations = AtcSoft.VideoSurveillance.Resources.Translations;

/// <summary>
/// The timeline under a recording, drawn to a bitmap: an orange band where something went
/// somewhere, nothing orange where nothing did, and the player's line where it is.
/// </summary>
public sealed class ActivityTimelineTests
{
    private const int Width = 100;
    private const int Height = 20;

    [Fact]
    public void An_Event_Is_An_Orange_Band_At_Its_Place_And_The_Player_Is_A_White_Line()
    {
        byte[] pixels = [];

        var error = WpfTestThread.Run(() =>
        {
            var timeline = new ActivityTimeline
            {
                Buckets = [.. Enumerable.Range(0, 10).Select(i => new ActivityBucket(i == 5 ? 30 : 1, i == 5 ? 1 : 0, IsAnalysed: true))],
                Position = 0.9,
            };

            pixels = Render(timeline);
        });

        error.Should().BeNull();
        Pixel(pixels, 55, Height - 2).Should().Be((0xFF, 0x8C, 0x00), "the sixth of ten stretches has a track");
        Pixel(pixels, 20, Height - 2).Should().NotBe((0xFF, 0x8C, 0x00));
        Pixel(pixels, 90, 2).Should().Be((0xFF, 0xFF, 0xFF), "the player is at nine tenths");
    }

    [Fact]
    public void An_Event_In_A_Watch_Zone_Is_Drawn_In_The_Zones_Colour()
    {
        byte[] pixels = [];

        var error = WpfTestThread.Run(() =>
        {
            var zones = new[]
            {
                new AtcSoft.VideoSurveillance.Models.MotionZone { Name = "Gate", Kind = MotionZoneKind.Watch, Color = "Lime" },
                new AtcSoft.VideoSurveillance.Models.MotionZone { Name = "Gate", Kind = MotionZoneKind.Watch, Color = "Red" },
                new AtcSoft.VideoSurveillance.Models.MotionZone { Name = "Road", Kind = MotionZoneKind.Ignore, Color = "Blue" },
            };
            var timeline = new ActivityTimeline
            {
                Buckets =
                [
                    .. Enumerable.Range(0, 10).Select(i => i switch
                    {
                        2 => new ActivityBucket(30, 1, IsAnalysed: true) { Zones = ["Gate"] },
                        7 => new ActivityBucket(30, 1, IsAnalysed: true) { Zones = ["Road"] },
                        _ => new ActivityBucket(1, 0, IsAnalysed: true),
                    }),
                ],
                ZoneBrushes = MotionZoneColors.ByZone(zones),
            };

            pixels = Render(timeline);
        });

        error.Should().BeNull();
        Pixel(pixels, 25, Height - 2).Should().Be((0x00, 0xFF, 0x00), "the first zone named Gate is lime");
        Pixel(pixels, 75, Height - 2).Should().Be((0xFF, 0x8C, 0x00), "an ignore zone has no colour of its own: orange");
    }

    [Fact]
    public void Zoomed_In_It_Draws_The_Part_Shown_Across_The_Strip()
    {
        byte[] pixels = [];

        var error = WpfTestThread.Run(() =>
        {
            var timeline = new ActivityTimeline
            {
                Buckets = [.. Enumerable.Range(0, 10).Select(i => new ActivityBucket(i is 1 or 5 ? 30 : 1, i is 1 or 5 ? 1 : 0, IsAnalysed: true))],
                Position = 0.9,
                VisibleStart = 0.5,
                VisibleEnd = 1,
            };

            pixels = Render(timeline);
        });

        error.Should().BeNull();
        Pixel(pixels, 10, Height - 2).Should().Be((0xFF, 0x8C, 0x00), "the sixth of ten stretches fills the first fifth of the second half");
        Pixel(pixels, 30, Height - 2).Should().NotBe((0xFF, 0x8C, 0x00), "the second stretch is not shown");
        Pixel(pixels, 80, 2).Should().Be((0xFF, 0xFF, 0xFF), "the player at nine tenths is four fifths into the part shown");
    }

    [Fact]
    public void A_Click_And_The_Wheel_Name_The_Place_In_The_Recording()
    {
        var places = new List<object?>();

        var error = WpfTestThread.Run(() =>
        {
            var command = new Atc.XamlToolkit.Command.RelayCommand<object?>(places.Add);
            var timeline = new ActivityTimeline
            {
                Buckets = [new ActivityBucket(1, 0, IsAnalysed: true)],
                VisibleStart = 0.5,
                VisibleEnd = 1,
                SeekCommand = command,
                ZoomCommand = command,
            };
            Render(timeline);

            var mouse = System.Windows.Input.Mouse.PrimaryDevice;
            timeline.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(mouse, 0, 120) { RoutedEvent = System.Windows.UIElement.MouseWheelEvent });
            timeline.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(mouse, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.UIElement.MouseLeftButtonDownEvent });
        });

        error.Should().BeNull();
        places.Should().HaveCount(2);
        places[0].Should().BeOfType<ActivityTimelineZoomRequest>().Which.ZoomIn.Should().BeTrue();
        ((double)places[1]!).Should().BeInRange(0.5, 1, "a click lands in the part shown");
    }

    [Fact]
    public void Resting_The_Mouse_Tells_The_Time_What_Moved_And_The_Zone()
    {
        var words = string.Empty;
        var quiet = string.Empty;

        var error = WpfTestThread.Run(() =>
        {
            var timeline = new ActivityTimeline
            {
                Buckets = [.. Enumerable.Range(0, 10).Select(i => i == 5
                    ? new ActivityBucket(30, 2, IsAnalysed: true) { Zones = ["Gate"] }
                    : new ActivityBucket(1, 0, IsAnalysed: true))],
                RecordingStart = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Local),
                DurationSeconds = 100,
            };
            Render(timeline);

            words = timeline.DescribeAt(55);
            quiet = timeline.DescribeAt(15);
        });

        error.Should().BeNull();
        words.Split(Environment.NewLine).Should().Equal("09:00:55", string.Format(CultureInfo.CurrentCulture, Translations.ActivityHoverMoving1, 2), string.Format(CultureInfo.CurrentCulture, Translations.ActivityHoverZone1, "Gate"), Translations.ActivityTimelineHint);
        quiet.Split(Environment.NewLine).Should().Equal("09:00:15", Translations.ActivityHoverNothingMoving, Translations.ActivityTimelineHint);
    }

    [Fact]
    public void Resting_The_Mouse_Shows_The_Nearest_Picture_And_On_An_Event_Its_Busiest_Moment()
    {
        var colours = new List<uint>();
        var eventWords = string.Empty;

        var error = WpfTestThread.Run(() =>
        {
            // Three grid tiles, five seconds apart, and the event's tile at 6.5 s: each one colour.
            uint[] tileColours = [0xFFFF0000, 0xFF00FF00, 0xFF0000FF, 0xFFFFFF00];
            var layout = new AtcSoft.VideoSurveillance.Models.RecordingPreviews(5, 4, 2, 4, 3, [new(6.5, 3)]);
            var pixels = new uint[16 * 2];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = tileColours[(i % 16) / 4];
            }

            var image = System.Windows.Media.Imaging.BitmapSource.Create(16, 2, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, 16 * 4);
            image.Freeze();
            var timeline = new ActivityTimeline
            {
                Buckets = [.. Enumerable.Range(0, 10).Select(i => new ActivityBucket(i == 6 ? 30 : 1, i == 6 ? 1 : 0, IsAnalysed: true))],
                RecordingStart = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Local),
                DurationSeconds = 10,
                Previews = new AtcSoft.VideoSurveillance.Wpf.Core.Services.RecordingPreviewTiles(layout, image),
            };
            Render(timeline);

            foreach (var x in new[] { 15d, 45, 65 })
            {
                var tile = timeline.PreviewAt(x)!;
                var first = new uint[1];
                tile.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), first, 4, 0);
                colours.Add(first[0]);
            }

            eventWords = timeline.DescribeAt(65);
        });

        error.Should().BeNull();
        colours.Should().Equal(0xFFFF0000, 0xFF00FF00, 0xFFFFFF00);
        eventWords.Split(Environment.NewLine)[0].Should().Be("09:00:06", "the time of the event's picture, 6.5 s in");
    }

    [Fact]
    public void Without_Activity_Nothing_Is_Drawn_But_The_Strip_Still_Takes_Clicks()
    {
        byte[] pixels = [];
        var hit = false;

        var error = WpfTestThread.Run(() =>
        {
            var timeline = new ActivityTimeline();
            pixels = Render(timeline);
            hit = System.Windows.Media.VisualTreeHelper.HitTest(timeline, new System.Windows.Point(50, 10)) is not null;
        });

        error.Should().BeNull();
        pixels.Should().OnlyContain(b => b == 0);
        hit.Should().BeTrue();
    }

    [Fact]
    public void A_Right_Click_On_An_Event_Offers_It_Correct_Or_A_False_Alarm_And_Anywhere_Something_Missed()
    {
        IReadOnlyList<AtcSoft.VideoSurveillance.Helpers.Review.TimelineVerdict> onEvent = [];
        IReadOnlyList<AtcSoft.VideoSurveillance.Helpers.Review.TimelineVerdict> beside = [];

        var error = WpfTestThread.Run(() =>
        {
            var timeline = new ActivityTimeline
            {
                Buckets = [.. Enumerable.Range(0, 10).Select(i => new ActivityBucket(i == 5 ? 30 : 1, i == 5 ? 1 : 0, IsAnalysed: true))],
                DurationSeconds = 100,
            };
            Render(timeline);

            onEvent = timeline.VerdictsAt(55);
            beside = timeline.VerdictsAt(20);
        });

        error.Should().BeNull();
        onEvent.Select(v => v.Verdict).Should().Equal("correct", "falseBox", "missed");
        onEvent.Select(v => (Math.Round(v.FromSeconds, 6), Math.Round(v.ToSeconds, 6))).Should().Equal((50d, 60d), (50d, 60d), (52.5, 57.5));
        beside.Select(v => v.Verdict).Should().Equal("missed");
    }

    private static byte[] Render(ActivityTimeline timeline)
    {
        timeline.Measure(new System.Windows.Size(Width, Height));
        timeline.Arrange(new System.Windows.Rect(0, 0, Width, Height));
        timeline.UpdateLayout();

        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(Width, Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(timeline);
        var pixels = new byte[Width * Height * 4];
        bitmap.CopyPixels(pixels, Width * 4, 0);
        return pixels;
    }

    private static (int R, int G, int B) Pixel(
        byte[] pixels,
        int x,
        int y)
    {
        var i = ((y * Width) + x) * 4;
        return (pixels[i + 2], pixels[i + 1], pixels[i]);
    }
}