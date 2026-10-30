namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// The settings sweep: a scene drawn once runs as on the fast path, a combination goes on top of a
/// scene's own settings, and the results are ranked with the shipped defaults marked.
/// </summary>
public sealed class StorybookSweepTests
{
    [Fact]
    public void A_Scene_Drawn_Once_Runs_As_On_The_Fast_Path()
    {
        var scene = Scene("s05-car-human-cat.storybook.json");
        var options = new StorybookRunOptions(AnalysesPerSecond: 4);

        var drawn = StorybookRunner.RunDrawn(scene, new BuiltInMotionDetector(new MotionDetectionSettings()), StorybookRunner.Draw(scene, options));
        var fast = StorybookRunner.Run(scene, new BuiltInMotionDetector(new MotionDetectionSettings()), options);

        drawn.Score.Should().BeEquivalentTo(fast.Score);
    }

    [Fact]
    public void A_Combination_Goes_On_Top_Of_A_Scenes_Own_Settings()
    {
        var own = new MotionDetectionSettings { IgnoreLightingChanges = false, PostMotionDurationSeconds = 3 };

        var applied = new SweepSettings(55, 0.07, 120).ApplyTo(own);

        (applied.Sensitivity, applied.SmallestObjectPercent, applied.StoppedObjectSeconds).Should().Be((55, 0.07, 120));
        (applied.IgnoreLightingChanges, applied.PostMotionDurationSeconds).Should().Be((false, 3), "the scene's own settings stay");
        own.Sensitivity.Should().Be(30, "the scene's settings are not changed");
        SweepSettings.Defaults.Should().Be(SweepSettings.From(new MotionDetectionSettings()));
        StorybookSweep.DefaultGrid.Should().Contain(SweepSettings.Defaults);
    }

    [Fact]
    public void A_Sweep_Ranks_The_Combinations_And_Says_Where_The_Defaults_Stand()
    {
        SweepScene[] scenes = [new("s02-tree-in-the-wind", Scene("s02-tree-in-the-wind.storybook.json")), new("s08-small-far-cat", Scene("s08-small-far-cat.storybook.json"))];
        var coarse = SweepSettings.Defaults with { SmallestObjectPercent = 0.2 };
        var steps = new List<double>();

        var ranked = StorybookSweep.Run(scenes, [], [coarse, SweepSettings.Defaults], rates: [4], progress: new SyncProgress(steps), cancellationToken: TestContext.Current.CancellationToken);
        var report = StorybookSweep.Report(ranked);
        TestContext.Current.TestOutputHelper?.WriteLine(report);

        ranked.Select(r => r.Settings).Should().Equal(SweepSettings.Defaults, coarse);
        ranked[0].ScenesMet.Should().Be(2);
        ranked[1].Scenes.Single(s => s.Scene == "s08-small-far-cat").Meets.Should().BeFalse("a smallest object of 0.2 % misses the small cat far away");
        steps.Should().Equal(0.5, 1);
        report.Should().Contain("The shipped defaults rank first.").And.Contain("| 2 | sensitivity 30, smallest object 0.2 %");
    }

    [Fact]
    public void The_Report_Says_What_Beats_The_Defaults()
    {
        SweepSceneResult[] good = [new("s1", 4, IsKnownBad: false, Meets: true, 1, 0)];
        SweepSceneResult[] worse = [new("s1", 4, IsKnownBad: false, Meets: false, 0.5, 0.2)];
        var better = new SweepSettings(50, 0.02, 60);

        var report = StorybookSweep.Report(StorybookSweep.Rank([new SweepResult(SweepSettings.Defaults, worse, []), new SweepResult(better, good, [])]));

        report.Should().Contain("The shipped defaults rank 2; the best is sensitivity 50").And.Contain("| s1 | 4/s | yes, 1.000, 0.000 | no, 0.500, 0.200 |");
    }

    private static StorybookScene Scene(string file)
        => StorybookReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Scenes", file));

    // Reports on the caller's thread, so the steps are all there when the sweep returns.
    private sealed class SyncProgress(List<double> steps) : IProgress<double>
    {
        public void Report(double value)
            => steps.Add(value);
    }
}