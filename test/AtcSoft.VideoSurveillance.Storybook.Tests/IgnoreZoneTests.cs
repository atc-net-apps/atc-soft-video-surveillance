namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// A camera's ignore zones in the storybook: the trees of S2 masked leave no activity behind (the
/// timeline's bars and the index), and the walker of S3 is missed inside a mask and found once it
/// leaves it.
/// </summary>
public sealed class IgnoreZoneTests
{
    [Fact]
    public void Masked_Trees_Leave_No_Activity()
    {
        var scene = Scene("s02-tree-in-the-wind.storybook.json");
        var open = new LevelRecorder(new BuiltInMotionDetector(new MotionDetectionSettings()));

        // The crowns reach far up: the left tree to 6 % from the top, the right one to 17 %.
        var masked = new LevelRecorder(new BuiltInMotionDetector(
            new MotionDetectionSettings(),
            zones: [Zone(0, 0, 0.42, 0.82), Zone(0.58, 0.1, 0.92, 0.8)]));

        var noise = new LevelRecorder(new BuiltInMotionDetector(new MotionDetectionSettings()));
        StorybookRunner.Run(Scene("s01-empty-noise.storybook.json"), noise);
        StorybookRunner.Run(scene, open);
        var run = StorybookRunner.Run(scene, masked);

        open.Levels.Max().Should().BeGreaterThan(0.5, "the trees sway");
        masked.Levels.Max().Should().BeLessThan(open.Levels.Max() / 10, "the trees are masked");
        masked.Levels.Average().Should().BeLessThanOrEqualTo(noise.Levels.Average() + 0.005, $"what is left is the noise of the picture (noise {noise.Levels.Average():F3}, masked {masked.Levels.Average():F3}, open {open.Levels.Average():F3})");
        run.Score.FalseBoxesPerFrame.Should().Be(0);
    }

    [Fact]
    public void A_Walker_Is_Missed_Inside_A_Mask_And_Found_Once_It_Leaves_It()
    {
        var scene = Scene("s03-slow-walker.storybook.json");
        var run = StorybookRunner.Run(scene, new BuiltInMotionDetector(new MotionDetectionSettings(), zones: [Zone(0, 0, 0.3, 1)]));

        run.Frames.Where(f => f.Time < 3).Should().OnlyContain(f => f.Detected.Count == 0, "the walker is inside the mask");
        var after = run.Frames.Where(f => f.Time >= 6 && f.Truth.Count > 0).ToList();
        after.Count(f => f.Detected.Count > 0).Should().BeGreaterThanOrEqualTo((int)(after.Count * 0.9), "out of the mask it is boxed again");
    }

    [Fact]
    public void Zones_Given_While_Running_Mask_From_Then_On()
    {
        var scene = Scene("s02-tree-in-the-wind.storybook.json");
        var open = new LevelRecorder(new BuiltInMotionDetector(new MotionDetectionSettings()));
        var masked = new ZonesAt(
            new BuiltInMotionDetector(new MotionDetectionSettings()),
            TimeSpan.FromSeconds(4),
            [Zone(0, 0, 0.42, 0.82), Zone(0.58, 0.1, 0.92, 0.8)]);

        StorybookRunner.Run(scene, open);
        StorybookRunner.Run(scene, masked);

        var openAfter = open.Levels.Skip(masked.Before.Count).Max();
        masked.Before.Max().Should().BeGreaterThan(0.5, "the trees sway until the zones come");
        masked.After.Max().Should().BeLessThan(openAfter / 10, "the trees are masked from the next picture on");
    }

    private static StorybookScene Scene(string file)
        => StorybookReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Scenes", file));

    private static MotionZone Zone(
        double left,
        double top,
        double right,
        double bottom)
        => new()
        {
            Kind = AtcSoft.VideoSurveillance.Enums.MotionZoneKind.Ignore,
            Points = [new(left, top), new(right, top), new(right, bottom), new(left, bottom)],
        };

    // Gives the detector the zones once the scene reaches a time, and keeps the levels before and after.
    private sealed class ZonesAt(
        IMotionDetector inner,
        TimeSpan at,
        IReadOnlyList<MotionZone> zones) : IMotionDetector
    {
        private DateTimeOffset? start;

        public List<double> Before { get; } = [];

        public List<double> After { get; } = [];

        public MotionDetectorResult Process(MotionFrame frame)
        {
            start ??= frame.Time;
            var given = frame.Time - start.Value >= at;
            if (given && After.Count == 0)
            {
                inner.UseZones(zones);
            }

            var result = inner.Process(frame);
            (given ? After : Before).Add(result.ChangePercent);
            return result;
        }

        public MotionDetectorResult Idle(DateTimeOffset now)
            => inner.Idle(now);

        public void Reset()
            => inner.Reset();
    }

    // Passes the pictures on and keeps the level of each, as the activity index would.
    private sealed class LevelRecorder(IMotionDetector inner) : IMotionDetector
    {
        public List<double> Levels { get; } = [];

        public MotionDetectorResult Process(MotionFrame frame)
        {
            var result = inner.Process(frame);
            Levels.Add(result.ChangePercent);
            return result;
        }

        public MotionDetectorResult Idle(DateTimeOffset now)
            => inner.Idle(now);

        public void Reset()
            => inner.Reset();

        public void UseZones(IReadOnlyList<MotionZone> zones)
            => inner.UseZones(zones);
    }
}