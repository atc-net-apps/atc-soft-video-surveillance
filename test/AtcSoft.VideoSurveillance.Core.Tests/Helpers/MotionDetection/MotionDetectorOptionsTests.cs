namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// What the detector runs with for a camera's settings: the plain-word settings become its
/// tuning, and the settings as they come give the tuning the storybook baseline was scored with.
/// </summary>
public sealed class MotionDetectorOptionsTests
{
    [Fact]
    public void The_Default_Settings_Give_The_Tuned_Options()
    {
        var settings = new MotionDetectionSettings();
        var (sigmas, minThreshold) = MotionDetectorOptions.Thresholds(settings.Sensitivity);

        MotionDetectorOptions.From(settings).Should().Be(new MotionDetectorOptions(ThresholdSigmas: sigmas, MinThreshold: minThreshold));
    }

    [Fact]
    public void The_Smallest_Object_Is_Taken_As_It_Is_And_The_Stopped_Time_Is_Three_Time_Constants()
    {
        var options = MotionDetectorOptions.From(new MotionDetectionSettings { SmallestObjectPercent = 0.5, StoppedObjectSeconds = 120 });

        options.SmallestObjectShare.Should().BeApproximately(0.005, 1e-12);
        options.ForegroundSeconds.Should().Be(40);
        options.BackgroundSeconds.Should().Be(new MotionDetectorOptions().BackgroundSeconds, "only what stops is learnt slower or faster");
    }

    [Theory]
    [InlineData(1, -1, MotionDetectorOptions.MinStoppedObjectSeconds, 0)]
    [InlineData(100_000, 500, MotionDetectorOptions.MaxStoppedObjectSeconds, MotionDetectorOptions.MaxSmallestObjectPercent / 100)]
    public void Values_Out_Of_Range_Are_Kept_Within_It(
        int stoppedSeconds,
        double smallestPercent,
        double expectedSeconds,
        double expectedShare)
    {
        var options = MotionDetectorOptions.From(new MotionDetectionSettings { StoppedObjectSeconds = stoppedSeconds, SmallestObjectPercent = smallestPercent });

        options.ForegroundSeconds.Should().Be(expectedSeconds / MotionDetectorOptions.StoppedObjectTimeConstants);
        options.SmallestObjectShare.Should().BeApproximately(expectedShare, 1e-12);
    }

    [Fact]
    public void Lighting_Changes_Not_Ignored_Are_Never_Taken_For_Light()
    {
        MotionDetectorOptions.From(new MotionDetectionSettings { IgnoreLightingChanges = false }).LightingChangeShare.Should().Be(1.0);
        MotionDetectorOptions.From(new MotionDetectionSettings()).LightingChangeShare.Should().Be(new MotionDetectorOptions().LightingChangeShare);
    }

    [Fact]
    public void Someone_Filling_Most_Of_The_Picture_Is_Seen_Sooner_When_Lighting_Changes_Are_Not_Ignored()
    {
        var notIgnored = FirstSeen(ignoreLighting: false);
        var ignored = FirstSeen(ignoreLighting: true);

        notIgnored.Should().NotBeNull();
        notIgnored.Should().BeLessThan(ignored ?? int.MaxValue, $"with lighting changes ignored it is first taken for light (seen at frame {ignored})");
    }

    [Fact]
    public void After_A_Lamp_Is_Switched_On_Someone_Walking_In_Is_Seen_Later_When_Lighting_Changes_Are_Not_Ignored()
    {
        var ignored = WalkerFirstSeen(ignoreLighting: true);
        var notIgnored = WalkerFirstSeen(ignoreLighting: false);

        ignored.Should().NotBeNull();
        notIgnored.Should().NotBeNull("the new light becomes the background in a few seconds, not the stopped-object time");
        notIgnored.Should().BeGreaterThan(ignored!.Value, $"the whole picture is moving until it has (seen at frame {ignored} and {notIgnored})");
    }

    // The picture turns brighter at 5 s; at 9 s someone small walks in from the left, four
    // pictures a second: the first picture the walker has a box in, null without one.
    private static int? WalkerFirstSeen(bool ignoreLighting)
    {
        const int width = 160;
        const int height = 120;
        var detector = new BuiltInMotionDetector(new MotionDetectionSettings { IgnoreLightingChanges = ignoreLighting });
        var start = DateTimeOffset.UnixEpoch;
        for (var i = 0; i < 80; i++)
        {
            var grey = new byte[width * height];
            Array.Fill(grey, i < 20 ? (byte)70 : (byte)170);
            if (i >= 36)
            {
                var left = (i - 36) * 3;
                for (var y = 50; y < 80; y++)
                {
                    Array.Fill(grey, (byte)20, (y * width) + left, Math.Min(10, width - left));
                }
            }

            var boxes = detector.Process(new MotionFrame(grey, width, height, start.AddSeconds(i * 0.25))).Boxes;
            if (i >= 36 && boxes.Any(b => b.Width < width / 2))
            {
                return i;
            }
        }

        return null;
    }

    // A dark shape three quarters of the picture wide walks in over a light, empty picture, four
    // pictures a second: the first picture with a box, null without one.
    private static int? FirstSeen(bool ignoreLighting)
    {
        const int width = 160;
        const int height = 120;
        var detector = new BuiltInMotionDetector(new MotionDetectionSettings { IgnoreLightingChanges = ignoreLighting });
        var start = DateTimeOffset.UnixEpoch;
        for (var i = 0; i < 40; i++)
        {
            var grey = new byte[width * height];
            Array.Fill(grey, (byte)200);
            if (i >= 20)
            {
                var left = (i - 20) * 2;
                for (var y = 0; y < height; y++)
                {
                    Array.Fill(grey, (byte)40, (y * width) + left, Math.Min(width * 3 / 4, width - left));
                }
            }

            if (detector.Process(new MotionFrame(grey, width, height, start.AddSeconds(i * 0.25))).Boxes.Count > 0)
            {
                return i;
            }
        }

        return null;
    }
}