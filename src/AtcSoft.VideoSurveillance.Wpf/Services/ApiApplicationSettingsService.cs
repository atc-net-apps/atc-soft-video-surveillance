// ReSharper disable RedundantArgumentDefaultValue
using CameraOverrides = AtcSoft.VideoSurveillance.Models.CameraOverrides;
using CameraProtocol = AtcSoft.VideoSurveillance.Enums.CameraProtocol;
using MediaCleanupSchedule = AtcSoft.VideoSurveillance.Enums.MediaCleanupSchedule;
using OverlayPosition = AtcSoft.VideoSurveillance.Enums.OverlayPosition;

namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// API-backed implementation of <see cref="IApplicationSettingsService"/>
/// for the WPF surveillance client. The bulk of settings — camera-display
/// defaults, connection / performance / recording / motion-detection /
/// advanced — live on the API server and are fetched / mutated via
/// <see cref="GatewayService"/>. A small set of per-user-machine
/// preferences (theme, language, window state) is the exception: those
/// genuinely belong to *this* Windows account on *this* PC and would be
/// wrong to share across every client connected to the same server, so
/// they're persisted to a tiny local file under
/// <c>%LocalAppData%\AtcSoft\VideoSurveillance.Client\client-prefs.json</c>.
/// <para>
/// The service holds an in-memory <see cref="ApplicationSettings"/> cache
/// so the synchronous <c>General</c> / <c>CameraDisplay</c> / etc. property
/// readers work without an HTTP round-trip on every access. <see cref="LoadAsync"/>
/// must be called once at app startup before any consumer reads the cache.
/// Save calls update the cache, persist client prefs locally, and push the
/// full settings tree to the API in the background; an API failure logs a
/// warning but doesn't lose the in-session change.
/// </para>
/// </summary>
[SuppressMessage(
    "Reliability",
    "CA1031:Do not catch general exception types",
    Justification = "Settings round-tripping must never tear down the app — log and continue.")]
