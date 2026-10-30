namespace AtcSoft.VideoSurveillance.E2E;

/// <summary>
/// The bounding box sample (<c>sample/BoundingBoxWpfApp</c>): it opens a storybook or a
/// recording, and what it shows is checked through UI Automation against a run of the same file
/// made here, and the storybook's score against the committed baseline. The overlay shows each
/// box as an element (<c>box-{trackId}</c>, the box in video pixels as its help text).
/// </summary>
[Collection(E2ETestGroup.Name)]
public sealed class BoundingBoxSampleSmokeTests
{
    private static readonly TimeSpan AnalysisTimeout = TimeSpan.FromMinutes(2);

    private static string ScenesFolder
        => Path.Combine(E2ESettings.RepositoryRoot, "test", "AtcSoft.VideoSurveillance.Storybook.Tests", "Scenes");

    [Fact]
    public void A_Storybook_Shows_The_Detectors_Boxes_And_The_Truth_Of_Each_Picture()
    {
        E2ESettings.SkipUnlessEnabled();

        const string sceneFile = "s05-car-human-cat.storybook.json";
        var scene = StorybookReader.ReadFile(Path.Combine(ScenesFolder, sceneFile));
        var expected = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()), new StorybookRunOptions(AnalysesPerSecond: 4));

        using var run = new BoundingBoxSampleRun(["--storybook", Path.Combine(ScenesFolder, sceneFile), "--rate", "4"]);
        var app = run.App;
        var window = app.WaitForWindowTitled(BoundingBoxSampleRun.WindowTitle, DesktopApp.StartTimeout);
        WaitUntilReady(app, window);

        // The score panel: the run made here, and the baseline the storybook tests keep.
        using (var json = JsonDocument.Parse(Element(app, window, "ScoreJson").Patterns.Value.Pattern.Value.Value))
        {
            var score = json.RootElement.GetProperty("score");
            score.GetProperty("recall").GetDouble().Should().Be(expected.Score.Recall);
            score.GetProperty("falseBoxesPerFrame").GetDouble().Should().Be(expected.Score.FalseBoxesPerFrame);
            score.GetProperty("trackCount").GetInt32().Should().Be(expected.Score.TrackCount);
            json.RootElement.GetProperty("way").GetString().Should().Be("fast");

            var baseline = Baseline("s05-car-human-cat@4/builtIn");
            score.GetProperty("recall").GetDouble().Should().BeGreaterThanOrEqualTo(baseline.GetProperty("recall").GetDouble() - 0.005);
            score.GetProperty("falseBoxesPerFrame").GetDouble().Should().BeLessThanOrEqualTo(baseline.GetProperty("falseBoxesPerFrame").GetDouble() + 0.005);
        }

        // A picture with all three moving, and the last picture, after they have gone.
        var busy = expected.Frames.ToList().FindIndex(f => f.Detected.Count == 3);
        busy.Should().BePositive("S5 has a moment with the car, the human and the cat all boxed");
        foreach (var index in new[] { busy, expected.Frames.Count - 1 })
        {
            GoTo(app, window, index, expected.Frames.Count);
            var frame = expected.Frames[index];

            Boxes(app, window, "DetectorBoxes").Should().BeEquivalentTo(
                frame.Detected.Select((d, i) => (d.TrackId is { } id ? $"box-{id}" : $"box-at-{i}", Describe(d.Box))),
                $"the detector's boxes of picture {index}");
            Boxes(app, window, "TruthBoxes").Select(b => b.Box).Should().BeEquivalentTo(
                frame.Truth.Where(t => !t.IsDistractor && t.State == TruthState.MustBox).Select(t => Describe(t.Box)),
                $"the objects to box in picture {index}");

            // A picture of the window in the run folder, to look at.
            WindowPicture.Save(window, Path.Combine(run.DataFolder, $"s05-picture-{index}.png"));
        }

        var closed = app.CloseApp(window);
        LogFindings.AssertClean(run.LogFolder, "the bounding box sample");
        closed.Should().BeTrue("the sample closes with its window");
    }

    [Fact]
    public void The_Debug_Layers_Say_Why_And_The_Tuning_Runs_Again_At_Once()
    {
        E2ESettings.SkipUnlessEnabled();

        // A copy of S5, so saving settings into it leaves the suite's scene alone.
        const string sceneFile = "s05-car-human-cat.storybook.json";
        var copy = Path.Combine(E2ESettings.NewFolder("tuning-" + Guid.NewGuid().ToString("N")[..8]), sceneFile);
        File.Copy(Path.Combine(ScenesFolder, sceneFile), copy);
        var scene = StorybookReader.ReadFile(copy);
        var options = new StorybookRunOptions(AnalysesPerSecond: 4);
        var expected = StorybookRunner.Run(scene, new BuiltInMotionDetector(scene.Detector.Settings()) { KeepDiagnostics = true }, options);
        var coarse = new AtcSoft.VideoSurveillance.Models.Settings.MotionDetectionSettings { SmallestObjectPercent = 0.5 };
        var tuned = StorybookRunner.Run(scene, new BuiltInMotionDetector(coarse), options);
        tuned.Score.Recall.Should().BeLessThan(expected.Score.Recall, "a smallest object of 0.5 % loses the cat");

        using var run = new BoundingBoxSampleRun(["--storybook", copy, "--rate", "4"]);
        var app = run.App;
        var window = app.WaitForWindowTitled(BoundingBoxSampleRun.WindowTitle, DesktopApp.StartTimeout);
        WaitUntilReady(app, window);

        // The time the detector took, beside the score and not in it.
        Element(app, window, "DetectorTime").Name.Should().StartWith("Detector: ").And.Contain("ms a picture");
        using (var json = JsonDocument.Parse(Element(app, window, "ScoreJson").Patterns.Value.Pattern.Value.Value))
        {
            json.RootElement.GetProperty("detectorMilliseconds").GetProperty("mean").GetDouble().Should().BePositive();
        }

        // The debug layers at a picture with all three boxed: the stage, the blobs and their labels.
        var busy = expected.Frames.ToList().FindIndex(f => f.Detected.Count == 3);
        GoTo(app, window, busy, expected.Frames.Count);
        var diagnostics = expected.Frames[busy].Diagnostics!;
        var shown = diagnostics.Candidates.Count(c => c.State == MotionCandidateState.Shown);
        Element(app, window, "DebugInfo").Name.Should().StartWith("analysed").And.Contain($"{shown} shown");
        var labels = CandidateLabels(app, window);
        labels.Should().HaveCount(diagnostics.Candidates.Count);
        labels.Should().Contain(l => l.EndsWith("held", StringComparison.Ordinal), "a track that has gone far is held out of the learning");
        Element(app, window, "ShowCandidatesCheckBox").AsCheckBox().IsChecked = false;
        WaitFor(app, () => CandidateLabels(app, window).Count == 0, "the blobs hidden");
        Element(app, window, "ShowCandidatesCheckBox").AsCheckBox().IsChecked = true;
        WindowPicture.Save(window, Path.Combine(run.DataFolder, "debug-layers.png"));

        // The tuning: a coarser smallest object runs again over the pictures kept, as a run made here.
        Element(app, window, "TuningInfo").Name.Should().Be("The file's own settings.");
        Element(app, window, "SmallestObjectTextBox").AsTextBox().Text = "0.5";
        WaitFor(app, () => Math.Abs(Score(app, window).Recall - tuned.Score.Recall) < 1e-9, "the run with a smallest object of 0.5 %");
        Score(app, window).FalseBoxesPerFrame.Should().Be(tuned.Score.FalseBoxesPerFrame);
        Element(app, window, "TuningInfo").Name.Should().StartWith("Tuned");
        Element(app, window, "FrameInfo").Name.Should().Contain($"picture {busy + 1} of", "the picture shown stays");

        // Saved into the storybook: only the detector section, and the file's settings are now the tuning.
        Element(app, window, "SaveStorybookSettingsButton").AsButton().Invoke();
        WaitFor(app, () => Element(app, window, "TuningInfo").Name.StartsWith("Saved", StringComparison.Ordinal), "the settings saved");
        StorybookReader.ReadFile(copy).Detector.Settings().Should().BeEquivalentTo(coarse);
        Element(app, window, "ResetTuningButton").AsButton().Invoke();
        WaitFor(app, () => Element(app, window, "TuningInfo").Name == "The file's own settings.", "the file's settings, now the saved ones");
        Element(app, window, "SmallestObjectTextBox").AsTextBox().Text.Should().Be("0.5");

        var closed = app.CloseApp(window);
        LogFindings.AssertClean(run.LogFolder, "the bounding box sample");
        closed.Should().BeTrue("the sample closes with its window");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void The_Live_Clock_Plays_Through_The_Player_With_The_Boxes_On_The_Walker(
        int rotation)
    {
        E2ESettings.SkipUnlessEnabled();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        Assert.SkipUnless(folder is not null && File.Exists(Path.Combine(folder, "ffmpeg.exe")), "The FFmpeg command-line tool streams the video.");

        // S3 streamed to the engine's player and turned by it (S7): the boxes are found in
        // VideoHost's overlay window, and lie on the walker as the truth turned the same way.
        using var run = new BoundingBoxSampleRun(["--storybook", Path.Combine(ScenesFolder, "s03-slow-walker.storybook.json"), "--rate", "4", "--live", rotation.ToString(CultureInfo.InvariantCulture)]);
        var app = run.App;
        var window = app.WaitForWindowTitled(BoundingBoxSampleRun.WindowTitle, DesktopApp.StartTimeout);
        var live = app.WaitForWindowTitled("Live - ", AnalysisTimeout);
        live.Title.Should().Contain($"turned {rotation}°");

        var onTheWalker = 0;
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        string status;
        while (true)
        {
            status = LiveElement(app, "LiveStatus")?.Name ?? string.Empty;
            if (status.StartsWith("Ended", StringComparison.Ordinal) || status.StartsWith("Stopped", StringComparison.Ordinal) || DateTime.UtcNow > deadline)
            {
                break;
            }

            var detected = LiveBoxes(app, "LiveDetectorBoxes");
            var truth = LiveBoxes(app, "LiveTruthBoxes");
            if (detected.Any(d => truth.Any(t => Overlap(d, t) > 0.3)))
            {
                if (onTheWalker == 0)
                {
                    WindowPicture.Save(live, Path.Combine(run.DataFolder, $"live-{rotation}.png"));
                }

                onTheWalker++;
            }

            DesktopApp.Settle(TimeSpan.FromMilliseconds(200));
        }

        status.Should().StartWith("Ended", "S3 plays to its end");
        onTheWalker.Should().BePositive("a box in the overlay window lies on the walker at least once (seen through UI Automation)");
        var score = LiveElement(app, "LiveScore")?.Name ?? string.Empty;
        score.Should().StartWith("recall ");
        var recall = double.Parse(score["recall ".Length..score.IndexOf(',', StringComparison.Ordinal)], CultureInfo.InvariantCulture);
        recall.Should().BeGreaterThan(0.5, $"the walker is boxed for most of the stretch it must be ({score})");

        live.Close();
        var closed = app.CloseApp(window);
        LogFindings.AssertClean(run.LogFolder, "the bounding box sample");
        closed.Should().BeTrue("the sample closes with its window");
    }

    [Fact]
    public void The_Live_Clock_Plays_A_Recording_To_Its_End()
    {
        E2ESettings.SkipUnlessEnabled();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, "ffmpeg.exe");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool makes the recording and streams it.");

        // A recording of a slow walker, made from the storybook: no truth, so the live clock plays it
        // until its stream ends, not to a known last picture.
        var scene = StorybookReader.ReadFile(Path.Combine(ScenesFolder, "s03-slow-walker.storybook.json"));
        scene.Video.BitrateKbps = 2000;
        var recording = Path.Combine(E2ESettings.NewFolder("bounding-box-live-recording-" + Guid.NewGuid().ToString("N")[..8]), "slow-walker.mkv");
        StorybookEncoder.Encode(scene, tool, recording, TestContext.Current.CancellationToken);

        using var run = new BoundingBoxSampleRun(["--recording", recording, "--rate", "4", "--live", "0"]);
        var app = run.App;
        var window = app.WaitForWindowTitled(BoundingBoxSampleRun.WindowTitle, DesktopApp.StartTimeout);
        var live = app.WaitForWindowTitled("Live - ", AnalysisTimeout);

        var boxed = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        string status;
        while (true)
        {
            status = LiveElement(app, "LiveStatus")?.Name ?? string.Empty;
            if (status.StartsWith("Ended", StringComparison.Ordinal) || DateTime.UtcNow > deadline)
            {
                break;
            }

            boxed |= LiveBoxes(app, "LiveDetectorBoxes").Count > 0;
            DesktopApp.Settle(TimeSpan.FromMilliseconds(200));
        }

        status.Should().StartWith("Ended");
        var endedAt = double.Parse(status.Split(' ')[2], CultureInfo.InvariantCulture);
        endedAt.Should().BeGreaterThan(scene.Video.DurationSeconds - 1, "a recording plays until its stream ends");
        boxed.Should().BeTrue("the walker is boxed in the overlay window");
        (LiveElement(app, "LiveScore")?.Name ?? string.Empty).Should().Contain("(no truth)");

        live.Close();
        var closed = app.CloseApp(window);
        LogFindings.AssertClean(run.LogFolder, "the bounding box sample");
        closed.Should().BeTrue("the sample closes with its window");
    }

    [Fact]
    public void A_Recording_Shows_The_Detectors_Boxes_And_Is_Reviewed()
    {
        E2ESettings.SkipUnlessEnabled();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, "ffmpeg.exe");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool makes the recording.");
        VideoEngineBootstrap.Initialize();

        // A recording of a slow walker, made from the storybook, opened as a plain recording.
        var scene = StorybookReader.ReadFile(Path.Combine(ScenesFolder, "s03-slow-walker.storybook.json"));
        scene.Video.BitrateKbps = 2000;
        var recording = Path.Combine(E2ESettings.NewFolder("bounding-box-recording"), "slow-walker.mkv");
        StorybookEncoder.Encode(scene, tool, recording, TestContext.Current.CancellationToken);

        // As Analyse recording reads it: the first seconds only teach the scene.
        var detector = new BuiltInMotionDetector(new AtcSoft.VideoSurveillance.Models.Settings.MotionDetectionSettings()) { WarmUpSeconds = RecordingAnalysis.WarmUpSeconds };
        var expected = StorybookRunner.RunRecording(recording, detector, new StorybookRunOptions(AnalysesPerSecond: 4), TestContext.Current.CancellationToken);

        using var run = new BoundingBoxSampleRun(["--recording", recording]);
        var app = run.App;
        var window = app.WaitForWindowTitled(BoundingBoxSampleRun.WindowTitle, DesktopApp.StartTimeout);
        WaitUntilReady(app, window);

        Element(app, window, "ScoreSummary").Name.Should().Contain("no truth");
        var boxed = expected.Frames.ToList().FindIndex(f => f.Detected.Count > 0);
        boxed.Should().BePositive("the walker is found");
        GoTo(app, window, boxed, expected.Frames.Count);
        Boxes(app, window, "DetectorBoxes").Should().BeEquivalentTo(
            expected.Frames[boxed].Detected.Select((d, i) => (d.TrackId is { } id ? $"box-{id}" : $"box-at-{i}", Describe(d.Box))));
        Boxes(app, window, "TruthBoxes").Should().BeEmpty("a recording has no truth");

        // Review: the walker marked as one person moving for two seconds, then saved beside the recording.
        Element(app, window, "ReviewInfo").Name.Should().Contain("No review yet");
        Element(app, window, "MarkStartButton").AsButton().Invoke();
        GoTo(app, window, boxed + 8, expected.Frames.Count);
        Element(app, window, "AddSpanButton").AsButton().Invoke();
        WaitFor(app, () => Element(app, window, "ScoreSummary").Name.StartsWith("Review: recall", StringComparison.Ordinal), "the run scored by the review");
        Element(app, window, "ReviewSpans").FindAllChildren().Should().ContainSingle();
        using (var json = JsonDocument.Parse(Element(app, window, "ScoreJson").Patterns.Value.Pattern.Value.Value))
        {
            json.RootElement.GetProperty("scoredBy").GetString().Should().Be("review");
            json.RootElement.GetProperty("score").GetProperty("recall").GetDouble().Should().BeGreaterThan(0.9, "the walker is boxed in the stretch marked");
        }

        Element(app, window, "SaveReviewButton").AsButton().Invoke();
        var reviewPath = RecordingPathHelper.BuildReviewPath(recording);
        WaitFor(app, () => File.Exists(reviewPath), "the review saved beside the recording");
        var saved = RecordingReviewFile.Read(reviewPath);
        saved.Recording.Sha256.Should().Be(RecordingReviewFile.Sha256Of(recording));
        saved.Spans.Should().ContainSingle().Which.Verdict.Should().Be(ReviewVerdicts.Correct);
        saved.Spans[0].From.TotalSeconds.Should().BeApproximately(expected.Frames[boxed].Time, 0.001);
        WindowPicture.Save(window, Path.Combine(run.DataFolder, "recording-review.png"));

        var closed = app.CloseApp(window);
        LogFindings.AssertClean(run.LogFolder, "the bounding box sample");
        closed.Should().BeTrue("the sample closes with its window");
    }

    [Fact]
    public void A_Sweep_Ranks_The_Motion_Settings_Over_A_Folder_Of_Scenes()
    {
        E2ESettings.SkipUnlessEnabled();

        // Two short scenes, and an empty corpus, so the sweep is quick and the same on every machine.
        var scenes = E2ESettings.NewFolder("sweep-scenes-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var file in new[] { "s01-empty-noise.storybook.json", "s02-tree-in-the-wind.storybook.json" })
        {
            File.Copy(Path.Combine(ScenesFolder, file), Path.Combine(scenes, file));
        }

        var corpus = E2ESettings.NewFolder("sweep-corpus-" + Guid.NewGuid().ToString("N")[..8]);
        using var run = new BoundingBoxSampleRun(["--sweep", scenes, "--corpus", corpus]);
        var app = run.App;
        var window = app.WaitForWindowTitled(BoundingBoxSampleRun.WindowTitle, DesktopApp.StartTimeout);
        WaitUntilReady(app, window);

        Element(app, window, "ScoreSummary").Name.Should().Contain("shipped defaults");
        Element(app, window, "ScoreJson").Patterns.Value.Pattern.Value.Value.Should().StartWith("# Motion settings sweep")
            .And.Contain(" (defaults) |");
        Directory.GetFiles(Path.Combine(run.DataFolder, "sweeps"), "sweep-*.md").Should().ContainSingle("the report is kept in the data folder");
        WindowPicture.Save(window, Path.Combine(run.DataFolder, "sweep.png"));

        var closed = app.CloseApp(window);
        LogFindings.AssertClean(run.LogFolder, "the bounding box sample");
        closed.Should().BeTrue("the sample closes with its window");
    }

    // An element of the live window or of VideoHost's overlay window, a child window WPF does not
    // know of: it is not in the live window's tree, so it is asked for by its handle.
    private static AutomationElement? LiveElement(
        DesktopApp app,
        string automationId)
    {
        foreach (var window in app.Windows().Where(w => w.Title.StartsWith("Live - ", StringComparison.Ordinal)))
        {
            try
            {
                if (ChildWindows.Find(app, window, automationId) is { } found)
                {
                    return found;
                }
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or TimeoutException or InvalidOperationException)
            {
                // The window went while it was searched.
            }
        }

        return null;
    }

    // The boxes an overlay in the live window shows, in video pixels.
    private static List<(double X, double Y, double Width, double Height)> LiveBoxes(
        DesktopApp app,
        string overlayId)
    {
        try
        {
            return LiveElement(app, overlayId)?
                .FindAllChildren()
                .Select(b => b.Properties.HelpText.ValueOrDefault ?? string.Empty)
                .Select(t => t.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray())
                .Where(v => v.Length == 4)
                .Select(v => (v[0], v[1], v[2], v[3]))
                .ToList() ?? [];
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or TimeoutException or InvalidOperationException or FormatException)
        {
            return [];
        }
    }

    // The overlap of two boxes over their union.
    private static double Overlap(
        (double X, double Y, double Width, double Height) a,
        (double X, double Y, double Width, double Height) b)
    {
        var across = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var down = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        if (across <= 0 || down <= 0)
        {
            return 0;
        }

        var shared = across * down;
        return shared / ((a.Width * a.Height) + (b.Width * b.Height) - shared);
    }

    // The labels of the blobs drawn over the picture.
    private static List<string> CandidateLabels(
        DesktopApp app,
        AutomationElement window)
        => Element(app, window, "Candidates")
            .FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text))
            .Select(t => t.Properties.Name.ValueOrDefault ?? string.Empty)
            .ToList();

    private static (double Recall, double FalseBoxesPerFrame) Score(
        DesktopApp app,
        AutomationElement window)
    {
        using var json = JsonDocument.Parse(Element(app, window, "ScoreJson").Patterns.Value.Pattern.Value.Value);
        var score = json.RootElement.GetProperty("score");
        return (score.GetProperty("recall").GetDouble(), score.GetProperty("falseBoxesPerFrame").GetDouble());
    }

    private static string Describe(PixelBox box)
        => string.Create(CultureInfo.InvariantCulture, $"{box.X:0.##},{box.Y:0.##},{box.Width:0.##},{box.Height:0.##}");

    private static JsonElement Baseline(string key)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(E2ESettings.RepositoryRoot, "test", "AtcSoft.VideoSurveillance.Storybook.Tests", "baseline.json")));
        return document.RootElement.GetProperty(key).Clone();
    }

    private static AutomationElement Element(
        DesktopApp app,
        AutomationElement window,
        string automationId)
        => app.FindWhere(window, e => e.Properties.AutomationId.ValueOrDefault == automationId, $"with the automation id '{automationId}'");

    private static void WaitUntilReady(
        DesktopApp app,
        AutomationElement window)
    {
        var status = Element(app, window, "Status");
        var deadline = DateTime.UtcNow + AnalysisTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var text = status.Properties.Name.ValueOrDefault ?? string.Empty;
            if (text == "Ready")
            {
                return;
            }

            if (text.StartsWith("Failed", StringComparison.Ordinal) || text.Contains("needed", StringComparison.Ordinal))
            {
                throw app.Fail($"The sample could not open the file: {text}");
            }

            Thread.Sleep(250);
        }

        throw app.Fail("The sample did not finish analysing in time.");
    }

    private static void GoTo(
        DesktopApp app,
        AutomationElement window,
        int index,
        int count)
    {
        Element(app, window, "FrameSlider").Patterns.RangeValue.Pattern.SetValue(index);
        var info = Element(app, window, "FrameInfo");
        var expected = $"picture {index + 1} of {count}";
        var deadline = DateTime.UtcNow + DesktopApp.ElementTimeout;
        while (!(info.Properties.Name.ValueOrDefault ?? string.Empty).Contains(expected, StringComparison.Ordinal))
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw app.Fail($"The sample did not show {expected}.");
            }

            Thread.Sleep(100);
        }
    }

    private static void WaitFor(
        DesktopApp app,
        Func<bool> condition,
        string what)
    {
        var deadline = DateTime.UtcNow + DesktopApp.ElementTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw app.Fail($"The sample did not show {what}.");
            }

            Thread.Sleep(100);
        }
    }

    private static List<(string Id, string Box)> Boxes(
        DesktopApp app,
        AutomationElement window,
        string overlayId)
        => Element(app, window, overlayId)
            .FindAllChildren()
            .Select(b => (b.Properties.AutomationId.ValueOrDefault ?? string.Empty, b.Properties.HelpText.ValueOrDefault ?? string.Empty))
            .ToList();
}