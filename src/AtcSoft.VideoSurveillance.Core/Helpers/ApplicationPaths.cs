namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Provides default application paths for data storage.
/// </summary>
/// <remarks>
/// The environment variable <see cref="DataPathEnvironmentVariable"/> moves every folder a desktop
/// app uses (ProgramData and LocalAppData) into one folder, so the E2E smoke tests and an Aspire
/// run never touch a user's real cameras, settings or recordings.
/// </remarks>
public static class ApplicationPaths
{
    /// <summary>
    /// The environment variable that moves all application data into one folder.
    /// </summary>
    public const string DataPathEnvironmentVariable = "ATCSOFT_DATA_PATH";

    // Declared first: the field initializers below read it.
    private static readonly string? DataPathOverrideValue =
        ParseDataPathOverride(Environment.GetEnvironmentVariable(DataPathEnvironmentVariable));

    private static readonly string BaseDataPath = ResolveBaseDataPath(DataPathOverrideValue, "VideoSurveillance");

    /// <summary>
    /// Gets the folder from <see cref="DataPathEnvironmentVariable"/>, or <see langword="null"/> when it is not set.
    /// </summary>
    public static string? DataPathOverride => DataPathOverrideValue;

    /// <summary>
    /// Gets the default path for log files.
    /// </summary>
    public static string DefaultLogsPath { get; } = Path.Combine(BaseDataPath, "logs");

    /// <summary>
    /// Gets the default path for snapshots.
    /// </summary>
    public static string DefaultSnapshotsPath { get; } = Path.Combine(BaseDataPath, "snapshots");

    /// <summary>
    /// Gets the default path for recordings.
    /// </summary>
    public static string DefaultRecordingsPath { get; } = Path.Combine(BaseDataPath, "recordings");

    /// <summary>
    /// Gets the default path for settings file.
    /// </summary>
    public static string DefaultSettingsPath { get; } = Path.Combine(BaseDataPath, "settings.json");

    /// <summary>
    /// Gets the default path for camera data file.
    /// </summary>
    public static string DefaultCameraDataPath { get; } = Path.Combine(BaseDataPath, "cameras.json");

    /// <summary>
    /// Gets the path of a per-user file: under <c>%LocalAppData%\AtcSoft</c>, or under
    /// <c>{override}\local</c> when <see cref="DataPathEnvironmentVariable"/> is set.
    /// </summary>
    /// <param name="relativeParts">The folder(s) and file name below the AtcSoft folder.</param>
    /// <returns>The full path.</returns>
    public static string GetLocalDataPath(params string[] relativeParts)
        => ResolveLocalDataPath(DataPathOverrideValue, relativeParts);

    /// <summary>
    /// Gets an application's data folder: under <c>%ProgramData%\AtcSoft</c>, or the override itself.
    /// </summary>
    /// <param name="dataPathOverride">The override, or <see langword="null"/>.</param>
    /// <param name="applicationName">The application folder name (e.g. "CameraWall").</param>
    /// <returns>The full path.</returns>
    public static string ResolveBaseDataPath(
        string? dataPathOverride,
        string applicationName)
        => dataPathOverride
           ?? Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
               "AtcSoft",
               applicationName);

    internal static string? ParseDataPathOverride(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : Path.GetFullPath(value.Trim());

    internal static string ResolveLocalDataPath(
        string? dataPathOverride,
        params string[] relativeParts)
    {
        var root = dataPathOverride is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtcSoft")
            : Path.Combine(dataPathOverride, "local");

        return Path.Combine([root, .. relativeParts]);
    }
}