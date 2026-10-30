namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Where the server keeps its own files. By default that is the shared
/// application data folder; the <c>Storage:DataPath</c> setting moves all of
/// it to another folder, so a second server (or a test) has data of its own.
/// </summary>
public sealed class ServerDataPaths
{
    /// <summary>
    /// The configuration key of the data folder. As an environment variable: <c>Storage__DataPath</c>.
    /// </summary>
    public const string ConfigurationKey = "Storage:DataPath";

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerDataPaths"/> class.
    /// </summary>
    /// <param name="dataPath">The data folder, or <c>null</c> / empty for the default location.</param>
    public ServerDataPaths(string? dataPath)
    {
        DataPath = string.IsNullOrWhiteSpace(dataPath)
            ? null
            : Path.GetFullPath(dataPath);

        SettingsPath = DataPath is null
            ? ApplicationPaths.DefaultSettingsPath
            : Path.Combine(DataPath, Path.GetFileName(ApplicationPaths.DefaultSettingsPath));

        CameraDataPath = DataPath is null
            ? ApplicationPaths.DefaultCameraDataPath
            : Path.Combine(DataPath, Path.GetFileName(ApplicationPaths.DefaultCameraDataPath));

        var folder = Path.GetDirectoryName(SettingsPath)!;
        WebhooksPath = Path.Combine(folder, "webhooks.json");
        AccountsPath = Path.Combine(folder, "accounts.json");
        SetupCodePath = Path.Combine(folder, "setup-code.txt");
        SigningKeyPath = Path.Combine(folder, "signing.key");
        AuditLogPath = Path.Combine(folder, "audit.log");
        AvailabilityPath = Path.Combine(folder, "availability");
        KeysPath = Path.Combine(folder, "keys");
    }

    /// <summary>
    /// Gets the configured data folder, or <c>null</c> when the default location is used.
    /// </summary>
    public string? DataPath { get; }

    /// <summary>
    /// Gets the path of the settings file.
    /// </summary>
    public string SettingsPath { get; }

    /// <summary>
    /// Gets the path of the cameras and layouts file.
    /// </summary>
    public string CameraDataPath { get; }

    /// <summary>
    /// Gets the path of the file with the webhook receivers added through
    /// the API. It lies next to the settings file.
    /// </summary>
    public string WebhooksPath { get; }

    /// <summary>
    /// Gets the path of the file with the users, their API keys and their
    /// sessions. It lies next to the settings file.
    /// </summary>
    public string AccountsPath { get; }

    /// <summary>
    /// Gets the path of the file with the one-time code that creates the
    /// first admin, while the server has no user.
    /// </summary>
    public string SetupCodePath { get; }

    /// <summary>
    /// Gets the path of the key the server signs its access tokens with.
    /// </summary>
    public string SigningKeyPath { get; }

    /// <summary>
    /// Gets the path of the audit log: what was changed, by whom.
    /// </summary>
    public string AuditLogPath { get; }

    /// <summary>
    /// Gets the folder with the history of whether each camera could be recorded (one file per
    /// camera, one for the server's starts and stops), which explains the gaps of the timeline.
    /// It lies next to the settings file.
    /// </summary>
    public string AvailabilityPath { get; }

    /// <summary>
    /// Gets the folder with the keys the stored secrets (camera passwords,
    /// webhook secrets) are encrypted with. Without it they cannot be read.
    /// </summary>
    public string KeysPath { get; }

    /// <summary>
    /// Moves the recording, snapshot and log folders into the data folder
    /// when they still point at the default location, and saves the change.
    /// A folder the user picked is left alone. Does nothing without a
    /// configured data folder.
    /// </summary>
    /// <param name="settingsService">The settings to bring in line.</param>
    public void RelocateDefaults(IApplicationSettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);

        if (DataPath is null)
        {
            return;
        }

        var recording = settingsService.Recording;
        if (IsSamePath(recording.RecordingPath, ApplicationPaths.DefaultRecordingsPath))
        {
            recording.RecordingPath = Path.Combine(DataPath, Path.GetFileName(ApplicationPaths.DefaultRecordingsPath));
            settingsService.SaveRecording(recording);
        }

        var cameraDisplay = settingsService.CameraDisplay;
        if (IsSamePath(cameraDisplay.SnapshotPath, ApplicationPaths.DefaultSnapshotsPath))
        {
            cameraDisplay.SnapshotPath = Path.Combine(DataPath, Path.GetFileName(ApplicationPaths.DefaultSnapshotsPath));
            settingsService.SaveCameraDisplay(cameraDisplay);
        }

        var advanced = settingsService.Advanced;
        if (IsSamePath(advanced.LogPath, ApplicationPaths.DefaultLogsPath))
        {
            advanced.LogPath = Path.Combine(DataPath, Path.GetFileName(ApplicationPaths.DefaultLogsPath));
            settingsService.SaveAdvanced(advanced);
        }
    }

    private static bool IsSamePath(
        string? path,
        string defaultPath)
        => !string.IsNullOrWhiteSpace(path) &&
           string.Equals(
               Path.TrimEndingDirectorySeparator(path),
               Path.TrimEndingDirectorySeparator(defaultPath),
               StringComparison.OrdinalIgnoreCase);
}