public sealed partial class ApiApplicationSettingsService : IApplicationSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly GatewayService gatewayService;
    private readonly ILogger<ApiApplicationSettingsService> logger;
    private readonly string clientPrefsPath;

    // A server that predates the disk-space-guard properties sends nothing for them.
    private static readonly MediaCleanupSettings DefaultCleanup = new();

    private ApplicationSettings cache = new();

    public ApiApplicationSettingsService(
        GatewayService gatewayService,
        ILogger<ApiApplicationSettingsService> logger)
    {
        this.gatewayService = gatewayService ?? throw new ArgumentNullException(nameof(gatewayService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        clientPrefsPath = AtcSoft.VideoSurveillance.Helpers.ApplicationPaths.GetLocalDataPath("VideoSurveillance.Client", "client-prefs.json");
    }

    /// <inheritdoc/>
    public GeneralSettings General => cache.General;

    /// <inheritdoc/>
    public CameraDisplayAppSettings CameraDisplay => cache.CameraDisplay;

    /// <inheritdoc/>
    public ConnectionAppSettings Connection => cache.Connection;

    /// <inheritdoc/>
    public PerformanceSettings Performance => cache.Performance;

    /// <inheritdoc/>
    public MotionDetectionSettings MotionDetection => cache.MotionDetection;

    /// <inheritdoc/>
    public RecordingSettings Recording => cache.Recording;

    /// <inheritdoc/>
    public AdvancedSettings Advanced => cache.Advanced;

    /// <inheritdoc/>
    public void Load() => LoadAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task LoadAsync()
    {
        var clientPrefs = LoadClientPrefs();
        AppSettings? api = null;

        try
        {
            api = await gatewayService.GetSettingsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogLoadFromApiFailed(ex);
        }

        cache = BuildCacheFrom(clientPrefs, api);
    }

    /// <inheritdoc/>
    public void SaveGeneral(GeneralSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cache.General = settings;

        SaveClientPrefs(ClientPreferences.From(settings));

        PushToApi();
    }

    /// <inheritdoc/>
    public void SaveCameraDisplay(CameraDisplayAppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cache.CameraDisplay = settings;
        PushToApi();
    }

    /// <inheritdoc/>
    public void SaveConnection(ConnectionAppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cache.Connection = settings;
        PushToApi();
    }

    /// <inheritdoc/>
    public void SavePerformance(PerformanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cache.Performance = settings;
        PushToApi();
    }

    /// <inheritdoc/>
    public void SaveMotionDetection(MotionDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cache.MotionDetection = settings;
        PushToApi();
    }

    /// <inheritdoc/>
    public void SaveRecording(RecordingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cache.Recording = settings;
        PushToApi();
    }

    /// <inheritdoc/>
    public void SaveAdvanced(AdvancedSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cache.Advanced = settings;
        PushToApi();
    }

    /// <inheritdoc/>
    public void Save()
    {
        SaveClientPrefs(ClientPreferences.From(cache.General));

        PushToApi();
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

        var overrideValue = overrideSelector(camera.Overrides);
        return overrideValue ?? appDefault;
    }

    /// <inheritdoc/>
    public string? GetEffectiveStringValue(
        CameraConfiguration camera,
        string? appDefault,
        Func<CameraOverrides?, string?> overrideSelector)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(overrideSelector);

        var overrideValue = overrideSelector(camera.Overrides);
        return overrideValue ?? appDefault;
    }

    private ClientPreferences LoadClientPrefs()
    {
        if (!File.Exists(clientPrefsPath))
        {
            return new ClientPreferences();
        }

        try
        {
            var json = File.ReadAllText(clientPrefsPath);
            return JsonSerializer.Deserialize<ClientPreferences>(json, JsonOptions)
                   ?? new ClientPreferences();
        }
        catch (Exception ex)
        {
            LogClientPrefsReadFailed(ex, clientPrefsPath);
            return new ClientPreferences();
        }
    }

    private void SaveClientPrefs(ClientPreferences prefs)
    {
        try
        {
            var dir = Path.GetDirectoryName(clientPrefsPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(prefs, JsonOptions);
            File.WriteAllText(clientPrefsPath, json);
        }
        catch (Exception ex)
        {
            LogClientPrefsWriteFailed(ex, clientPrefsPath);
        }
    }

    private void PushToApi()
    {
        // Fire-and-forget: cache and local prefs are already updated, so
        // the in-session UX is consistent. A failed push leaves the API
        // out of sync until the next successful save — surface it via the
        // logger rather than blocking the UI thread on HTTP. Snapshot the
        // cache so the captured value is the one that was just saved.
        var snapshot = CloneCache();
        _ = Task.Run(async () =>
        {
            try
            {
                var apiSettings = BuildApiSettings(snapshot);
                await gatewayService.UpdateSettingsAsync(apiSettings).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogPushToApiFailed(ex);
            }
        });
    }

    private ApplicationSettings CloneCache()
        => new()
        {
            General = cache.General,
            CameraDisplay = cache.CameraDisplay,
            Connection = cache.Connection,
            Performance = cache.Performance,
            MotionDetection = cache.MotionDetection,
            Recording = cache.Recording,
            Advanced = cache.Advanced,
        };

    /// <summary>
    /// Builds the in-memory <see cref="ApplicationSettings"/> cache by
    /// merging the local client preferences (theme / language / window
    /// state) with the server-side <see cref="AppSettings"/>. When the API
    /// is unreachable, falls back to constructed defaults so the app can
    /// still start.
    /// </summary>
    internal static ApplicationSettings BuildCacheFrom(
        ClientPreferences clientPrefs,
        AppSettings? api)
    {
        var result = new ApplicationSettings
        {
            General =
            {
                ThemeBase = clientPrefs.ThemeBase,
                ThemeAccent = clientPrefs.ThemeAccent,
                Language = clientPrefs.Language,
                StartMaximized = clientPrefs.StartMaximized,
                StartRibbonCollapsed = clientPrefs.StartRibbonCollapsed,
                GridSize = clientPrefs.GridSize,
                ConnectCamerasOnStartup = api?.ConnectOnStartup ?? true,
            },
        };

        if (api is null)
        {
            return result;
        }

        result.CameraDisplay = new CameraDisplayAppSettings
        {
            ShowOverlayTitle = api.ShowOverlayTitle,
            ShowOverlayDescription = api.ShowOverlayDescription,
            ShowOverlayTime = api.ShowOverlayTime,
            ShowOverlayConnectionStatus = api.ShowOverlayConnectionStatus,
            ShowOverlayQuickActions = api.ShowOverlayQuickActions,
            ShowOverlayConnectionTypeIcon = api.ShowOverlayConnectionTypeIcon,
            ConnectionTypeIconPlacement = ParseEnumOrDefault(api.ConnectionTypeIconPlacement.ToString(), AtcSoft.VideoSurveillance.Enums.ConnectionTypeIconPlacement.SameRow),
            OverlayOpacity = api.OverlayOpacity,
            OverlayPosition = ParseEnumOrDefault(api.OverlayPosition.ToString(), OverlayPosition.TopLeft),
            AllowDragAndDropReorder = api.AllowDragAndDropReorder,
            AutoSaveLayoutChanges = api.AutoSaveLayoutChanges,
            SnapshotPath = api.SnapshotPath ?? string.Empty,
        };

        result.Connection = new ConnectionAppSettings
        {
            DefaultProtocol = ParseEnumOrDefault(api.DefaultProtocol.ToString(), CameraProtocol.Rtsp),
            DefaultPort = api.DefaultPort,
            ConnectionTimeoutSeconds = api.ConnectionTimeoutSeconds,
            ReconnectDelaySeconds = api.ReconnectDelaySeconds,
            AutoReconnectOnFailure = api.AutoReconnectOnFailure,
            ShowNotificationOnDisconnect = api.ShowNotificationOnDisconnect,
            ShowNotificationOnReconnect = api.ShowNotificationOnReconnect,
            PlayNotificationSound = api.PlayNotificationSound,
        };

        result.Performance = new PerformanceSettings
        {
            VideoQuality = AtcSoft.VideoSurveillance.Helpers.VideoQualityHelper.Normalize(api.VideoQuality) ?? AtcSoft.VideoSurveillance.Helpers.VideoQualityHelper.Auto,
            HardwareAcceleration = api.HardwareAcceleration,
            LowLatencyMode = api.LowLatencyMode,
            BufferDurationMs = api.BufferDurationMs,
            RtspTransport = api.RtspTransport.ToWireValue(),
            MaxLatencyMs = api.MaxLatencyMs,
        };

        result.MotionDetection = new MotionDetectionSettings
        {
            Sensitivity = api.MotionSensitivity,
            SmallestObjectPercent = api.SmallestObjectPercent,
            StoppedObjectSeconds = api.StoppedObjectSeconds,
            IgnoreLightingChanges = api.IgnoreLightingChanges,
            AnalysisFrameRate = api.AnalysisFrameRate,
            AnalysisWidth = api.AnalysisWidth,
            PostMotionDurationSeconds = api.PostMotionDurationSeconds,
            CooldownSeconds = api.CooldownSeconds,
            BoundingBox = new BoundingBoxSettings
            {
                ShowInGrid = api.BoundingBoxShowInGrid,
                ShowInFullScreen = api.BoundingBoxShowInFullScreen,
                Color = api.BoundingBoxColor ?? string.Empty,
                Thickness = api.BoundingBoxThickness,
                Padding = api.BoundingBoxPadding,
                Smoothing = api.BoundingBoxSmoothing,
            },
        };

        result.Recording = new RecordingSettings
        {
            RecordingPath = api.RecordingPath ?? string.Empty,
            RecordingFormat = api.RecordingFormat.ToWireValue(),
            TranscodeVideoCodec = api.RecordAsH264 ? AtcSoft.VideoSurveillance.Enums.VideoTranscodeCodec.H264 : AtcSoft.VideoSurveillance.Enums.VideoTranscodeCodec.None,
            EnableRecordingOnMotion = api.EnableRecordingOnMotion,
            EnableRecordingOnConnect = api.EnableRecordingOnConnect,
            EnableActivityIndex = api.EnableActivityIndex,
            EnableHourlySegmentation = api.EnableHourlySegmentation,
            UseDailySubfolders = api.UseDailySubfolders,
            SeparateThumbnailFolder = api.SeparateThumbnailFolder,
            MaxRecordingDurationMinutes = api.MaxRecordingDurationMinutes,
            ThumbnailTileCount = api.ThumbnailTileCount,
            EnableTimelapse = api.EnableTimelapse,
            TimelapseInterval = api.TimelapseInterval ?? string.Empty,
            Cleanup = new MediaCleanupSettings
            {
                Schedule = ParseEnumOrDefault(api.CleanupSchedule.ToString(), MediaCleanupSchedule.Disabled),
                RecordingRetentionDays = api.RecordingRetentionDays,
                IncludeSnapshots = api.CleanupIncludeSnapshots,
                SnapshotRetentionDays = api.SnapshotRetentionDays,
                EnableDiskSpaceGuard = api.EnableDiskSpaceGuard,
                MinFreeSpaceMb = api.MinFreeSpaceMb > 0 ? api.MinFreeSpaceMb : DefaultCleanup.MinFreeSpaceMb,
                MaxRecordingStorageGb = Math.Max(0, api.MaxRecordingStorageGb),
            },
            PlaybackOverlay = new PlaybackOverlaySettings
            {
                ShowFilename = api.PlaybackShowFilename,
                FilenameColor = api.PlaybackFilenameColor ?? string.Empty,
                ShowTimestamp = api.PlaybackShowTimestamp,
                TimestampColor = api.PlaybackTimestampColor ?? string.Empty,
            },
        };

        result.Advanced = new AdvancedSettings
        {
            EnableDebugLogging = api.EnableDebugLogging,
            LogPath = api.LogPath ?? string.Empty,
        };

        return result;
    }

    /// <summary>
    /// Builds the flat API DTO from the in-memory cache. Theme and window
    /// state are sent so the API server has a record of the last values
    /// written, but the WPF client never reads them back — the local
    /// <see cref="ClientPreferences"/> file is authoritative for those. The
    /// language is not sent: it belongs to this client.
    /// </summary>
    internal static UpdateSettingsRequest BuildApiSettings(
        ApplicationSettings settings)
    {
        var themeBase = KnownValue.FromName(AppSettingsThemeBase.KnownValues, settings.General.ThemeBase, AppSettingsThemeBase.Dark);
        var overlayPosition = KnownValue.FromName(AppSettingsOverlayPosition.KnownValues, settings.CameraDisplay.OverlayPosition.ToString(), AppSettingsOverlayPosition.TopLeft);
        var protocol = KnownValue.FromName(AppSettingsDefaultProtocol.KnownValues, settings.Connection.DefaultProtocol.ToString(), AppSettingsDefaultProtocol.Rtsp);
        var rtspTransport = KnownValue.FromName(AppSettingsRtspTransport.KnownValues, settings.Performance.RtspTransport, AppSettingsRtspTransport.Tcp);
        var recordingFormat = KnownValue.FromName(AppSettingsRecordingFormat.KnownValues, settings.Recording.RecordingFormat, AppSettingsRecordingFormat.Mp4);
        var cleanupSchedule = KnownValue.FromName(AppSettingsCleanupSchedule.KnownValues, settings.Recording.Cleanup.Schedule.ToString(), AppSettingsCleanupSchedule.Disabled);

        return new UpdateSettingsRequest(
            ThemeBase: themeBase,
            ThemeAccent: settings.General.ThemeAccent,
            ConnectOnStartup: settings.General.ConnectCamerasOnStartup,
            StartMaximized: settings.General.StartMaximized,
            ShowOverlayTitle: settings.CameraDisplay.ShowOverlayTitle,
            ShowOverlayDescription: settings.CameraDisplay.ShowOverlayDescription,
            ShowOverlayTime: settings.CameraDisplay.ShowOverlayTime,
            ShowOverlayConnectionStatus: settings.CameraDisplay.ShowOverlayConnectionStatus,
            ShowOverlayQuickActions: settings.CameraDisplay.ShowOverlayQuickActions,
            ShowOverlayConnectionTypeIcon: settings.CameraDisplay.ShowOverlayConnectionTypeIcon,
            ConnectionTypeIconPlacement: KnownValue.FromName(AppSettingsConnectionTypeIconPlacement.KnownValues, settings.CameraDisplay.ConnectionTypeIconPlacement.ToString(), AppSettingsConnectionTypeIconPlacement.SameRow),
            OverlayOpacity: settings.CameraDisplay.OverlayOpacity,
            OverlayPosition: overlayPosition,
            AllowDragAndDropReorder: settings.CameraDisplay.AllowDragAndDropReorder,
            AutoSaveLayoutChanges: settings.CameraDisplay.AutoSaveLayoutChanges,
            SnapshotPath: settings.CameraDisplay.SnapshotPath,
            DefaultProtocol: protocol,
            DefaultPort: settings.Connection.DefaultPort,
            ConnectionTimeoutSeconds: settings.Connection.ConnectionTimeoutSeconds,
            ReconnectDelaySeconds: settings.Connection.ReconnectDelaySeconds,
            AutoReconnectOnFailure: settings.Connection.AutoReconnectOnFailure,
            ShowNotificationOnDisconnect: settings.Connection.ShowNotificationOnDisconnect,
            ShowNotificationOnReconnect: settings.Connection.ShowNotificationOnReconnect,
            PlayNotificationSound: settings.Connection.PlayNotificationSound,
            VideoQuality: settings.Performance.VideoQuality,
            HardwareAcceleration: settings.Performance.HardwareAcceleration,
            LowLatencyMode: settings.Performance.LowLatencyMode,
            BufferDurationMs: settings.Performance.BufferDurationMs,
            RtspTransport: rtspTransport,
            MaxLatencyMs: settings.Performance.MaxLatencyMs,
            MotionSensitivity: settings.MotionDetection.Sensitivity,
            SmallestObjectPercent: settings.MotionDetection.SmallestObjectPercent,
            StoppedObjectSeconds: settings.MotionDetection.StoppedObjectSeconds,
            IgnoreLightingChanges: settings.MotionDetection.IgnoreLightingChanges,
            AnalysisFrameRate: settings.MotionDetection.AnalysisFrameRate,
            AnalysisWidth: settings.MotionDetection.AnalysisWidth,
            PostMotionDurationSeconds: settings.MotionDetection.PostMotionDurationSeconds,
            CooldownSeconds: settings.MotionDetection.CooldownSeconds,
            BoundingBoxShowInGrid: settings.MotionDetection.BoundingBox.ShowInGrid,
            BoundingBoxShowInFullScreen: settings.MotionDetection.BoundingBox.ShowInFullScreen,
            BoundingBoxColor: settings.MotionDetection.BoundingBox.Color,
            BoundingBoxThickness: settings.MotionDetection.BoundingBox.Thickness,
            BoundingBoxPadding: settings.MotionDetection.BoundingBox.Padding,
            BoundingBoxSmoothing: settings.MotionDetection.BoundingBox.Smoothing,
            RecordingPath: settings.Recording.RecordingPath,
            RecordingFormat: recordingFormat,
            RecordAsH264: settings.Recording.TranscodeVideoCodec == AtcSoft.VideoSurveillance.Enums.VideoTranscodeCodec.H264,
            EnableRecordingOnMotion: settings.Recording.EnableRecordingOnMotion,
            EnableRecordingOnConnect: settings.Recording.EnableRecordingOnConnect,
            EnableActivityIndex: settings.Recording.EnableActivityIndex,
            EnableHourlySegmentation: settings.Recording.EnableHourlySegmentation,
            UseDailySubfolders: settings.Recording.UseDailySubfolders,
            SeparateThumbnailFolder: settings.Recording.SeparateThumbnailFolder,
            MaxRecordingDurationMinutes: settings.Recording.MaxRecordingDurationMinutes,
            ThumbnailTileCount: settings.Recording.ThumbnailTileCount,
            EnableTimelapse: settings.Recording.EnableTimelapse,
            TimelapseInterval: settings.Recording.TimelapseInterval,
            CleanupSchedule: cleanupSchedule,
            RecordingRetentionDays: settings.Recording.Cleanup.RecordingRetentionDays,
            CleanupIncludeSnapshots: settings.Recording.Cleanup.IncludeSnapshots,
            SnapshotRetentionDays: settings.Recording.Cleanup.SnapshotRetentionDays,
            EnableDiskSpaceGuard: settings.Recording.Cleanup.EnableDiskSpaceGuard,
            MinFreeSpaceMb: settings.Recording.Cleanup.MinFreeSpaceMb,
            MaxRecordingStorageGb: settings.Recording.Cleanup.MaxRecordingStorageGb,
            PlaybackShowFilename: settings.Recording.PlaybackOverlay.ShowFilename,
            PlaybackFilenameColor: settings.Recording.PlaybackOverlay.FilenameColor,
            PlaybackShowTimestamp: settings.Recording.PlaybackOverlay.ShowTimestamp,
            PlaybackTimestampColor: settings.Recording.PlaybackOverlay.TimestampColor,
            EnableDebugLogging: settings.Advanced.EnableDebugLogging,
            LogPath: settings.Advanced.LogPath);
    }

    private static T ParseEnumOrDefault<T>(
        string? source,
        T fallback)
        where T : struct, Enum
        => Enum.TryParse<T>(source, ignoreCase: true, out var v) ? v : fallback;
}