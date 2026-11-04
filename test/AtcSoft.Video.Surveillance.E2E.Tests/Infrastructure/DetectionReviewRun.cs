namespace AtcSoft.Video.Surveillance.E2E.Infrastructure;

/// <summary>
/// One run of Detection Review in its own data folder (ATCSOFT_DATA_PATH), opening a recording
/// or a review folder from its command line.
/// </summary>
internal sealed class DetectionReviewRun : IDisposable
{
    public const string WindowTitle = "Detection Review";

    /// <param name="arguments">What to open: a recording or a review folder, and <c>--every seconds</c>.</param>
    public DetectionReviewRun(IEnumerable<string> arguments)
    {
        DataFolder = E2ESettings.NewFolder("detection-review-" + Guid.NewGuid().ToString("N")[..8]);
        LogFolder = Path.Combine(DataFolder, "logs");

        var process = ChildProcess.Start(
            E2ESettings.DetectionReviewExe,
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