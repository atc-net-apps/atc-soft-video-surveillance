namespace AtcSoft.Video.Surveillance.E2E;

/// <summary>
/// CameraWall records three cameras while a person looks around: the recordings browser, and another
/// layout with one of the cameras and back. Full screen is not here: its button is in the tile's overlay
/// window, which UI Automation does not reach (see https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/E2E-Smoke-Tests). Each camera's recording goes on the whole time, in one
/// file, and the app keeps answering.
/// </summary>
[Collection(E2ETestGroup.Name)]
public sealed class CameraWallRecordingTests
{
    private const int CameraCount = 3;

    // A pause for the app to get on with it; a recorder that stopped has closed its file by then.
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(4);

    [Fact]
    public void Recording_Goes_On_While_Recordings_Are_Browsed_And_Layouts_Are_Switched()
    {
        E2ESettings.SkipUnlessEnabled();
        if (TestCameras.FFmpegTool is null)
        {
            Assert.Skip("The FFmpeg command-line tool is needed for the live test cameras.");
        }

        using var run = new CameraWallRun(liveCameraCount: CameraCount, recordOnConnect: true, secondLayout: true);
        var app = run.App;
        var main = app.WaitForWindowTitled(CameraWallRun.MainWindowTitle, DesktopApp.StartTimeout);
        var recordings = Path.Combine(run.DataFolder, "recordings");

        // The status bar counts the connected cameras.
        app.Find(main, $"Connected  {CameraCount}", ControlType.Text, TimeSpan.FromSeconds(30));
        var started = WaitForRecordings(app, recordings, CameraCount);

        // The recordings browser, open for a few of its refreshes.
        DesktopApp.Press(app.Find(main, "View", ControlType.TabItem));
        var opening = Stopwatch.StartNew();
        var browser = app.OpenWindow(() => DesktopApp.Press(app.Find(main, "Browse Recordings", ControlType.Button)), "Browse Recordings");
        app.FindWhere(browser, e => e.ControlType == ControlType.DataItem, "a recording row");
        opening.Stop();
        ShouldKeepRecording(app, recordings, started, "while the recordings browser is open");
        ShouldAnswer(app, browser, "Refresh", "the recordings browser");
        app.CloseWindow(browser);
        ShouldKeepRecording(app, recordings, started, "after the recordings browser closed");

        // Ctrl+2 shows the second layout, with one of the cameras; Ctrl+1 the first again.
        ShowLayout(app, main, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_2, "Cameras  1");
        ShouldKeepRecording(app, recordings, started, "while another layout is shown");
        ShowLayout(app, main, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_1, $"Cameras  {CameraCount + 1}");
        ShouldKeepRecording(app, recordings, started, "after the first layout is back");
        ShouldAnswer(app, main, "Browse Recordings", "the main window");

        var recordedFor = DateTime.UtcNow - started.At;
        var closed = app.CloseApp(main);

        opening.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "the recordings browser opens without holding up the app");
        app.CloseWhileRecordingAsked.Should().BeTrue("closing stops the recording, so CameraWall asks first");
        closed.Should().BeTrue("CameraWall closes when its main window closes");
        foreach (var file in started.Files)
        {
            DurationOf(file).Should().BeGreaterThan(
                recordedFor - TimeSpan.FromSeconds(5),
                $"{Path.GetFileName(file)} was recorded without a break for {recordedFor.TotalSeconds:N0} s");
        }

