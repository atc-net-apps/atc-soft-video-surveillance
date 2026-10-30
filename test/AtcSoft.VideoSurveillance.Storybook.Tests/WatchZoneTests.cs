namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// A camera's watch zones in the storybook: inside one, its own sensitivity and smallest object
/// count; outside, the camera's. The small far cat of S8 crosses the middle of the picture, at a
/// height of 45 %, and is about 0.055 % of the picture.
/// </summary>
public sealed class WatchZoneTests
{
    [Fact]
    public void A_Cat_Smaller_Than_The_Camera_Takes_Is_Found_Inside_A_Watch_Zone_That_Takes_It()
    {
        var scene = Scene("s08-small-far-cat.storybook.json");
        var settings = new MotionDetectionSettings();
        var camera = MotionDetectorOptions.From(settings) with { SmallestObjectShare = 0.002 };

        var plain = StorybookRunner.Run(scene, new BuiltInMotionDetector(settings, camera));
        var onPath = StorybookRunner.Run(scene, new BuiltInMotionDetector(settings, camera, [Watch(0.35, 0.55, smallestObjectPercent: 0.02)]));
        var elsewhere = StorybookRunner.Run(scene, new BuiltInMotionDetector(settings, camera, [Watch(0.7, 0.95, smallestObjectPercent: 0.02)]));

        Boxed(plain).Should().Be(0, "the cat is smaller than the 0.2 % the camera takes");
        Boxed(onPath).Should().BeGreaterThan(Seen(onPath) / 2, "in the zone the cat is larger than its 0.02 %");
        Boxed(elsewhere).Should().Be(0, "outside the zone the camera's smallest object counts");
    }

    [Fact]
    public void A_Faint_Cat_The_Camera_Misses_Is_Found_Inside_A_More_Sensitive_Watch_Zone()
    {
        // Grey 118 on a background of about 97 at the cat's height: closer than the 24 levels the camera needs at sensitivity 0.
        var scene = Scene("s08-small-far-cat.storybook.json");
        scene.Objects[0].Shade = 118;
        var settings = new MotionDetectionSettings { Sensitivity = 0 };

        var plain = StorybookRunner.Run(scene, new BuiltInMotionDetector(settings));
        var onPath = StorybookRunner.Run(scene, new BuiltInMotionDetector(settings, zones: [Watch(0.35, 0.55, sensitivity: 100)]));
        var elsewhere = StorybookRunner.Run(scene, new BuiltInMotionDetector(settings, zones: [Watch(0.7, 0.95, sensitivity: 100)]));

        TestContext.Current.TestOutputHelper?.WriteLine($"plain {Boxed(plain)}/{Seen(plain)}, on path {Boxed(onPath)}/{Seen(onPath)}, elsewhere {Boxed(elsewhere)}/{Seen(elsewhere)}");
        Boxed(plain).Should().BeLessThan(Seen(plain) / 10, "the faint cat is below the camera's thresholds");
        Boxed(onPath).Should().BeGreaterThan(Seen(onPath) / 2, "the zone's thresholds are lower");
        Boxed(elsewhere).Should().BeLessThan(Seen(elsewhere) / 10, "outside the zone the camera's thresholds count");
    }

    [Fact]
    public void Noise_In_A_Watch_Zone_At_Full_Sensitivity_Gives_No_Boxes()
    {
        var run = StorybookRunner.Run(
            Scene("s01-empty-noise.storybook.json"),
            new BuiltInMotionDetector(new MotionDetectionSettings(), zones: [Watch(0, 1, sensitivity: 100, smallestObjectPercent: 0)]));

        run.Score.FalseBoxesPerFrame.Should().Be(0, "noise sways on the spot and never travels");
    }

    [Fact]
    public void A_Watch_Zone_Without_Values_Detects_As_The_Camera_Does()
    {
        var scene = Scene("s03-slow-walker.storybook.json");

        var plain = StorybookRunner.Run(scene, new BuiltInMotionDetector(new MotionDetectionSettings()));
        var watched = StorybookRunner.Run(scene, new BuiltInMotionDetector(new MotionDetectionSettings(), zones: [Watch(0, 1)]));

        watched.Frames.Select(f => f.Detected.Count).Should().Equal(plain.Frames.Select(f => f.Detected.Count));
    }

    private static int Boxed(StorybookRun run)
        => run.Frames.Count(f => f.Truth.Count > 0 && f.Detected.Count > 0);

    private static int Seen(StorybookRun run)
        => run.Frames.Count(f => f.Truth.Count > 0);

    private static StorybookScene Scene(string file)
        => StorybookReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Scenes", file));

    // A band across the picture, from one height to another.
    private static MotionZone Watch(
        double top,
        double bottom,
        int? sensitivity = null,
        double? smallestObjectPercent = null)
        => new()
        {
            Name = "Path",
            Kind = AtcSoft.VideoSurveillance.Enums.MotionZoneKind.Watch,
            Points = [new(0, top), new(1, top), new(1, bottom), new(0, bottom)],
            Sensitivity = sensitivity,
            SmallestObjectPercent = smallestObjectPercent,
        };
}