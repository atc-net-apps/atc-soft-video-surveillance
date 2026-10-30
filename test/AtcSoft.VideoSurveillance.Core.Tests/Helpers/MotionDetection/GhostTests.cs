namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// What was in the first picture and then left must not stay as a ghost: the background was
/// wrong there, not the picture.
/// </summary>
public sealed class GhostTests
{
    private const int Width = 160;
    private const int Height = 120;

    [Fact]
    public void Something_In_The_First_Picture_That_Leaves_Is_Not_A_Ghost_For_A_Minute()
    {
        var detector = new BuiltInMotionDetector(new MotionDetectionSettings());
        detector.Process(Picture(0, withSquare: true));

        // Four pictures a second: the first one, then two seconds without the square.
        var changed = Enumerable.Range(1, 8).Select(k => detector.Process(Picture(k, withSquare: false)).ChangePercent).ToList();

        changed[0].Should().BeGreaterThan(1, "right after it left, its place differs from the background");
        changed[^1].Should().BeLessThan(0.1, "two seconds later the background has learnt the place without it");
    }

    // Four pictures a second: a still grey scene with a fixed texture, and a bright square.
    private static MotionFrame Picture(
        int index,
        bool withSquare)
    {
        var gray = new byte[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var inSquare = withSquare && x is >= 60 and < 90 && y is >= 40 and < 70;
                gray[(y * Width) + x] = inSquare ? (byte)220 : (byte)(70 + ((((x / 8) + (y / 8)) % 3) * 10));
            }
        }

        return new MotionFrame(gray, Width, Height, DateTimeOffset.UnixEpoch.AddSeconds(index / 4.0));
    }
}