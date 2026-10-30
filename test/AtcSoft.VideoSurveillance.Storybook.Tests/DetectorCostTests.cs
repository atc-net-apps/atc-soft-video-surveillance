namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// What a picture costs each detector at the apps' analysis size, written to the test output.
/// The server runs one detector per camera, so this is the number that limits cameras per
/// machine. Asserts only a generous ceiling, so a slow test machine does not fail it.
/// </summary>
public sealed class DetectorCostTests
{
    [Fact]
    public void A_Picture_Costs_A_Few_Milliseconds_At_The_Analysis_Size()
    {
        var scene = StorybookReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Scenes", "s05-car-human-cat.storybook.json"));
        var renderer = new SceneRenderer(scene);
        var frames = Enumerable.Range(0, 60)
            .Select(i => StorybookRunner.Resize(renderer.Render(i * 2).Luma, scene.Video.Width, scene.Video.Height, 800, 600))
            .ToList();

        var (builtIn, builtInBytes) = Measure(new BuiltInMotionDetector(new MotionDetectionSettings()), frames);
        var (frameDifference, _) = Measure(new FrameDifferenceMotionDetector(new MotionDetectionSettings()), frames);

        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"per picture at 800x600: built-in {builtIn:0.0} ms, {builtInBytes:0} bytes allocated; frame difference {frameDifference:0.0} ms"));
        builtIn.Should().BeLessThan(100, "one picture must not take a tenth of a second");
        builtInBytes.Should().BeLessThan(64 * 1024, "a detector runs all day for every camera: its picture-sized arrays are kept, not allocated per picture");
    }

    private static (double Milliseconds, double Bytes) Measure(
        IMotionDetector detector,
        List<byte[]> frames)
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        detector.Process(new MotionFrame(frames[0], 800, 600, start));
        detector.Process(new MotionFrame(frames[1], 800, 600, start.AddSeconds(2 / 15.0)));
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var i = 2; i < frames.Count; i++)
        {
            detector.Process(new MotionFrame(frames[i], 800, 600, start.AddSeconds(i * 2 / 15.0)));
        }

        var pictures = frames.Count - 2;
        return (watch.Elapsed.TotalMilliseconds / pictures, (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / (double)pictures);
    }
}