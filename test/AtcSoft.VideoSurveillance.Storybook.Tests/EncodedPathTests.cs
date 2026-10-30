namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// The encoded path itself changes nothing the detector sees: a scene encoded at a high quality and
/// read back as the apps read a recording scores as on the fast path. So what a scene with a low
/// bitrate loses (S11) is the compression's doing, not the pipeline's. Only the detector that
/// ships: today's frame difference already moves with the way a picture is scaled. Needs the
/// FFmpeg libraries and its command-line tool; skipped without them.
/// </summary>
public sealed class EncodedPathTests : IDisposable
{
    private const double Tolerance = 0.02;

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-encoded-path-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a temp folder left behind is harmless.
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(15)]
    public void A_Scene_Encoded_At_A_High_Quality_Scores_As_On_The_Fast_Path(
        int analysesPerSecond)
    {
        var tool = EncodedScenes.RequireTool();
        var scene = StorybookReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Scenes", "s05-car-human-cat.storybook.json"));
        Directory.CreateDirectory(directory);
        var video = Path.Combine(directory, "s05.mkv");
        var truth = StorybookEncoder.Encode(scene, tool, video, TestContext.Current.CancellationToken);
        var options = new StorybookRunOptions(AnalysesPerSecond: analysesPerSecond);

        var fast = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()), options).Score;
        var encoded = StorybookRunner.RunEncoded(scene, new BuiltInMotionDetector(scene.Detector.Settings()), video, truth, options).Score;
        TestContext.Current.TestOutputHelper?.WriteLine($"fast:    {fast.Summary}");
        TestContext.Current.TestOutputHelper?.WriteLine($"encoded: {encoded.Summary}");

        encoded.Recall.Should().BeApproximately(fast.Recall, Tolerance);
        encoded.FalseBoxesPerFrame.Should().BeApproximately(fast.FalseBoxesPerFrame, Tolerance);
        encoded.TrackCount.Should().Be(fast.TrackCount);
    }
}