        LogFindings.AssertClean(run.LogFolder, "CameraWall");
    }

    [Fact]
    public void A_Recording_Goes_On_In_A_New_File_When_Its_Camera_Comes_Back_After_Dropping_Out()
    {
        E2ESettings.SkipUnlessEnabled();
        if (TestCameras.FFmpegTool is null)
        {
            Assert.Skip("The FFmpeg command-line tool is needed for the live test cameras.");
        }

        using var run = new CameraWallRun(liveCameraCount: 1, recordOnConnect: true);
        var app = run.App;
        var main = app.WaitForWindowTitled(CameraWallRun.MainWindowTitle, DesktopApp.StartTimeout);
        var recordings = Path.Combine(run.DataFolder, "recordings");
        app.Find(main, "Connected  1", ControlType.Text, TimeSpan.FromSeconds(30));
        var first = WaitForRecordings(app, recordings, 1).Files.Single();
        RealTime.Wait(Pause);

        // The camera reboots: gone for 20 s, then back.
        run.LiveCameras[0].DropOut(TimeSpan.FromSeconds(20));
        var droppedAt = Stopwatch.StartNew();

        // The file it was writing ends, and once the camera is back a new one is written: well
        // before the next hourly segment.
        var deadline = TimeSpan.FromSeconds(20) + TimeSpan.FromSeconds(90);
        List<string> ongoing = [];
        while (droppedAt.Elapsed < deadline)
        {
            ongoing = Ongoing(recordings);
            if (ongoing.Count == 1 && !string.Equals(ongoing[0], first, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            app.Process.HasExited.Should().BeFalse("CameraWall is still running");
            RealTime.Wait(1000);
        }

        ongoing.Should().ContainSingle().Which.Should().NotBe(first, "the recording goes on in a new file after the camera came back");
        RecordingPathHelper.IsBeingWritten(first).Should().BeFalse("the file of before the drop is finished");
        var newFile = ongoing[0];
        RealTime.Wait(Pause);
        Ongoing(recordings).Should().Equal([newFile], "the new file keeps being written");

        var closed = app.CloseApp(main);
        closed.Should().BeTrue("CameraWall closes when its main window closes");
        DurationOf(first).Should().BeGreaterThan(TimeSpan.Zero, "the file of before the drop is playable");
        DurationOf(newFile).Should().BeGreaterThan(TimeSpan.Zero, "the file of after the drop is playable");
        LogFindings.AssertClean(run.LogFolder, "CameraWall");
    }

    [Fact]
    public void A_Recording_Camera_That_No_Layout_Shows_Is_Opened_Again_After_Dropping_Out()
    {
        E2ESettings.SkipUnlessEnabled();
        if (TestCameras.FFmpegTool is null)
        {
            Assert.Skip("The FFmpeg command-line tool is needed for the live test cameras.");
        }

        using var run = new CameraWallRun(liveCameraCount: 2, recordOnConnect: true, secondLayout: true);
        var app = run.App;
        var main = app.WaitForWindowTitled(CameraWallRun.MainWindowTitle, DesktopApp.StartTimeout);
        var recordings = Path.Combine(run.DataFolder, "recordings");
        app.Find(main, "Connected  2", ControlType.Text, TimeSpan.FromSeconds(30));
        var before = WaitForRecordings(app, recordings, 2).Files;

        // The second layout shows only the first camera: the second records on without a tile.
        ShowLayout(app, main, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_2, "Cameras  1");
        ShouldKeepRecording(app, recordings, (before, DateTime.UtcNow), "while the second camera has no tile");

        // The camera without a tile reboots: gone for 20 s, then back.
        run.LiveCameras[1].DropOut(TimeSpan.FromSeconds(20));
        var droppedAt = Stopwatch.StartNew();

        // Its recording goes on in a new file once it is back, without a tile showing it; the first
        // camera's file goes on as it was.
        var deadline = TimeSpan.FromSeconds(20) + TimeSpan.FromSeconds(120);
        List<string> ongoing = [];
        while (droppedAt.Elapsed < deadline)
        {
            ongoing = Ongoing(recordings);
            if (ongoing.Count == 2 && ongoing.Except(before, StringComparer.OrdinalIgnoreCase).Count() == 1)
            {
                break;
            }

            app.Process.HasExited.Should().BeFalse("CameraWall is still running");
            RealTime.Wait(1000);
        }

        ongoing.Should().HaveCount(2, "both cameras record");
        var newFile = ongoing.Except(before, StringComparer.OrdinalIgnoreCase).Should().ContainSingle(
            "the camera without a tile was opened again and its recording went on in a new file").Subject;
        var stillThere = ongoing.Intersect(before, StringComparer.OrdinalIgnoreCase).Should().ContainSingle(
            "the shown camera's file goes on").Subject;

        // Back to the first layout: the camera's tile takes the player back, still recording.
        ShowLayout(app, main, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_1, "Cameras  3");
        app.Find(main, "Connected  2", ControlType.Text, TimeSpan.FromSeconds(30));
        ShouldKeepRecording(app, recordings, ([.. new[] { stillThere, newFile }.Order(StringComparer.OrdinalIgnoreCase)], DateTime.UtcNow), "after the first layout is back");

        var closed = app.CloseApp(main);
        closed.Should().BeTrue("CameraWall closes when its main window closes");
        DurationOf(newFile).Should().BeGreaterThan(TimeSpan.Zero, "the file of after the drop is playable");
        LogFindings.AssertClean(run.LogFolder, "CameraWall");
    }

    [Fact]
    public void A_Camera_That_Records_On_Motion_Goes_On_Recording_Motion_While_No_Layout_Shows_It()
    {
        E2ESettings.SkipUnlessEnabled();
        if (TestCameras.FFmpegTool is null)
        {
            Assert.Skip("The FFmpeg command-line tool is needed for the live test cameras.");
        }

        // The test pattern moves all the time: both cameras record on motion once they are watched.
        using var run = new CameraWallRun(liveCameraCount: 2, recordOnMotion: true, secondLayout: true);
        var app = run.App;
        var main = app.WaitForWindowTitled(CameraWallRun.MainWindowTitle, DesktopApp.StartTimeout);
        var recordings = Path.Combine(run.DataFolder, "recordings");
        app.Find(main, "Connected  2", ControlType.Text, TimeSpan.FromSeconds(30));
        WaitForRecordings(app, recordings, 2);

        // The second layout shows only the first camera. Before, the second camera's watching
        // stopped with its tile, and its motion recording ended after the post-motion time (10 s).
        ShowLayout(app, main, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_2, "Cameras  1");
        RealTime.Wait(TimeSpan.FromSeconds(25));

        app.Process.HasExited.Should().BeFalse("CameraWall is still running");
        Ongoing(recordings).Should().HaveCount(2, "the camera no layout shows goes on recording its motion");
        Directory.EnumerateFiles(recordings, "*.*", SearchOption.AllDirectories)
            .Where(f => f.Contains(CameraWallRun.LiveCameraName(1), StringComparison.OrdinalIgnoreCase) &&
                        (f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase)))
            .Where(RecordingPathHelper.IsBeingWritten)
            .Should().ContainSingle("the second camera's recording is the one going on");

        var closed = app.CloseApp(main);
        closed.Should().BeTrue("CameraWall closes when its main window closes");
        LogFindings.AssertClean(run.LogFolder, "CameraWall");
    }

    [Fact]
    public void A_Camera_That_Records_On_Motion_And_Never_Had_A_Tile_Records_Its_Motion()
    {
        E2ESettings.SkipUnlessEnabled();
        if (TestCameras.FFmpegTool is null)
        {
            Assert.Skip("The FFmpeg command-line tool is needed for the live test cameras.");
        }

        // The app starts with the layout that shows only the first camera. Before, the second camera
        // was never connected (cameras connected only through a tile), and its motion went unrecorded.
        using var run = new CameraWallRun(liveCameraCount: 2, recordOnMotion: true, startOnSecondLayout: true);
        var app = run.App;
        var main = app.WaitForWindowTitled(CameraWallRun.MainWindowTitle, DesktopApp.StartTimeout);
        var recordings = Path.Combine(run.DataFolder, "recordings");
        app.Find(main, "Cameras  1", ControlType.Text, TimeSpan.FromSeconds(30));
        WaitForRecordings(app, recordings, 2);

        Directory.EnumerateFiles(recordings, "*.*", SearchOption.AllDirectories)
            .Where(f => f.Contains(CameraWallRun.LiveCameraName(1), StringComparison.OrdinalIgnoreCase) &&
                        (f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase)))
            .Where(RecordingPathHelper.IsBeingWritten)
            .Should().ContainSingle("the camera no layout shows records its motion");

        var closed = app.CloseApp(main);
        closed.Should().BeTrue("CameraWall closes when its main window closes");
        LogFindings.AssertClean(run.LogFolder, "CameraWall");
    }

    [Fact]
    public void A_Camera_That_Hangs_Is_Noticed_In_Seconds_And_Its_Recording_Goes_On()
    {
        E2ESettings.SkipUnlessEnabled();
        if (TestCameras.FFmpegTool is null)
        {
            Assert.Skip("The FFmpeg command-line tool is needed for the live test cameras.");
        }

        using var run = new CameraWallRun(liveCameraCount: 1, recordOnConnect: true);
        var app = run.App;
        var main = app.WaitForWindowTitled(CameraWallRun.MainWindowTitle, DesktopApp.StartTimeout);
        var recordings = Path.Combine(run.DataFolder, "recordings");
        app.Find(main, "Connected  1", ControlType.Text, TimeSpan.FromSeconds(30));
        var first = WaitForRecordings(app, recordings, 1).Files.Single();
        RealTime.Wait(Pause);

        // The camera hangs for 40 s: the connection stays open and nothing comes.
        run.LiveCameras[0].Hang(TimeSpan.FromSeconds(40));
        var hungAt = Stopwatch.StartNew();

        // Noticed within seconds (not ~5 minutes of read time-outs), reconnected once the
        // camera answers again, and the recording goes on in a new file.
        var deadline = TimeSpan.FromSeconds(40) + TimeSpan.FromSeconds(90);
        List<string> ongoing = [];
        while (hungAt.Elapsed < deadline)
        {
            ongoing = Ongoing(recordings);
            if (ongoing.Count == 1 && !string.Equals(ongoing[0], first, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            app.Process.HasExited.Should().BeFalse("CameraWall is still running");
            RealTime.Wait(1000);
        }

        ongoing.Should().ContainSingle().Which.Should().NotBe(first, "the recording goes on in a new file after the camera answers again");

        var closed = app.CloseApp(main);
        closed.Should().BeTrue("CameraWall closes when its main window closes");
        DurationOf(first).Should().BeGreaterThan(TimeSpan.Zero, "the file of before the hang is playable");
        LogFindings.AssertClean(run.LogFolder, "CameraWall");
    }

    // The recordings being written: those whose file a recorder has open.
    private static List<string> Ongoing(string recordings)
        => Directory.Exists(recordings)
            ? [.. Directory
                .EnumerateFiles(recordings, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                .Where(RecordingPathHelper.IsBeingWritten)
                .Order(StringComparer.OrdinalIgnoreCase),]
            : [];

    private static (List<string> Files, DateTime At) WaitForRecordings(
        DesktopApp app,
        string recordings,
        int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (true)
        {
            var ongoing = Ongoing(recordings);
            if (ongoing.Count >= count)
            {
                return (ongoing, DateTime.UtcNow);
            }

            if (DateTime.UtcNow > deadline)
            {
                throw app.Fail($"{count} recordings did not start; being written: {ongoing.Count}.");
            }

            RealTime.Wait(500);
        }
    }

    // The app is running and every recording that started is still the one being written:
    // none stopped, and none was stopped and started again in a new file.
    private static void ShouldKeepRecording(
        DesktopApp app,
        string recordings,
        (List<string> Files, DateTime At) started,
        string when)
    {
        RealTime.Wait(Pause);
        app.Process.HasExited.Should().BeFalse($"CameraWall is still running {when}");
        Ongoing(recordings).Should().Equal(started.Files, $"each camera's recording goes on {when}");
    }

    // Ctrl+<n> shows the n-th layout. A key press is lost when another window has the focus at that
    // moment (a toast, the desktop), so it is pressed again until the status bar shows the layout.
    private static void ShowLayout(
        DesktopApp app,
        Window main,
        FlaUI.Core.WindowsAPI.VirtualKeyShort key,
        string statusText)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            main.Focus();
            FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, key);
            try
            {
                app.Find(main, statusText, ControlType.Text, TimeSpan.FromSeconds(5));
                return;
            }
            catch (InvalidOperationException) when (attempt < 3)
            {
                // Pressed again.
            }
        }
    }

    // A frozen app does not answer UI Automation: finding a button times out.
    private static void ShouldAnswer(
        DesktopApp app,
        AutomationElement root,
        string buttonName,
        string what)
    {
        var watch = Stopwatch.StartNew();
        app.Find(root, buttonName, ControlType.Button);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3), $"{what} answers at once");
    }

    private static TimeSpan DurationOf(string file)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(TestCameras.FFmpegTool)!, "ffprobe.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", file })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);
        return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;
    }
}