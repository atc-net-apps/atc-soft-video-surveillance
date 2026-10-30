namespace AtcSoft.VideoSurveillance.Api.Services;

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
    private ApplicationSettings appSettings = new();

    public JsonApplicationSettingsService(
        ILogger<JsonApplicationSettingsService> logger)
        : this(ApplicationPaths.DefaultSettingsPath, logger)
    {
    }

    public JsonApplicationSettingsService(
        string storagePath,
        ILogger<JsonApplicationSettingsService> logger)
    {
        this.storagePath = storagePath;
        this.logger = logger;
        EnsureSettingsFileExists();
        Load();
    }

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
        var loaded = SafeJsonFile.TryRead<ApplicationSettings>(storagePath, JsonOptions);
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
        if (!SafeJsonFile.TryWrite(storagePath, appSettings, JsonOptions))
        {
            LogSettingsSaveFailed(new IOException("Atomic save failed"), storagePath);
        }
    }

    private void EnsureSettingsFileExists()
    {
        if (File.Exists(storagePath))
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