namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// What "something that stops becomes background after" means: something that arrives and stops
/// is still foreground at half the time and background by the time (sooner at low contrast), and
/// when it leaves, before that or after, its place is background again at once, with no ghost,
/// even after a change of light. The same holds for a light taken in and switched off again.
/// </summary>
public sealed class StoppedObjectTests
{
    private const int Width = 160;
    private const int Height = 120;
    private const int PerSecond = 4;

    [Theory]
    [InlineData(150)]
    [InlineData(40)]
    public void Something_That_Stops_Is_Background_By_The_Stopped_Object_Time(
        int contrast)
    {
        var settings = new MotionDetectionSettings();
        var detector = Arrived(settings, contrast, out var next);

        var changed = Enumerable.Range(next, settings.StoppedObjectSeconds * PerSecond).Select(k => detector.Process(Picture(k, contrast)).ChangePercent).ToList();

        changed[^1].Should().BeLessThan(0.1, "it is background by the stopped-object time");
        if (contrast == 150)
        {
            changed[(settings.StoppedObjectSeconds / 2 * PerSecond) - 1].Should().BeGreaterThan(1, "at half the time, something that differs clearly is still foreground");
        }
    }

    [Fact]
    public void Something_That_Stops_And_Leaves_Leaves_No_Ghost()
    {
        var detector = Arrived(new MotionDetectionSettings(), 150, out var next);
        for (var k = next; k < next + (10 * PerSecond); k++)
        {
            detector.Process(Picture(k, 150));
        }

        var changed = Enumerable.Range(next + (10 * PerSecond), PerSecond).Select(k => detector.Process(Picture(k, 0)).ChangePercent).ToList();

        changed[0].Should().BeLessThan(0.1, "where it stood is the background from before it came");
    }

    [Fact]
    public void Something_That_Stops_And_Leaves_After_A_Change_Of_Light_Leaves_No_Ghost()
    {
        var detector = Arrived(new MotionDetectionSettings(), 150, out var next);
        for (var k = next; k < next + (10 * PerSecond); k++)
        {
            detector.Process(Picture(k, 150));
        }

        // The light comes up by 30 grey levels, then it leaves.
        for (var k = next + (10 * PerSecond); k < next + (12 * PerSecond); k++)
        {
            detector.Process(Picture(k, 150, light: 30));
        }

        var changed = Enumerable.Range(next + (12 * PerSecond), 4 * PerSecond).Select(k => detector.Process(Picture(k, 0, light: 30)).ChangePercent).ToList();

        // A ghost would be the whole square, 4.7 % of the picture; a few pixels at its edge may stay a moment.
        changed[0].Should().BeLessThan(0.5, "the background from before it came moved with the light");
        changed[^1].Should().BeLessThan(0.1);
    }

    [Fact]
    public void Something_Taken_In_That_Leaves_Leaves_No_Ghost()
    {
        var settings = new MotionDetectionSettings();
        var detector = Arrived(settings, 150, out var next);
        var end = next + ((settings.StoppedObjectSeconds + 10) * PerSecond);
        for (var k = next; k < end; k++)
        {
            detector.Process(Picture(k, 150));
        }

        var changed = Enumerable.Range(end, PerSecond).Select(k => detector.Process(Picture(k, 0)).ChangePercent).ToList();

        // A ghost would be the whole square, 4.7 % of the picture.
        changed[0].Should().BeLessThan(0.1, "where it stood is the background from before it came, though it was taken in");
    }

    [Fact]
    public void A_Light_Taken_In_And_Switched_Off_Is_Background_Again_At_Once()
    {
        var detector = new BuiltInMotionDetector(new MotionDetectionSettings { IgnoreLightingChanges = false });
        var k = 0;
        for (; k < 5 * PerSecond; k++)
        {
            detector.Process(Picture(k, 0));
        }

        // A lamp: the whole picture 60 grey levels brighter for ten seconds, then off again.
        for (; k < 15 * PerSecond; k++)
        {
            detector.Process(Picture(k, 0, light: 60));
        }

        var on = detector.Process(Picture(k++, 0, light: 60)).ChangePercent;
        var off = detector.Process(Picture(k, 0)).ChangePercent;

        on.Should().BeLessThan(0.1, "the lamp's light was taken in");
        off.Should().BeLessThan(0.1, "the picture from before the lamp is the background again");
    }

    // Five seconds of the empty scene, then the square arrives.
    private static BuiltInMotionDetector Arrived(
        MotionDetectionSettings settings,
        int contrast,
        out int next)
    {
        var detector = new BuiltInMotionDetector(settings);
        for (var k = 0; k < 5 * PerSecond; k++)
        {
            detector.Process(Picture(k, 0));
        }

        detector.Process(Picture(5 * PerSecond, contrast));
        next = (5 * PerSecond) + 1;
        return detector;
    }

    // A still grey scene with a fixed texture, and a square brighter by the contrast (none at 0).
    private static MotionFrame Picture(
        int index,
        int contrast,
        int light = 0)
    {
        var gray = new byte[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var scene = 70 + ((((x / 8) + (y / 8)) % 3) * 10) + light;
                var inSquare = contrast > 0 && x is >= 60 and < 90 && y is >= 40 and < 70;
                gray[(y * Width) + x] = (byte)Math.Min(255, inSquare ? scene + contrast : scene);
            }
        }

        return new MotionFrame(gray, Width, Height, DateTimeOffset.UnixEpoch.AddSeconds(index / (double)PerSecond));
    }
}