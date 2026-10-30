namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// The detector's diagnostics, for the sample's debug layers: asking for them changes nothing,
/// and they tell why there is no box (still learning, light, a blob too small, a branch that sways
/// on the spot).
/// </summary>
public sealed class MotionDiagnosticsTests
{
    [Theory]
    [InlineData("s05-car-human-cat.storybook.json", 4)]
    [InlineData("s15-strong-wind.storybook.json", 15)]
    [InlineData("s06-lighting-step.storybook.json", 15)]
    [InlineData("s14-door-camera.storybook.json", 4)]
    public void Keeping_Diagnostics_Changes_No_Box(
        string file,
        double rate)
    {
        var scene = Scene(file);
        var options = new StorybookRunOptions(AnalysesPerSecond: rate);

        var plain = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()), options);
        var diagnosed = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()) { KeepDiagnostics = true }, options);

        diagnosed.Score.Should().BeEquivalentTo(plain.Score);
        diagnosed.Frames.Select(f => f.Detected).Should().BeEquivalentTo(plain.Frames.Select(f => f.Detected), o => o.WithStrictOrdering());
        plain.Frames.Should().OnlyContain(f => f.Diagnostics == null, "without asking, nothing is kept");
        diagnosed.Frames.Should().OnlyContain(f => f.Diagnostics != null);
    }

    [Fact]
    public void The_Shown_Candidates_Are_The_Boxes_Shown()
    {
        var scene = Scene("s05-car-human-cat.storybook.json");
        var run = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()) { KeepDiagnostics = true }, new StorybookRunOptions(AnalysesPerSecond: 4));

        run.Frames[0].Diagnostics!.Stage.Should().Be(MotionDiagnosticsStage.Learning);
        var analysed = run.Frames.Where(f => f.Diagnostics!.Stage == MotionDiagnosticsStage.Analysed && f.Detected.Count > 0).ToList();
        analysed.Should().NotBeEmpty();
        foreach (var frame in analysed)
        {
            var shown = frame.Diagnostics!.Candidates.Where(c => c.State == MotionCandidateState.Shown).ToList();
            shown.Select(c => c.TrackId).Should().BeEquivalentTo(frame.Detected.Select(d => d.TrackId), $"at {frame.Time} s");
            shown.Should().OnlyContain(c => c.Travel >= 1, "a shown track has travelled");
            foreach (var candidate in shown)
            {
                Enumerable.Range((int)candidate.Box.Y, (int)candidate.Box.Height)
                    .Any(y => Enumerable.Range((int)candidate.Box.X, (int)candidate.Box.Width).Any(x => frame.Diagnostics.IsForeground(x, y)))
                    .Should().BeTrue($"the foreground is under track {candidate.TrackId} at {frame.Time} s");
            }
        }
    }

    [Fact]
    public void A_Tree_In_The_Wind_Is_Followed_But_Not_Travelled()
    {
        var scene = Scene("s02-tree-in-the-wind.storybook.json");
        var run = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()) { KeepDiagnostics = true }, new StorybookRunOptions(AnalysesPerSecond: 15));

        var states = run.Frames.SelectMany(f => f.Diagnostics!.Candidates).Select(c => c.State).ToHashSet();
        states.Should().Contain(MotionCandidateState.NotTravelled).And.Contain(MotionCandidateState.TooNew);
        run.Frames.SelectMany(f => f.Diagnostics!.Candidates)
            .Where(c => c.State == MotionCandidateState.NotTravelled)
            .Should().OnlyContain(c => c.Travel < 1 && c.TrackId != null);
    }

    [Fact]
    public void Rain_Leaves_Blobs_Too_Small()
    {
        var scene = Scene("s10-rain.storybook.json");
        var run = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()) { KeepDiagnostics = true }, new StorybookRunOptions(AnalysesPerSecond: 4));

        run.Frames.SelectMany(f => f.Diagnostics!.Candidates)
            .Where(c => c.State == MotionCandidateState.TooSmall)
            .Should().NotBeEmpty().And.OnlyContain(c => c.TrackId == null && !c.Held);
    }

    [Fact]
    public void A_Change_Of_Light_And_The_Warm_Up_Are_Named()
    {
        var lighting = Scene("s06-lighting-step.storybook.json");
        var stages = StorybookRunner.Run(lighting, new BuiltInMotionDetector(lighting.Detector.Settings()) { KeepDiagnostics = true }, new StorybookRunOptions(AnalysesPerSecond: 15))
            .Frames.Select(f => f.Diagnostics!.Stage).ToList();
        stages.Should().Contain(MotionDiagnosticsStage.LightingChange).And.Contain(MotionDiagnosticsStage.Settling).And.NotContain(MotionDiagnosticsStage.WarmUp);

        var walker = Scene("s03-slow-walker.storybook.json");
        var warm = StorybookRunner.Run(walker, new BuiltInMotionDetector(walker.Detector.Settings()) { KeepDiagnostics = true, WarmUpSeconds = 3 }, new StorybookRunOptions(AnalysesPerSecond: 4))
            .Frames;
        warm.Where(f => f.Time is > 0 and < 3).Should().OnlyContain(f => f.Diagnostics!.Stage == MotionDiagnosticsStage.WarmUp);
        warm.Where(f => f.Time > 3).Should().OnlyContain(f => f.Diagnostics!.Stage == MotionDiagnosticsStage.Analysed);
    }

    [Fact]
    public void The_Ignore_Zones_Are_Told()
    {
        var zone = new MotionZone { Name = "Road", Kind = AtcSoft.VideoSurveillance.Enums.MotionZoneKind.Ignore, Points = [new(0, 0), new(0.5, 0), new(0.5, 1), new(0, 1)] };
        var detector = new BuiltInMotionDetector(new MotionDetectionSettings(), zones: [zone]) { KeepDiagnostics = true };
        var scene = Scene("s03-slow-walker.storybook.json");

        var last = StorybookRunner.Run(scene, detector, new StorybookRunOptions(AnalysesPerSecond: 4)).Frames[^1].Diagnostics!;

        last.HasIgnored.Should().BeTrue();
        last.IsIgnored(10, last.Height / 2).Should().BeTrue();
        last.IsIgnored(last.Width - 10, last.Height / 2).Should().BeFalse();
    }

    [Fact]
    public void A_Rerun_Over_The_Kept_Pictures_Is_The_Same_Run()
    {
        var scene = Scene("s05-car-human-cat.storybook.json");
        var first = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()), new StorybookRunOptions(AnalysesPerSecond: 15, KeepPictures: true));

        var again = StorybookRunner.Rerun(first, scene.Expect, new BuiltInMotionDetector(scene.Detector.Settings()));
        var other = StorybookRunner.Rerun(first, scene.Expect, new BuiltInMotionDetector(new MotionDetectionSettings { SmallestObjectPercent = 0.5 }));

        again.Score.Should().BeEquivalentTo(first.Score);
        again.Frames.Select(f => f.Detected).Should().BeEquivalentTo(first.Frames.Select(f => f.Detected), o => o.WithStrictOrdering());
        (again.AnalysisWidth, again.AnalysisHeight, again.VideoWidth, again.VideoHeight).Should().Be((first.AnalysisWidth, first.AnalysisHeight, first.VideoWidth, first.VideoHeight));
        again.Frames.Should().OnlyContain(f => f.Pixels != null, "a rerun keeps the pictures for the next");
        other.Score.Recall.Should().BeLessThan(first.Score.Recall, "a smallest object of 0.5 % loses the cat");

        var withoutPictures = () => StorybookRunner.Rerun(StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings())), scene.Expect, new BuiltInMotionDetector(scene.Detector.Settings()));
        withoutPictures.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Every_Picture_Tells_How_Long_The_Detector_Took()
    {
        var scene = Scene("s03-slow-walker.storybook.json");

        var run = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()), new StorybookRunOptions(AnalysesPerSecond: 4));

        run.Frames.Should().OnlyContain(f => f.DetectorMilliseconds >= 0);
        run.Frames.Sum(f => f.DetectorMilliseconds).Should().BePositive();
    }

    private static StorybookScene Scene(string file)
        => StorybookReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Scenes", file));
}