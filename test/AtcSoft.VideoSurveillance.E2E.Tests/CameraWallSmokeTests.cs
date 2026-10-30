namespace AtcSoft.VideoSurveillance.E2E;

/// <summary>
/// CameraWall from start to close in a scratch folder: every ribbon tab, every dialog the
/// ribbon, the backstage and a tile's menu open (each tab and drop-down in them), then the
/// log must hold no binding error and no unhandled exception.
/// </summary>
/// <remarks>
/// A tile's context menu is left out: it needs a real right click on the video, which does
/// not open the menu reliably from a test (see docs/e2e-smoke-tests.md).
/// Left out on purpose as well: the snapshot (a save dialog), folder pickers, opening the recordings
/// folder (Explorer), update downloads (a browser), deleting a recording, Exit and restoring
/// the default settings.
/// </remarks>
[Collection(E2ETestGroup.Name)]
public sealed class CameraWallSmokeTests
{
    [Fact]
    public void Every_Window_Opens_Without_Binding_Errors()
    {
        E2ESettings.SkipUnlessEnabled();

        using var run = new CameraWallRun();
        var app = run.App;
        var main = app.WaitForWindowTitled(CameraWallRun.MainWindowTitle, DesktopApp.StartTimeout);
        var steps = new SmokeSteps(recover: () => CloseDialogs(app, main));

        if (run.HasLiveCamera)
        {
            steps.Run("live camera connects", () => app.Find(main, $"{TestCameras.LiveCameraName}: Connected", timeout: TimeSpan.FromSeconds(30)));
        }

        // Ribbon: Layouts
        steps.Run("Layouts tab", () => SelectTab(app, main, "Layouts"));
        steps.Run("ribbon drop-downs", () => app.OpenEveryDropDown(main));
        steps.Run("New Layout", () => Walk(app, main, "New Layout"));
        steps.Run("Rename Layout", () => Walk(app, main, "Rename Layout"));
        steps.Run("Assign Camera", () => Walk(app, main, "Assign Camera"));

        // Ribbon: Cameras
        steps.Run("Cameras tab", () => SelectTab(app, main, "Cameras"));
        steps.Run("Add Camera", () => Walk(app, main, "Add Camera"));
        steps.Run("draw a motion zone", () => DrawMotionZone(app, main));
        steps.Run("Add USB Camera", () => Walk(app, main, "Add USB Camera"));
        steps.Run("Reconnect All", () => DesktopApp.Press(app.Find(main, "Reconnect All", ControlType.Button)));

        // Ribbon: View
        steps.Run("View tab", () => SelectTab(app, main, "View"));
        steps.Run("Browse Recordings", () => Walk(app, main, "Browse Recordings"));
        steps.Run("play a recording with its activity timeline", () => PlayWithTimeline(app, main, run.DataFolder));
        steps.Run("Timeline and play all from here", () => TimelineAndPlayAll(app, main));
        steps.Run("analyze an older recording", () => AnalyseOlderRecording(app, main));

        // Backstage
        steps.Run("Settings", () => app.WalkAndClose(app.OpenWindow(() => app.PressBackstageItem(main, "Settings"), "Settings")));
        steps.Run("About", () => app.WalkAndClose(app.OpenWindow(() => app.PressBackstageItem(main, "About"), "About")));
        steps.Run("Check for Updates", () => app.WalkAndClose(app.OpenWindow(() => app.PressBackstageItem(main, "Check for Updates"), "Check for Updates")));
        steps.Run("leave the backstage", () => SelectTab(app, main, "Layouts"));

        var closed = app.CloseApp(main);

        steps.Failures.Should().BeEmpty(steps.Report());
        LogFindings.AssertClean(run.LogFolder, "CameraWall");
        foreach (var file in Directory.EnumerateFiles(run.DataFolder, "cameras.json*"))
        {
            File.ReadAllText(file).Should().NotContain(CameraWallRun.CameraPassword, $"camera passwords are kept encrypted ({Path.GetFileName(file)})");
        }

        closed.Should().BeTrue("CameraWall closes when its main window closes");
    }

    private static void SelectTab(
        DesktopApp app,
        Window main,
        string name)
        => DesktopApp.Press(app.Find(main, name, ControlType.TabItem));

    private static void Walk(
        DesktopApp app,
        Window main,
        string buttonName)
        => app.WalkAndClose(app.OpenWindow(() => DesktopApp.Press(app.Find(main, buttonName, ControlType.Button)), buttonName));

