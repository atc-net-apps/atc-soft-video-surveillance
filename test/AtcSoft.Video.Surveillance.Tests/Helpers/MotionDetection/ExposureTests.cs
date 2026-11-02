namespace AtcSoft.Video.Surveillance.Helpers.MotionDetection;

/// <summary>
/// A camera's automatic exposure brightens or darkens the whole picture by a few percent, the
/// bright surfaces most. At a high sensitivity that turned walls and ceilings into one foreground
/// the size of the picture; the background now follows the exposure, while lighting changes are
/// ignored.
/// </summary>
public sealed class ExposureTests
{
    private const int Width = 160;
    private const int Height = 120;

    [Fact]
    public void An_Exposure_Step_Of_A_Few_Percent_Is_Not_Motion()
    {
        var change = ChangeAfterExposureStep(followExposure: true, gain: 1.06);

        change.Should().BeLessThan(1, "the background followed the camera's exposure");
    }

    [Fact]
    public void Without_Following_The_Exposure_The_Same_Step_Is_Foreground()
    {
        var change = ChangeAfterExposureStep(followExposure: false, gain: 1.06);

        change.Should().BeGreaterThan(10, "this is what the walls and ceiling did before");
    }

    [Fact]
    public void A_Camera_That_Does_Not_Ignore_Lighting_Changes_Does_Not_Follow_The_Exposure()
        => MotionDetectorOptions.From(new MotionDetectionSettings { IgnoreLightingChanges = false }).FollowExposure.Should().BeFalse();

    // The share of the picture that differs (percent) a second after the whole picture took a gain,
    // at the highest sensitivity.
    private static double ChangeAfterExposureStep(
        bool followExposure,
        double gain)
    {
        var settings = new MotionDetectionSettings { Sensitivity = 100 };
        var detector = new BuiltInMotionDetector(settings, MotionDetectorOptions.From(settings) with { FollowExposure = followExposure });
        for (var i = 0; i < 40; i++)
        {
            detector.Process(Picture(i, 1.0));
        }

        var change = 0.0;
        for (var i = 40; i < 44; i++)
        {
            change = detector.Process(Picture(i, gain)).ChangePercent;
        }

        return change;
    }

    // Four pictures a second: a still scene of walls from dim to bright, times a gain.
    private static MotionFrame Picture(
        int index,
        double gain)
    {
        var gray = new byte[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var wall = 90 + ((((x / 16) + (y / 16)) % 4) * 40);
                gray[(y * Width) + x] = (byte)Math.Clamp(Math.Round(wall * gain), 0, 255);
            }
        }

        return new MotionFrame(gray, Width, Height, DateTimeOffset.UnixEpoch.AddSeconds(index / 4.0));
    }
}