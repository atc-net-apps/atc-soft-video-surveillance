namespace AtcSoft.VideoSurveillance.E2E;

/// <summary>
/// Writes each WPF app's UI Automation tree to the run folder, for writing new smoke steps.
/// Runs only with ATCSOFT_E2E_DUMP=1 as well.
/// </summary>
[Collection(E2ETestGroup.Name)]
public sealed class UiTreeDumpTests(ServerFixture server)
{
    [Fact]
    public void CameraWall_Tree_Is_Written()
    {
        SkipUnlessDumping();

        using var run = new CameraWallRun();
        var main = run.App.WaitForWindowTitled(CameraWallRun.MainWindowTitle, DesktopApp.StartTimeout);
        DesktopApp.Settle(TimeSpan.FromSeconds(3));
        TestContext.Current.SendDiagnosticMessage(run.App.DumpTree("camera-wall-main"));
        run.App.CloseApp(main);
    }

    [Fact]
    public void WpfClient_Tree_Is_Written()
    {
        SkipUnlessDumping();

        using var run = new WpfClientRun(server);
        var main = run.App.WaitForWindowTitled(WpfClientRun.MainWindowTitle, DesktopApp.StartTimeout);
        DesktopApp.Settle(TimeSpan.FromSeconds(3));
        TestContext.Current.SendDiagnosticMessage(run.App.DumpTree("wpf-client-main"));
        run.App.CloseApp(main);
    }

    private static void SkipUnlessDumping()
    {
        E2ESettings.SkipUnlessEnabled();
        Assert.SkipUnless(
            string.Equals(Environment.GetEnvironmentVariable("ATCSOFT_E2E_DUMP"), "1", StringComparison.Ordinal),
            "Writes UI trees only with ATCSOFT_E2E_DUMP=1.");
    }
}