    // The zone editor in the camera dialog: a zone added with its button is picked, so it can get
    // a corner, which is picked in turn. The dialog is closed without saving.
    private static void DrawMotionZone(
        DesktopApp app,
        Window main)
    {
        var dialog = app.OpenWindow(() => DesktopApp.Press(app.Find(main, "Add Camera", ControlType.Button)), "Add Camera");
        DesktopApp.Press(app.Find(dialog, "Motion zones", ControlType.TabItem));
        app.Find(dialog, "Remove zone", ControlType.Button).IsEnabled.Should().BeFalse("no zone is picked yet");

        DesktopApp.Press(app.Find(dialog, "Add zone", ControlType.Button));

        // The picked zone's kind can be chosen now: its drop-down is opened like every other.
        app.OpenEveryDropDown(dialog);
        var addCorner = app.Find(dialog, "Add corner", ControlType.Button);
        addCorner.IsEnabled.Should().BeTrue("the new zone is picked");
        DesktopApp.Press(addCorner);
        app.Find(dialog, "Remove corner", ControlType.Button).IsEnabled.Should().BeTrue("the new corner is picked");

        // The pointer: the new corner is in the middle of the zone's top edge (0.4, 0.25 of the
        // grey picture). A double click on it removes it; one on the edge there adds one again.
        var canvas = app.FindWhere(dialog, e => e.ControlType == ControlType.Custom && e.Name == "Motion zones", "the zone drawing");
        var topMiddle = OnPicture(canvas, 0.4, 0.25);
        FlaUI.Core.Input.Mouse.DoubleClick(topMiddle);
        DesktopApp.Settle(TimeSpan.FromSeconds(1));
        app.Find(dialog, "Remove corner", ControlType.Button).IsEnabled.Should().BeFalse("the corner is gone, none is picked");

        FlaUI.Core.Input.Mouse.DoubleClick(topMiddle);
        DesktopApp.Settle(TimeSpan.FromSeconds(1));
        app.Find(dialog, "Remove corner", ControlType.Button).IsEnabled.Should().BeTrue("a corner is added on the edge and picked");

        app.CloseWindow(dialog);
    }

    // A point of the picture in the zone drawing: the picture keeps its shape (16:9 while there is
    // none) in the middle of the drawing.
    private static System.Drawing.Point OnPicture(
        AutomationElement canvas,
        double x,
        double y)
    {
        var bounds = canvas.BoundingRectangle;
        var picture = AtcSoft.VideoSurveillance.Helpers.MotionDetection.MotionZoneEditing.Fit(
            bounds.Width,
            bounds.Height,
            AtcSoft.VideoSurveillance.Helpers.MotionDetection.MotionZoneEditing.DefaultAspectRatio);
        return new System.Drawing.Point(
            (int)Math.Round(bounds.X + picture.X + (x * picture.Width)),
            (int)Math.Round(bounds.Y + picture.Y + (y * picture.Height)));
    }

    // The seeded recordings have an activity index with one event: the player shows the timeline,
    // and "Next event" jumps to it.
    private static void PlayWithTimeline(
        DesktopApp app,
        Window main,
        string dataFolder)
    {
        var browser = app.OpenWindow(() => DesktopApp.Press(app.Find(main, "Browse Recordings", ControlType.Button)), "Browse Recordings");
        var row = app.FindWhere(browser, e => e.ControlType == ControlType.DataItem, "a recording row");
        row.Patterns.SelectionItem.Pattern.Select();

        var player = app.OpenWindow(() => DesktopApp.Press(app.Find(browser, "Play", ControlType.Button)), "Play");
        var timeline = app.Find(player, "Activity", timeout: TimeSpan.FromSeconds(20));
        app.ShouldCoverTheTaskbar(player);
        HoverTimeline(app, timeline);

        // A right-click on the event: boxed correctly, written to the review beside the recording.
        VerdictWalk.Give(app, player, timeline, "Boxed correctly");
        VerdictWalk.SpansUnder(dataFolder).Should().ContainSingle().Which.Verdict.Should().Be(ReviewVerdicts.Correct);
        DesktopApp.Press(app.Find(player, "Next event", ControlType.Button));
        DesktopApp.Press(app.Find(player, "Previous event", ControlType.Button));
        DesktopApp.Press(app.Find(player, "Play events only"));

        // A three-second recording is shorter than the shortest zoom: the buttons are there, not usable.
        foreach (var name in new[] { "Zoom in", "Zoom out" })
        {
            app.Find(player, name, ControlType.Button);
        }

        app.CloseWindow(player);
        app.CloseWindow(browser);
    }

