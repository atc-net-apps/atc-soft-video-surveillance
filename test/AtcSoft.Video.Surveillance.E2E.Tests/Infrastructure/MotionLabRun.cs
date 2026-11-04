namespace AtcSoft.Video.Surveillance.E2E.Infrastructure;

/// <summary>
/// One run of Motion Lab in its own data folder (ATCSOFT_DATA_PATH), opening a
/// storybook or a recording from its command line.
/// </summary>
internal sealed class MotionLabRun : IDisposable
{
    public const string WindowTitle = "Motion Lab";

    /// <param name="arguments">What to open: <c>--storybook file</c> or <c>--recording file</c>, and <c>--rate</c>.</param>
    public MotionLabRun(IEnumerable<string> arguments)
    {
        DataFolder = E2ESettings.NewFolder("motion-lab-" + Guid.NewGuid().ToString("N")[..8]);
        LogFolder = Path.Combine(DataFolder, "logs");

        var process = ChildProcess.Start(
            E2ESettings.MotionLabExe,
            arguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ATCSOFT_DATA_PATH"] = DataFolder,
            },
            Path.Combine(DataFolder, "output.log"));
        App = new DesktopApp(process, Path.Combine(DataFolder, "ui"));
    }

    public string DataFolder { get; }

    public string LogFolder { get; }

    public DesktopApp App { get; }

    public void Dispose()
    {
        App.Dispose();
        App.Process.Dispose();
    }
}