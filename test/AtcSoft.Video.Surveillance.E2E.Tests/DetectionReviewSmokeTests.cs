namespace AtcSoft.Video.Surveillance.E2E;

/// <summary>
/// Detection Review (<c>tools/AtcSoft.Video.Surveillance.Tools.DetectionReview</c>): it makes the
/// pictures of a recording, a person judges them, comments and compares, the detector runs again
/// over the review, and the same again without a window (<c>--rerun</c>). What it does is read
/// back from the review's <c>boxes.json</c>.
/// </summary>
[Collection(E2ETestGroup.Name)]
public sealed class DetectionReviewSmokeTests
{
    private static readonly TimeSpan WorkTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public void A_Recording_Is_Reviewed_Compared_And_Run_Again()
    {
        E2ESettings.SkipUnlessEnabled();
        var ffmpeg = VideoEngineBootstrap.FindFFmpegPath();
        var tool = ffmpeg is null ? null : Path.Combine(ffmpeg, "ffmpeg.exe");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool makes the recording.");

        // A recording of a slow walker, made from the storybook.
        var scene = StorybookReader.ReadFile(Path.Combine(E2ESettings.RepositoryRoot, "test", "AtcSoft.Video.Surveillance.Storybook.Tests", "Scenes", "s03-slow-walker.storybook.json"));
        scene.Video.BitrateKbps = 2000;
        var recording = Path.Combine(E2ESettings.NewFolder("detection-review-recording-" + Guid.NewGuid().ToString("N")[..8]), "slow-walker.mkv");
        StorybookEncoder.Encode(scene, tool, recording, TestContext.Current.CancellationToken);
        var folder = Path.Combine(Path.GetDirectoryName(recording)!, "slow-walker-review");
        var boxesPath = Path.Combine(folder, "boxes.json");

        using var run = new DetectionReviewRun([recording, "--every", "2"]);
        var app = run.App;
        var window = app.WaitForWindowTitled(DetectionReviewRun.WindowTitle, DesktopApp.StartTimeout);

        // The pictures are made, then opened for review.
        WaitFor(app, () => File.Exists(boxesPath), "the review made", WorkTimeout);
        var status = Element(app, window, "Status");
        WaitFor(app, () => (status.Properties.Name.ValueOrDefault ?? string.Empty).StartsWith("Drag on the picture", StringComparison.Ordinal), "the review opened", WorkTimeout);
        var made = ReadReview(app, boxesPath);
        made.GetProperty("everySeconds").GetDouble().Should().Be(2);
        var pictures = made.GetProperty("pictures");
        pictures.GetArrayLength().Should().BeGreaterThan(2, "a 12 s recording gives a picture every 2 s");
        Directory.GetFiles(Path.Combine(folder, "original")).Should().HaveCount(pictures.GetArrayLength());

        // Good on a picture with the walker boxed: the yellow boxes become the person's.
        var boxed = Enumerable.Range(0, pictures.GetArrayLength() - 1)
            .FirstOrDefault(i => pictures[i].GetProperty("detector").GetArrayLength() > 0, -1);
        boxed.Should().BeGreaterThanOrEqualTo(0, "the walker is boxed in a picture before the last");
        var list = Element(app, window, "Pictures");
        app.FindWhere(list, e => e.ControlType == ControlType.ListItem, "listed as a picture");
        list.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem))[boxed].Patterns.SelectionItem.Pattern.Select();
        DesktopApp.Settle(TimeSpan.FromMilliseconds(300));
        DesktopApp.Press(app.Find(window, "✓ Good (Ctrl+1)", ControlType.Button));
        WaitFor(app, () => Picture(boxesPath, boxed) is { } p && p.TryGetProperty("verdict", out var v) && v.GetString() == "good", "the verdict saved");
        var good = Picture(boxesPath, boxed)!.Value;
        Describe(good.GetProperty("person")).Should().Equal(Describe(good.GetProperty("detector")), "Good takes the yellow boxes as the person's");

        // Good stepped to the next picture: a comment, then nothing to box there.
        const string comment = "The walker leaves the picture.";
        Element(app, window, "Comment").Patterns.Value.Pattern.SetValue(comment);
        WaitFor(app, () => Picture(boxesPath, boxed + 1) is { } p && p.TryGetProperty("comment", out var c) && c.GetString() == comment, "the comment saved");
        DesktopApp.Press(app.Find(window, "Nothing to box here (Ctrl+N)", ControlType.Button));
        WaitFor(app, () => Picture(boxesPath, boxed + 1) is { } p && p.GetProperty("person") is { ValueKind: JsonValueKind.Array } person && person.GetArrayLength() == 0, "nothing to box saved");

        // Compare: the comparison in a dialog of its own, and in compare.md.
        var dialog = app.OpenWindow(() => DesktopApp.Press(app.Find(window, "Compare", ControlType.Button)), "Compare");
        dialog.Title.Should().Contain("Compared");
        File.Exists(Path.Combine(folder, "compare.md")).Should().BeTrue();
        Element(app, window, "CompareText").Name.Should().NotBeNullOrWhiteSpace();
        WindowPicture.Save(dialog, Path.Combine(run.DataFolder, "compared.png"));
        app.CloseWindow(dialog);

        // Run detector again: the boxes are made again, and what the person said stays.
        var before = File.GetLastWriteTimeUtc(boxesPath);
        var rerun = app.Find(window, "Run detector again", ControlType.Button);
        DesktopApp.Press(rerun);
        WaitFor(app, () => File.GetLastWriteTimeUtc(boxesPath) > before && rerun.IsEnabled && (status.Properties.Name.ValueOrDefault ?? string.Empty).StartsWith("Compared: ", StringComparison.Ordinal), "the detector run again", WorkTimeout);
        AssertKept(boxesPath, boxed, comment);
        WindowPicture.Save(window, Path.Combine(run.DataFolder, "review.png"));

        var closed = app.CloseApp(window);
        LogFindings.AssertClean(run.LogFolder, "Detection Review");
        closed.Should().BeTrue("Detection Review closes with its window");

        // The same without a window, for scripts.
        File.Delete(Path.Combine(folder, "compare.md"));
        using var headless = ChildProcess.Start(
            E2ESettings.DetectionReviewExe,
            ["--rerun", folder],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["ATCSOFT_DATA_PATH"] = run.DataFolder },
            Path.Combine(run.DataFolder, "rerun-output.log"));
        headless.Process.WaitForExit(WorkTimeout).Should().BeTrue("--rerun ends by itself");
        headless.Process.ExitCode.Should().Be(0, File.ReadAllText(Path.Combine(folder, "rerun.log")));
        File.Exists(Path.Combine(folder, "compare.md")).Should().BeTrue("--rerun compares too");
        AssertKept(boxesPath, boxed, comment);
    }

    // What the person said survives the detector running again (it finds the same boxes).
    private static void AssertKept(
        string boxesPath,
        int boxed,
        string comment)
    {
        var good = Picture(boxesPath, boxed)!.Value;
        good.GetProperty("verdict").GetString().Should().Be("good");
        Describe(good.GetProperty("person")).Should().Equal(Describe(good.GetProperty("detector")));
        var next = Picture(boxesPath, boxed + 1)!.Value;
        next.GetProperty("comment").GetString().Should().Be(comment);
        next.GetProperty("person").GetArrayLength().Should().Be(0);
    }

    private static List<string> Describe(JsonElement boxes)
        => [.. boxes.EnumerateArray().Select(b => string.Create(CultureInfo.InvariantCulture, $"{b.GetProperty("x").GetInt32()},{b.GetProperty("y").GetInt32()},{b.GetProperty("width").GetInt32()},{b.GetProperty("height").GetInt32()}"))];

    private static JsonElement? Picture(
        string boxesPath,
        int index)
        => TryReadReview(boxesPath) is { } review ? review.GetProperty("pictures")[index] : null;

    private static JsonElement ReadReview(
        DesktopApp app,
        string boxesPath)
        => TryReadReview(boxesPath) ?? throw app.Fail($"Could not read {boxesPath}.");

    // boxes.json as the app last wrote it; null while it is being written.
    private static JsonElement? TryReadReview(string boxesPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(boxesPath));
            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static AutomationElement Element(
        DesktopApp app,
        AutomationElement window,
        string automationId)
        => app.FindWhere(window, e => e.Properties.AutomationId.ValueOrDefault == automationId, $"with the automation id '{automationId}'");

    private static void WaitFor(
        DesktopApp app,
        Func<bool> condition,
        string what,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DesktopApp.ElementTimeout);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw app.Fail($"Detection Review did not show {what}.");
            }

            Thread.Sleep(200);
        }
    }
}