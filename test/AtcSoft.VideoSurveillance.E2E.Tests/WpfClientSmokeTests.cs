namespace AtcSoft.VideoSurveillance.E2E;

/// <summary>
/// The server's WPF client against the test server: every view, every dialog the views, the
/// rows and the backstage open (each tab and drop-down in them), then the client's log must
/// hold no binding error and no unhandled exception, and the server's output no fatal entry.
/// </summary>
/// <remarks>
/// Left out on purpose: Change Server (it starts a new process), Sign out, Start with Windows
/// (it changes Windows' start-up), Export Diagnostics and Capture Snapshot (save dialogs),
/// folder pickers, update downloads (a browser), Delete, and Exit.
/// </remarks>
[Collection(E2ETestGroup.Name)]
public sealed class WpfClientSmokeTests(ServerFixture server)
{
    private static readonly string[] Views = ["Live View", "Dashboard", "Cameras", "Layouts", "Recordings", "Timeline", "Notifications"];

    [Fact]
    public void Every_Window_Opens_Without_Binding_Errors()
    {
        E2ESettings.SkipUnlessEnabled();

        using var run = new WpfClientRun(server);
        var app = run.App;
        var main = app.WaitForWindowTitled(WpfClientRun.MainWindowTitle, DesktopApp.StartTimeout);
        var steps = new SmokeSteps(recover: () => CloseDialogs(app));

        // Every view, with its drop-downs
        foreach (var view in Views)
        {
            steps.Run($"view {view}", () =>
            {
                ShowView(app, main, view);
                app.OpenEveryDropDown(main);
            });
        }

        // Recordings: play the one the fixture recorded, with its activity from the server
        steps.Run("play a recording with its activity timeline", () => PlayWithTimeline(app, main, server.DataFolder));

        // Timeline: every camera gets a lane from the server's timeline
        steps.Run("timeline lanes", () =>
        {
            ShowView(app, main, "Timeline");
            app.FindWhere(
                main,
                e => e.ControlType == ControlType.Custom && e.Name == TestCameras.LiveCameraName,
                "a timeline lane for the live camera",
                TimeSpan.FromSeconds(20));
        });

        // Play all from here: a tile per camera with a recording, one pause for all
        steps.Run("play all from here", () =>
        {
            var grid = app.OpenWindow(() => DesktopApp.Press(app.Find(main, "Play all from here", ControlType.Button)), "Play all from here");
            app.Find(grid, TestCameras.LiveCameraName, timeout: TimeSpan.FromSeconds(20));

            // The live camera's recording plays: its tile stops saying it is loading, and no tile failed.
            app.WaitUntilGone(grid, "Loading...", ControlType.Text, TimeSpan.FromSeconds(20)).Should().BeTrue("the recording should open");
            app.TryFind(grid, "Could not be played", ControlType.Text).Should().BeNull();
            DesktopApp.Press(app.Find(grid, "Pause", ControlType.Button));
            app.Find(grid, "Play", ControlType.Button);
            app.CloseWindow(grid);
        });

        // Cameras: add, and edit each camera
        steps.Run("Cameras view", () => ShowView(app, main, "Cameras"));
        steps.Run("Add Camera", () => Walk(app, main, "Add Camera"));
        foreach (var index in RowButtons(app, main, "Edit"))
        {
            steps.Run($"Edit camera {index + 1}", () => app.WalkAndClose(app.OpenWindow(() => DesktopApp.Press(RowButtons(app, main, "Edit", index)), "Edit")));
        }

        steps.Run("draw a motion zone on a snapshot", () => DrawZoneOnSnapshot(app, main));

        // Layouts: add, and edit each layout
        steps.Run("Layouts view", () => ShowView(app, main, "Layouts"));
        steps.Run("Add Layout", () => Walk(app, main, "Add Layout"));
        foreach (var index in RowButtons(app, main, "Edit"))
        {
            steps.Run($"Edit layout {index + 1}", () => app.WalkAndClose(app.OpenWindow(() => DesktopApp.Press(RowButtons(app, main, "Edit", index)), "Edit")));
        }

        // Backstage
        foreach (var item in new[] { "Settings", "Users", "Change password", "Check for Updates", "About" })
        {
            steps.Run(item, () => app.WalkAndClose(app.OpenWindow(() => app.PressBackstageItem(main, item), item)));
        }

        steps.Run("leave the backstage", () => DesktopApp.Press(app.Find(main, "Home", ControlType.TabItem)));

        var closed = app.CloseApp(main);

        steps.Failures.Should().BeEmpty(steps.Report());
        LogFindings.AssertClean(run.LogFolder, "The WPF client");
        server.ServerFailures().Should().BeEmpty($"the server must not fail while the client walks (output: {server.ServerOutputPath})");
        closed.Should().BeTrue("the client closes when its main window closes");
    }

