namespace AtcSoft.VideoSurveillance.E2E;

/// <summary>
/// The FFmpeg set-up dialog both desktop apps show at start when the FFmpeg the engine needs
/// is not found. The search is limited (ATCSOFT_FFMPEG_SEARCH=explicit), so the dialog shows on
/// a machine that has FFmpeg; the install itself (a 100 MB download) is left to the unit tests.
/// </summary>
[Collection(E2ETestGroup.Name)]
public sealed class FFmpegSetupSmokeTests(ServerFixture server)
{
    private const string DialogTitle = "FFmpeg Setup";

    private static readonly Dictionary<string, string> WithoutFFmpeg = new(StringComparer.Ordinal)
    {
        [FFmpegRuntime.SearchEnvironmentVariable] = "explicit",
        [FFmpegRuntime.PathEnvironmentVariable] = string.Empty,
    };

    // A picture of the dialog in the run folder, to look at.
    private static void Picture(
        Window dialog,
        string name)
    {
        WindowPicture.Save(dialog, Path.Combine(E2ESettings.RunFolder, $"ffmpeg-setup-{name}.png"));
    }

    [Fact]
    public void CameraWall_Starts_With_A_Folder_Chosen_In_The_Set_Up_Dialog()
    {
        E2ESettings.SkipUnlessEnabled();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        Assert.SkipUnless(folder is not null && FFmpegRuntime.Check([folder]).Status == FFmpegStatus.Ready, "FFmpeg 9 is not installed.");

        using var run = new CameraWallRun(WithoutFFmpeg);
        var app = run.App;
        var dialog = app.WaitForWindowTitled(DialogTitle, DesktopApp.StartTimeout);
        Picture(dialog, run.GetType().Name);

        app.FindWhere(dialog, e => (e.Properties.Name.ValueOrDefault ?? string.Empty).Contains("not installed", StringComparison.Ordinal), "saying FFmpeg is not installed");
        var install = app.Find(dialog, "Install FFmpeg", ControlType.Button);
        app.Find(dialog, "Download page", ControlType.Button);

        // The folder reaches the dialog when the text box loses focus; until then the button is off.
        var text = app.FindWhere(dialog, e => e.ControlType == ControlType.Edit, "to type the folder in").AsTextBox();
        text.Text = folder;
        install.Focus();
        var useFolder = app.FindWhere(dialog, e => e.ControlType == ControlType.Button && e.Name == "Use this folder" && e.IsEnabled, "enabled to use the folder");
        DesktopApp.Press(useFolder);

        var main = app.WaitForWindowTitled(CameraWallRun.MainWindowTitle, DesktopApp.StartTimeout);
        var closed = app.CloseApp(main);

        LogFindings.AssertClean(run.LogFolder, "CameraWall");
        closed.Should().BeTrue("CameraWall closes when its main window closes");
    }

    [Fact]
    public void The_Client_Exits_When_The_User_Leaves_The_Set_Up_Dialog()
    {
        E2ESettings.SkipUnlessEnabled();

        using var run = new WpfClientRun(server, WithoutFFmpeg);
        var app = run.App;
        var dialog = app.WaitForWindowTitled(DialogTitle, DesktopApp.StartTimeout);
        Picture(dialog, run.GetType().Name);

        app.FindWhere(dialog, e => (e.Properties.Name.ValueOrDefault ?? string.Empty).Contains("FFmpeg", StringComparison.Ordinal), "naming FFmpeg");
        DesktopApp.Press(app.Find(dialog, "Exit", ControlType.Button));

        app.Process.Process.WaitForExit(20_000).Should().BeTrue("the client exits when FFmpeg is not set up");
        LogFindings.AssertClean(run.LogFolder, "the WPF client");
    }
}