    // Resting the mouse on the middle of the timeline (a second and a half into the three) tells
    // what moved there, in a tooltip that follows the mouse. The recording had no previews: playing
    // it queues them, the player asks again, and the tooltip shows the picture once they are made.
    private static void HoverTimeline(
        DesktopApp app,
        AutomationElement timeline)
    {
        var bounds = timeline.BoundingRectangle;
        var middle = new System.Drawing.Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));

        // A WPF tooltip is a pop-up window of the app's own, with the tooltip inside it.
        var desktop = app.Automation.GetDesktop();
        var processId = timeline.Properties.ProcessId.Value;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        string? words = null;
        var hasPicture = false;
        var nudge = 1;
        while (DateTime.UtcNow < deadline && (words is null || !hasPicture))
        {
            // The tooltip's picture and words change as the mouse moves.
            FlaUI.Core.Input.Mouse.MoveTo(middle with { X = middle.X + (nudge = -nudge) });
            Thread.Sleep(500);
            var tips = desktop
                .FindAllChildren(app.Conditions.ByProcessId(processId))
                .SelectMany(w => w.Properties.ControlType.ValueOrDefault == ControlType.ToolTip
                    ? [w]
                    : w.FindAllDescendants(app.Conditions.ByControlType(ControlType.ToolTip)))
                .ToList();
            words = tips
                .SelectMany(t => new[] { t.Properties.Name.ValueOrDefault }.Concat(t.FindAllDescendants().Select(d => d.Properties.Name.ValueOrDefault)))
                .FirstOrDefault(n => n?.Contains("Moving", StringComparison.Ordinal) == true);
            hasPicture = tips.Any(t => t.FindFirstDescendant(app.Conditions.ByControlType(ControlType.Image)) is { IsOffscreen: false });
        }

        words.Should().NotBeNull("resting the mouse on the timeline shows a tooltip").And.Contain("Moving: 1");
        hasPicture.Should().BeTrue("the previews are made after the recording is played, and the tooltip shows its picture");
        FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point(bounds.X, bounds.Y - 200));
    }

    // The oldest recording was seeded without an activity index: analysing it reads the file
    // through the engine and writes one.
    private static void AnalyseOlderRecording(
        DesktopApp app,
        Window main)
    {
        var browser = app.OpenWindow(() => DesktopApp.Press(app.Find(main, "Browse Recordings", ControlType.Button)), "Browse Recordings");
        app.FindWhere(browser, e => e.ControlType == ControlType.DataItem, "a recording row");
        var rows = browser.FindAllDescendants(app.Conditions.ByControlType(ControlType.DataItem));
        rows[^1].Patterns.SelectionItem.Pattern.Select();

        DesktopApp.Press(app.Find(browser, "Analyze recording", ControlType.Button));
        app.Find(browser, "The recording has its activity timeline now.", timeout: TimeSpan.FromSeconds(60));
        app.CloseWindow(browser);
    }

    // Every camera has a lane from the seeded recordings; play all opens a tile per camera, each
    // recording opens, and one pause stops them all.
    private static void TimelineAndPlayAll(
        DesktopApp app,
        Window main)
    {
        var timeline = app.OpenWindow(() => DesktopApp.Press(app.Find(main, "Timeline", ControlType.Button)), "Timeline");
        app.FindWhere(
            timeline,
            e => e.ControlType == ControlType.Custom && e.Name == TestCameras.DeadCameraName,
            "a timeline lane for the offline camera",
            TimeSpan.FromSeconds(20));

        var grid = app.OpenWindow(() => DesktopApp.Press(app.Find(timeline, "Play all from here", ControlType.Button)), "Play all from here");
        app.Find(grid, TestCameras.DeadCameraName, timeout: TimeSpan.FromSeconds(20));
        app.WaitUntilGone(grid, "Loading...", ControlType.Text, TimeSpan.FromSeconds(20)).Should().BeTrue("the recordings should open");
        app.TryFind(grid, "Could not be played", ControlType.Text).Should().BeNull();
        DesktopApp.Press(app.Find(grid, "Pause", ControlType.Button));
        app.Find(grid, "Play", ControlType.Button);

        app.CloseWindow(grid);
        app.CloseWindow(timeline);
    }

    private static void CloseDialogs(
        DesktopApp app,
        Window main)
    {
        foreach (var window in app.Windows().Where(w => !w.Title.Contains(CameraWallRun.MainWindowTitle, StringComparison.OrdinalIgnoreCase)))
        {
            app.CloseWindow(window);
        }

        _ = main;
    }
}