    private static void ShowView(
        DesktopApp app,
        Window main,
        string view)
    {
        DesktopApp.Press(app.Find(main, "Home", ControlType.TabItem));
        DesktopApp.Press(app.Find(main, view, ControlType.Button));
        DesktopApp.Settle(TimeSpan.FromSeconds(1.5));
    }

    // The server wrote an activity index while the fixture recorded the live camera; whether the
    // test pattern gives a track is up to the detector, so the event buttons are pressed only
    // when there is an event.
    private static void PlayWithTimeline(
        DesktopApp app,
        Window main,
        string serverFolder)
    {
        ShowView(app, main, "Recordings");
        var row = app.FindWhere(main, e => e.ControlType == ControlType.DataItem, "a recording row");
        row.Patterns.SelectionItem.Pattern.Select();

        var player = app.OpenWindow(() => DesktopApp.Press(app.Find(main, "Play", ControlType.Button)), "Play");
        var timeline = app.Find(player, "Activity", timeout: TimeSpan.FromSeconds(20));
        app.ShouldCoverTheTaskbar(player);

        // A right-click anywhere offers "missed something here"; the server writes the review.
        VerdictWalk.Give(app, player, timeline, "Missed something here");
        VerdictWalk.SpansUnder(serverFolder).Should().Contain(s => s.Verdict == ReviewVerdicts.Missed);
        foreach (var name in new[] { "Next event", "Previous event" })
        {
            if (app.Find(player, name, ControlType.Button) is { IsEnabled: true } button)
            {
                DesktopApp.Press(button);
            }
        }

        foreach (var name in new[] { "Zoom in", "Zoom out" })
        {
            app.Find(player, name, ControlType.Button);
        }

        app.CloseWindow(player);
    }

    // The client asks the server for a snapshot and shows the answer: the picture, or that there
    // is none. The live test camera serves one client, which the live view's stream holds, so the
    // server usually cannot take one here; a camera that allows a second session gives one. A zone
    // added is picked either way. The dialog is closed without saving.
    private static void DrawZoneOnSnapshot(
        DesktopApp app,
        Window main)
    {
        var dialog = app.OpenWindow(() => DesktopApp.Press(RowButtons(app, main, "Edit", 0)), "Edit");
        DesktopApp.Press(app.Find(dialog, "Take Snapshot", ControlType.Button));
        app.WaitUntilGone(dialog, "No picture yet: take a snapshot to draw on the camera's picture.", ControlType.Text, TimeSpan.FromSeconds(45))
            .Should().BeTrue("the snapshot either comes or is reported missing");

        DesktopApp.Press(app.Find(dialog, "Add zone", ControlType.Button));
        app.Find(dialog, "Remove zone", ControlType.Button).IsEnabled.Should().BeTrue("the new zone is picked");
        app.CloseWindow(dialog);
    }

    private static void Walk(
        DesktopApp app,
        Window main,
        string buttonName)
        => app.WalkAndClose(app.OpenWindow(() => DesktopApp.Press(app.Find(main, buttonName, ControlType.Button)), buttonName));

    /// <summary>
    /// Gets the places of the row buttons with the name (one per row of the current list).
    /// </summary>
    private static IEnumerable<int> RowButtons(
        DesktopApp app,
        Window main,
        string name)
        => Enumerable.Range(0, Buttons(app, main, name).Count);

    private static AutomationElement RowButtons(
        DesktopApp app,
        Window main,
        string name,
        int index)
        => Buttons(app, main, name)[index];

    private static List<AutomationElement> Buttons(
        DesktopApp app,
        Window main,
        string name)
        => main.FindAllDescendants(app.Conditions.ByName(name).And(app.Conditions.ByControlType(ControlType.Button)))
            .Where(b => !b.IsOffscreen)
            .ToList();

    private static void CloseDialogs(DesktopApp app)
    {
        foreach (var window in app.Windows().Where(w => !w.Title.Contains(WpfClientRun.MainWindowTitle, StringComparison.OrdinalIgnoreCase)))
        {
            app.CloseWindow(window);
        }
    }
}