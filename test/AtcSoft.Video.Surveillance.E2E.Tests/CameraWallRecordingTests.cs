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
        var started = WaitForRecordings(app, recordings);

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
        main.Focus();
        FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_2);
        app.Find(main, "Cameras  1", ControlType.Text);
        ShouldKeepRecording(app, recordings, started, "while another layout is shown");
        FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_1);
        app.Find(main, $"Cameras  {CameraCount + 1}", ControlType.Text);
        ShouldKeepRecording(app, recordings, started, "after the first layout is back");
        ShouldAnswer(app, main, "Browse Recordings", "the main window");

        var recordedFor = DateTime.UtcNow - started.At;
        var closed = app.CloseApp(main);

        opening.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "the recordings browser opens without holding up the app");
        closed.Should().BeTrue("CameraWall closes when its main window closes");
        foreach (var file in started.Files)
        {
            DurationOf(file).Should().BeGreaterThan(
                recordedFor - TimeSpan.FromSeconds(5),
                $"{Path.GetFileName(file)} was recorded without a break for {recordedFor.TotalSeconds:N0} s");
        }

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
        string recordings)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (true)
        {
            var ongoing = Ongoing(recordings);
            if (ongoing.Count >= CameraCount)
            {
                return (ongoing, DateTime.UtcNow);
            }

            if (DateTime.UtcNow > deadline)
            {
                throw app.Fail($"{CameraCount} recordings did not start; being written: {ongoing.Count}.");
            }

            Thread.Sleep(500);
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
        Thread.Sleep(Pause);
        app.Process.HasExited.Should().BeFalse($"CameraWall is still running {when}");
        Ongoing(recordings).Should().Equal(started.Files, $"each camera's recording goes on {when}");
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