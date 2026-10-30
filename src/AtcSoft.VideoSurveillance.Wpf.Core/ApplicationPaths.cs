namespace AtcSoft.VideoSurveillance.Wpf.Core;

/// <summary>
/// Provides default application paths for data storage.
/// The base folder can be configured per application (e.g. "CameraWall" or "VideoSurveillance").
/// </summary>
public static class ApplicationPaths
{
    // ATCSOFT_DATA_PATH (the E2E smoke tests, a scratch run) replaces the whole folder.
    private static string baseDataPath = AtcSoft.VideoSurveillance.Helpers.ApplicationPaths.ResolveBaseDataPath(
        AtcSoft.VideoSurveillance.Helpers.ApplicationPaths.DataPathOverride,
        "CameraWall");

    /// <summary>
    /// Gets the default path for log files.
    /// </summary>
    public static string DefaultLogsPath
        => Path.Combine(baseDataPath, "logs");

    /// <summary>
    /// Gets the default path for snapshots.
    /// </summary>
    public static string DefaultSnapshotsPath
        => Path.Combine(baseDataPath, "snapshots");

    /// <summary>
    /// Gets the default path for recordings.
    /// </summary>
    public static string DefaultRecordingsPath
        => Path.Combine(baseDataPath, "recordings");

    /// <summary>
    /// Gets the default path for settings file.
    /// </summary>
    public static string DefaultSettingsPath
        => Path.Combine(baseDataPath, "settings.json");

    /// <summary>
    /// Gets the default path for camera data file.
    /// </summary>
    public static string DefaultCameraDataPath
        => Path.Combine(baseDataPath, "cameras.json");

    /// <summary>
    /// Configures the application folder name under AtcSoft in ProgramData.
    /// Must be called before any path properties are accessed. With
    /// ATCSOFT_DATA_PATH set the folder stays the override.
    /// </summary>
    /// <param name="applicationName">The application folder name (e.g. "CameraWall" or "VideoSurveillance").</param>
    public static void Configure(string applicationName)
    {
        baseDataPath = AtcSoft.VideoSurveillance.Helpers.ApplicationPaths.ResolveBaseDataPath(
            AtcSoft.VideoSurveillance.Helpers.ApplicationPaths.DataPathOverride,
            applicationName);
    }
}