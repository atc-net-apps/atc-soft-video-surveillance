namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// Where the apps under test are, whether the smoke tests run at all, and the run's folders.
/// </summary>
internal static class E2ESettings
{
    /// <summary>
    /// The smoke tests start real windows, so they run only when this is "1".
    /// </summary>
    public const string EnableVariable = "ATCSOFT_E2E";

    private const string AppTargetFramework = "net10.0-windows10.0.19041.0";
    private const string ServerTargetFramework = "net10.0-windows";

    public static bool IsEnabled
        => string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal);

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    /// Gets the build configuration of this test run (Debug or Release), so the apps
    /// started are the ones built with it.
    /// </summary>
    public static string Configuration { get; } = FindConfiguration();

    /// <summary>
    /// Gets the folder of this run: every app's data, logs and the test output go below it,
    /// and it stays after the run for a look at what happened.
    /// </summary>
    public static string RunFolder { get; } = Path.Combine(
        Path.GetTempPath(),
        "AtcSoft.E2E",
        DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

    public static string CameraWallExe
        => AppPath("AtcSoft.CameraWall.Wpf.App", AppTargetFramework);

    public static string WpfClientExe
        => AppPath("AtcSoft.VideoSurveillance.Wpf.App", AppTargetFramework);

    public static string ServerExe
        => AppPath("AtcSoft.VideoSurveillance.Api", ServerTargetFramework);

    /// <summary>
    /// Gets the bounding box sample, a developer tool under <c>sample/</c>.
    /// </summary>
    public static string BoundingBoxSampleExe
    {
        get
        {
            var path = Path.Combine(RepositoryRoot, "sample", "BoundingBoxWpfApp", "bin", Configuration, AppTargetFramework, "BoundingBoxWpfApp.exe");
            return File.Exists(path)
                ? path
                : throw new FileNotFoundException($"Build BoundingBoxWpfApp ({Configuration}) first; the smoke tests start its executable.", path);
        }
    }

    public static string BlazorProjectFolder
        => Path.Combine(RepositoryRoot, "src", "AtcSoft.VideoSurveillance.Blazor.App");

    public static void SkipUnlessEnabled()
        => Assert.SkipUnless(
            IsEnabled,
            $"The end-to-end smoke tests start real windows; set {EnableVariable}=1 to run them.");

    public static string NewFolder(string name)
    {
        var folder = Path.Combine(RunFolder, name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string AppPath(
        string project,
        string targetFramework)
    {
        var path = Path.Combine(RepositoryRoot, "src", project, "bin", Configuration, targetFramework, project + ".exe");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Build {project} ({Configuration}) first; the smoke tests start its executable.", path);
        }

        return path;
    }

    private static string FindRepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "AtcSoft.VideoSurveillance.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
               ?? throw new InvalidOperationException("The repository root (AtcSoft.VideoSurveillance.slnx) was not found above " + AppContext.BaseDirectory);
    }

    private static string FindConfiguration()
    {
        // ...\bin\{Configuration}\{TargetFramework}\
        var targetFrameworkFolder = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        return targetFrameworkFolder.Parent?.Name ?? "Debug";
    }
}