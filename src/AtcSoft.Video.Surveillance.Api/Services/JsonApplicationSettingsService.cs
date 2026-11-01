namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// JSON file-based implementation of <see cref="IApplicationSettingsService"/> for the server edition.
/// </summary>
public sealed partial class JsonApplicationSettingsService : IApplicationSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string storagePath;
    private readonly ILogger<JsonApplicationSettingsService> logger;
    private readonly ServerDataFile file;
    private ApplicationSettings appSettings = new();

    public JsonApplicationSettingsService(
        ILogger<JsonApplicationSettingsService> logger)
        : this(ApplicationPaths.DefaultSettingsPath, logger)
    {
    }

    public JsonApplicationSettingsService(
        string storagePath,
        ILogger<JsonApplicationSettingsService> logger,
        int lockedAttempts = ServerDataFile.DefaultLockedAttempts)
    {
        this.storagePath = storagePath;
        this.logger = logger;
        file = new ServerDataFile(storagePath, logger, lockedAttempts);
        EnsureSettingsFileExists();
        Load();
    }

    /// <summary>
    /// Gets a value indicating whether the settings file existed but could not be read, so the
    /// server runs on the default settings: nothing is deleted by age or size on them.
    /// </summary>
    public bool IsUnreadable => file.Outcome == JsonFileReadOutcome.Unreadable;

    /// <inheritdoc/>
    public GeneralSettings General => appSettings.General;

    /// <inheritdoc/>
    public CameraDisplayAppSettings CameraDisplay => appSettings.CameraDisplay;

    /// <inheritdoc/>
    public ConnectionAppSettings Connection => appSettings.Connection;

    /// <inheritdoc/>
    public PerformanceSettings Performance => appSettings.Performance;

    /// <inheritdoc/>
    public MotionDetectionSettings MotionDetection
        => appSettings.MotionDetection;

    /// <inheritdoc/>
    public RecordingSettings Recording => appSettings.Recording;

    /// <inheritdoc/>
    public AdvancedSettings Advanced => appSettings.Advanced;

    /// <inheritdoc/>
    public void SaveGeneral(GeneralSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        appSettings.General = settings;
        Save();
    }

    /// <inheritdoc/>
    public void SaveCameraDisplay(CameraDisplayAppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        appSettings.CameraDisplay = settings;
        Save();
    }

    /// <inheritdoc/>
    public void SaveConnection(ConnectionAppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        appSettings.Connection = settings;
        Save();
    }

    /// <inheritdoc/>
    public void SavePerformance(PerformanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        appSettings.Performance = settings;
        Save();
    }

    /// <inheritdoc/>
    public void SaveMotionDetection(MotionDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        appSettings.MotionDetection = settings;
        Save();
    }

    /// <inheritdoc/>
    public void SaveRecording(RecordingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        appSettings.Recording = settings;
        Save();
    }

    /// <inheritdoc/>
    public void SaveAdvanced(AdvancedSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        appSettings.Advanced = settings;
        Save();
    }

    /// <inheritdoc/>
    public void ApplyDefaultsToCamera(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        camera.Connection.Protocol = Connection.DefaultProtocol;
        camera.Connection.Port = Connection.DefaultPort;
        camera.Display.OverlayPosition = CameraDisplay.OverlayPosition;
        camera.Stream.UseLowLatencyMode = Performance.LowLatencyMode;
        camera.Stream.MaxLatencyMs = Performance.MaxLatencyMs;
        camera.Stream.RtspTransport = Performance.RtspTransport;
        camera.Stream.BufferDurationMs = Performance.BufferDurationMs;
    }

    /// <inheritdoc/>
    public T GetEffectiveValue<T>(
        CameraConfiguration camera,
        T appDefault,
        Func<CameraOverrides?, T?> overrideSelector)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(overrideSelector);
        return overrideSelector(camera.Overrides) ?? appDefault;
    }

    /// <inheritdoc/>
    public string? GetEffectiveStringValue(
        CameraConfiguration camera,
        string? appDefault,
        Func<CameraOverrides?, string?> overrideSelector)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(overrideSelector);
        return overrideSelector(camera.Overrides) ?? appDefault;
    }

    /// <inheritdoc/>
    public void Load()
    {
        // A file that cannot be read is never taken for a missing one: the
        // backup is tried, the file is copied aside, and nothing is saved over
        // a file that could not be kept.
        var loaded = file.Read<ApplicationSettings>(JsonOptions);
        if (loaded is not null)
        {
            appSettings = loaded;
            LogSettingsLoaded(storagePath);
        }
        else
        {
            appSettings = new ApplicationSettings();
        }
    }

    /// <inheritdoc/>
    public void Save()
    {
        file.TryWrite(appSettings, JsonOptions);
    }

    private void EnsureSettingsFileExists()
    {
        // Only on a first start: a missing file with a backup is read from the backup.
        if (File.Exists(storagePath) || File.Exists(storagePath + ".bak"))
        {
            return;
        }

        if (SafeJsonFile.TryWrite(storagePath, new ApplicationSettings(), JsonOptions))
        {
            LogDefaultSettingsCreated(storagePath);
        }
        else
        {
            LogDefaultSettingsCreateFailed(new IOException("Atomic save failed"), storagePath);
        }
    }
}