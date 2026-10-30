namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// The sample's <i>Save as storybook settings</i>: only the detector section changes, and only
/// what differs from the defaults is written.
/// </summary>
public sealed class StorybookWriterTests
{
    [Theory]
    [InlineData("s05-car-human-cat.storybook.json")]
    [InlineData("s15-strong-wind.storybook.json")]
    public void The_Defaults_Leave_A_Scene_Without_Settings_As_It_Is(string file)
    {
        var text = File.ReadAllText(ScenePath(file));

        StorybookWriter.WithDetector(text, new MotionDetectionSettings()).Should().Be(text);
    }

    [Fact]
    public void A_Scenes_Own_Settings_Are_Rewritten_In_Place()
    {
        var text = File.ReadAllText(ScenePath("s14-door-camera.storybook.json"));

        StorybookWriter.WithDetector(text, new MotionDetectionSettings { IgnoreLightingChanges = false }).Should().Be(text);

        var tuned = StorybookWriter.WithDetector(text, new MotionDetectionSettings { IgnoreLightingChanges = false, Sensitivity = 45, StoppedObjectSeconds = 120 });
        var scene = StorybookReader.Read(tuned);
        (scene.Detector.Sensitivity, scene.Detector.SmallestObjectPercent, scene.Detector.StoppedObjectSeconds, scene.Detector.IgnoreLightingChanges)
            .Should().Be((45, (double?)null, 120, false));
        tuned.IndexOf("\"detector\"", StringComparison.Ordinal).Should().BeLessThan(tuned.IndexOf("\"expect\"", StringComparison.Ordinal));

        var defaults = StorybookWriter.WithDetector(text, new MotionDetectionSettings());
        defaults.Should().NotContain("\"detector\"");
        StorybookReader.Read(defaults).Detector.Settings().Should().BeEquivalentTo(new MotionDetectionSettings());
    }

    [Fact]
    public void New_Settings_Go_Before_What_Is_Expected()
    {
        var text = File.ReadAllText(ScenePath("s05-car-human-cat.storybook.json"));

        var tuned = StorybookWriter.WithDetector(text, new MotionDetectionSettings { SmallestObjectPercent = 0.05 });

        tuned.Should().Contain("\"detector\": {\n    \"smallestObjectPercent\": 0.05\n  },\n  \"expect\"".ReplaceLineEndings(text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n"));
        StorybookReader.Read(tuned).Detector.Settings().SmallestObjectPercent.Should().Be(0.05);
        tuned.Replace("  \"detector\": {\n    \"smallestObjectPercent\": 0.05\n  },\n".ReplaceLineEndings(text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n"), string.Empty, StringComparison.Ordinal)
            .Should().Be(text, "nothing else changed");
    }

    [Theory]
    [InlineData("\"sensitivity\": 101", "detector.sensitivity: 0 to 100")]
    [InlineData("\"smallestObjectPercent\": -1", "detector.smallestObjectPercent: 0 to 100")]
    [InlineData("\"stoppedObjectSeconds\": 5", "detector.stoppedObjectSeconds: 10 to 600")]
    public void Settings_Out_Of_Range_Are_Named(
        string setting,
        string error)
    {
        var json = $$"""{ "version": 1, "name": "x", "detector": { {{setting}} } }""";

        var read = () => StorybookReader.Read(json);

        read.Should().Throw<StorybookException>().WithMessage($"*{error}*");
    }

    private static string ScenePath(string file)
        => Path.Combine(AppContext.BaseDirectory, "Scenes", file);
}