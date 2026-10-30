namespace AtcSoft.VideoSurveillance.Blazor.App.Pages.SettingsTabs;

/// <summary>
/// The settings as they are edited on the settings page. The starting
/// values are the server's defaults; a test keeps them equal.
/// </summary>
public sealed class SettingsState
{
    private bool loadedUseDailySubfolders = true;
    private bool loadedSeparateThumbnailFolder = true;

    public AppSettingsThemeBase ThemeBase { get; set; } = AppSettingsThemeBase.Dark;

    public string ThemeAccent { get; set; } = "Blue";

    public bool ConnectOnStartup { get; set; } = true;

    public bool StartMaximized { get; set; }

    public bool ShowOverlayTitle { get; set; } = true;

    public bool ShowOverlayDescription { get; set; } = true;

    public bool ShowOverlayTime { get; set; }

    public bool ShowOverlayConnectionStatus { get; set; } = true;

    public bool ShowOverlayQuickActions { get; set; } = true;

    public double OverlayOpacity { get; set; } = 0.7;

    public AppSettingsOverlayPosition OverlayPosition { get; set; } = AppSettingsOverlayPosition.TopLeft;

    public bool ShowOverlayConnectionTypeIcon { get; set; }

    public AppSettingsConnectionTypeIconPlacement ConnectionTypeIconPlacement { get; set; } = AppSettingsConnectionTypeIconPlacement.SameRow;

    public bool AllowDragAndDropReorder { get; set; } = true;

    public bool AutoSaveLayoutChanges { get; set; } = true;

    public string SnapshotPath { get; set; } = string.Empty;

    public AppSettingsDefaultProtocol DefaultProtocol { get; set; } = AppSettingsDefaultProtocol.Rtsp;

    public int DefaultPort { get; set; } = 554;

    public int ConnectionTimeoutSeconds { get; set; } = 10;

    public int ReconnectDelaySeconds { get; set; } = 10;

    public bool AutoReconnectOnFailure { get; set; } = true;

    public bool ShowNotificationOnDisconnect { get; set; } = true;

    public bool ShowNotificationOnReconnect { get; set; }

    public bool PlayNotificationSound { get; set; }

    public string VideoQuality { get; set; } = "Auto";

    public bool HardwareAcceleration { get; set; } = true;

    public bool LowLatencyMode { get; set; }

    public int BufferDurationMs { get; set; } = 500;

    public AppSettingsRtspTransport RtspTransport { get; set; } = AppSettingsRtspTransport.Tcp;

    public int MaxLatencyMs { get; set; } = 500;

    public int MotionSensitivity { get; set; } = 30;

    public double SmallestObjectPercent { get; set; } = 0.02;

    public int StoppedObjectSeconds { get; set; } = 60;

    public bool IgnoreLightingChanges { get; set; } = true;

    public int AnalysisFrameRate { get; set; } = 15;

    /// <summary>
    /// Gets or sets the longer side of the analysed picture; the other side follows the camera's
    /// picture, so the height is left to the server.
    /// </summary>
    public int AnalysisWidth { get; set; } = 800;

    public int PostMotionDurationSeconds { get; set; } = 10;

    public int CooldownSeconds { get; set; } = 5;

    public bool BoundingBoxShowInGrid { get; set; }

    public bool BoundingBoxShowInFullScreen { get; set; }

    public string BoundingBoxColor { get; set; } = "Red";

    public int BoundingBoxThickness { get; set; } = 2;

    public int BoundingBoxPadding { get; set; } = 4;

    public double BoundingBoxSmoothing { get; set; } = 0.3;

    public string RecordingPath { get; set; } = string.Empty;

    public AppSettingsRecordingFormat RecordingFormat { get; set; } = AppSettingsRecordingFormat.Mkv;

    public bool RecordAsH264 { get; set; }

    public bool EnableRecordingOnMotion { get; set; }

    public bool EnableRecordingOnConnect { get; set; }

    public bool EnableActivityIndex { get; set; } = true;

    public bool EnableHourlySegmentation { get; set; } = true;

    public bool UseDailySubfolders { get; set; } = true;

    public bool SeparateThumbnailFolder { get; set; } = true;

    public int MaxRecordingDurationMinutes { get; set; } = 60;

    public int ThumbnailTileCount { get; set; } = 4;

    public bool EnableTimelapse { get; set; }

    public string TimelapseInterval { get; set; } = "5m";

    public AppSettingsCleanupSchedule CleanupSchedule { get; set; } = AppSettingsCleanupSchedule.OnStartupAndPeriodically;

    public int RecordingRetentionDays { get; set; } = 30;

    public bool CleanupIncludeSnapshots { get; set; }

    public int SnapshotRetentionDays { get; set; } = 7;

    public bool EnableDiskSpaceGuard { get; set; } = true;

    public int MinFreeSpaceMb { get; set; } = 2048;

    public int MaxRecordingStorageGb { get; set; }

    public bool PlaybackShowFilename { get; set; } = true;

    public string PlaybackFilenameColor { get; set; } = "White";

    public bool PlaybackShowTimestamp { get; set; } = true;

    public string PlaybackTimestampColor { get; set; } = "White";

    public bool EnableDebugLogging { get; set; }

    public string LogPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether a folder layout switch differs from
    /// what the server has. Saving then moves the existing recordings.
    /// </summary>
    public bool HasRecordingLayoutChanged
        => UseDailySubfolders != loadedUseDailySubfolders ||
           SeparateThumbnailFolder != loadedSeparateThumbnailFolder;

    /// <summary>
    /// Gets where a recording goes with the current folder layout, below the recording folder.
    /// </summary>
    public string RecordingPathExample
        => RecordingLayoutExample.Build(RecordingFormat.ToString(), UseDailySubfolders, SeparateThumbnailFolder).Video;

    /// <summary>
    /// Gets where its thumbnail goes.
    /// </summary>
    public string ThumbnailPathExample
        => RecordingLayoutExample.Build(RecordingFormat.ToString(), UseDailySubfolders, SeparateThumbnailFolder).Thumbnail;

    /// <summary>
    /// Gets the help of the "folder per day" switch: the recording path with the switch on and off.
    /// </summary>
    public string UseDailySubfoldersHint
        => OnOffHint(
            RecordingLayoutExample.Build(RecordingFormat.ToString(), useDailySubfolders: true, SeparateThumbnailFolder).Video,
            RecordingLayoutExample.Build(RecordingFormat.ToString(), useDailySubfolders: false, SeparateThumbnailFolder).Video);

    /// <summary>
    /// Gets the help of the "separate thumbnail folder" switch: the thumbnail path with the switch on and off.
    /// </summary>
    public string SeparateThumbnailFolderHint
        => OnOffHint(
            RecordingLayoutExample.Build(RecordingFormat.ToString(), UseDailySubfolders, separateThumbnailFolder: true).Thumbnail,
            RecordingLayoutExample.Build(RecordingFormat.ToString(), UseDailySubfolders, separateThumbnailFolder: false).Thumbnail);

    public void LoadFrom(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ThemeBase = settings.ThemeBase;
        ThemeAccent = settings.ThemeAccent ?? "Blue";
        ConnectOnStartup = settings.ConnectOnStartup;
        StartMaximized = settings.StartMaximized;

        ShowOverlayTitle = settings.ShowOverlayTitle;
        ShowOverlayDescription = settings.ShowOverlayDescription;
        ShowOverlayTime = settings.ShowOverlayTime;
        ShowOverlayConnectionStatus = settings.ShowOverlayConnectionStatus;
        ShowOverlayQuickActions = settings.ShowOverlayQuickActions;
        OverlayOpacity = settings.OverlayOpacity;
        OverlayPosition = settings.OverlayPosition;
        ShowOverlayConnectionTypeIcon = settings.ShowOverlayConnectionTypeIcon;
        ConnectionTypeIconPlacement = settings.ConnectionTypeIconPlacement;
        AllowDragAndDropReorder = settings.AllowDragAndDropReorder;
        AutoSaveLayoutChanges = settings.AutoSaveLayoutChanges;
        SnapshotPath = settings.SnapshotPath ?? string.Empty;

        DefaultProtocol = settings.DefaultProtocol;
        DefaultPort = settings.DefaultPort;
        ConnectionTimeoutSeconds = settings.ConnectionTimeoutSeconds;
        ReconnectDelaySeconds = settings.ReconnectDelaySeconds;
        AutoReconnectOnFailure = settings.AutoReconnectOnFailure;
        ShowNotificationOnDisconnect = settings.ShowNotificationOnDisconnect;
        ShowNotificationOnReconnect = settings.ShowNotificationOnReconnect;
        PlayNotificationSound = settings.PlayNotificationSound;

        VideoQuality = string.IsNullOrEmpty(settings.VideoQuality) ? "Auto" : settings.VideoQuality;
        HardwareAcceleration = settings.HardwareAcceleration;
        LowLatencyMode = settings.LowLatencyMode;
        BufferDurationMs = settings.BufferDurationMs;
        RtspTransport = settings.RtspTransport;
        MaxLatencyMs = settings.MaxLatencyMs;

        MotionSensitivity = settings.MotionSensitivity;
        SmallestObjectPercent = settings.SmallestObjectPercent;
        StoppedObjectSeconds = settings.StoppedObjectSeconds;
        IgnoreLightingChanges = settings.IgnoreLightingChanges;
        AnalysisFrameRate = settings.AnalysisFrameRate;
        AnalysisWidth = settings.AnalysisWidth;
        PostMotionDurationSeconds = settings.PostMotionDurationSeconds;
        CooldownSeconds = settings.CooldownSeconds;
        BoundingBoxShowInGrid = settings.BoundingBoxShowInGrid;
        BoundingBoxShowInFullScreen = settings.BoundingBoxShowInFullScreen;
        BoundingBoxColor = ColorChoices.Normalize(settings.BoundingBoxColor, "Red");
        BoundingBoxThickness = settings.BoundingBoxThickness;
        BoundingBoxPadding = settings.BoundingBoxPadding;
        BoundingBoxSmoothing = settings.BoundingBoxSmoothing;

        RecordingPath = settings.RecordingPath ?? string.Empty;
        RecordingFormat = settings.RecordingFormat;
        RecordAsH264 = settings.RecordAsH264;
        EnableRecordingOnMotion = settings.EnableRecordingOnMotion;
        EnableRecordingOnConnect = settings.EnableRecordingOnConnect;
        EnableActivityIndex = settings.EnableActivityIndex;
        EnableHourlySegmentation = settings.EnableHourlySegmentation;
        MaxRecordingDurationMinutes = settings.MaxRecordingDurationMinutes;

        // The server makes a thumbnail of one tile or of four.
        ThumbnailTileCount = settings.ThumbnailTileCount == 1 ? 1 : 4;
        UseDailySubfolders = settings.UseDailySubfolders;
        SeparateThumbnailFolder = settings.SeparateThumbnailFolder;
        loadedUseDailySubfolders = UseDailySubfolders;
        loadedSeparateThumbnailFolder = SeparateThumbnailFolder;
        EnableTimelapse = settings.EnableTimelapse;
        TimelapseInterval = settings.TimelapseInterval ?? "5m";
        CleanupSchedule = settings.CleanupSchedule;
        RecordingRetentionDays = settings.RecordingRetentionDays;
        CleanupIncludeSnapshots = settings.CleanupIncludeSnapshots;
        SnapshotRetentionDays = settings.SnapshotRetentionDays;
        EnableDiskSpaceGuard = settings.EnableDiskSpaceGuard;
        MinFreeSpaceMb = settings.MinFreeSpaceMb > 0 ? settings.MinFreeSpaceMb : 2048;
        MaxRecordingStorageGb = Math.Max(0, settings.MaxRecordingStorageGb);
        PlaybackShowFilename = settings.PlaybackShowFilename;
        PlaybackFilenameColor = ColorChoices.Normalize(settings.PlaybackFilenameColor, "White");
        PlaybackShowTimestamp = settings.PlaybackShowTimestamp;
        PlaybackTimestampColor = ColorChoices.Normalize(settings.PlaybackTimestampColor, "White");

        EnableDebugLogging = settings.EnableDebugLogging;
        LogPath = settings.LogPath ?? string.Empty;
    }

    public UpdateSettingsRequest ToApiModel()
        => new(
            ThemeBase: ThemeBase,
            ThemeAccent: ThemeAccent,
            ConnectOnStartup: ConnectOnStartup,
            StartMaximized: StartMaximized,
            ShowOverlayTitle: ShowOverlayTitle,
            ShowOverlayDescription: ShowOverlayDescription,
            ShowOverlayTime: ShowOverlayTime,
            ShowOverlayConnectionStatus: ShowOverlayConnectionStatus,
            ShowOverlayQuickActions: ShowOverlayQuickActions,
            ShowOverlayConnectionTypeIcon: ShowOverlayConnectionTypeIcon,
            ConnectionTypeIconPlacement: ConnectionTypeIconPlacement,
            OverlayOpacity: OverlayOpacity,
            OverlayPosition: OverlayPosition,
            AllowDragAndDropReorder: AllowDragAndDropReorder,
            AutoSaveLayoutChanges: AutoSaveLayoutChanges,
            SnapshotPath: SnapshotPath,
            DefaultProtocol: DefaultProtocol,
            DefaultPort: DefaultPort,
            ConnectionTimeoutSeconds: ConnectionTimeoutSeconds,
            ReconnectDelaySeconds: ReconnectDelaySeconds,
            AutoReconnectOnFailure: AutoReconnectOnFailure,
            ShowNotificationOnDisconnect: ShowNotificationOnDisconnect,
            ShowNotificationOnReconnect: ShowNotificationOnReconnect,
            PlayNotificationSound: PlayNotificationSound,
            VideoQuality: VideoQuality,
            HardwareAcceleration: HardwareAcceleration,
            LowLatencyMode: LowLatencyMode,
            BufferDurationMs: BufferDurationMs,
            RtspTransport: RtspTransport,
            MaxLatencyMs: MaxLatencyMs,
            MotionSensitivity: MotionSensitivity,
            SmallestObjectPercent: SmallestObjectPercent,
            StoppedObjectSeconds: StoppedObjectSeconds,
            IgnoreLightingChanges: IgnoreLightingChanges,
            AnalysisFrameRate: AnalysisFrameRate,
            AnalysisWidth: AnalysisWidth,
            PostMotionDurationSeconds: PostMotionDurationSeconds,
            CooldownSeconds: CooldownSeconds,
            BoundingBoxShowInGrid: BoundingBoxShowInGrid,
            BoundingBoxShowInFullScreen: BoundingBoxShowInFullScreen,
            BoundingBoxColor: BoundingBoxColor,
            BoundingBoxThickness: BoundingBoxThickness,
            BoundingBoxPadding: BoundingBoxPadding,
            BoundingBoxSmoothing: BoundingBoxSmoothing,
            RecordingPath: RecordingPath,
            RecordingFormat: RecordingFormat,
            RecordAsH264: RecordAsH264,
            EnableRecordingOnMotion: EnableRecordingOnMotion,
            EnableRecordingOnConnect: EnableRecordingOnConnect,
            EnableActivityIndex: EnableActivityIndex,
            EnableHourlySegmentation: EnableHourlySegmentation,
            MaxRecordingDurationMinutes: MaxRecordingDurationMinutes,
            ThumbnailTileCount: ThumbnailTileCount,
            UseDailySubfolders: UseDailySubfolders,
            SeparateThumbnailFolder: SeparateThumbnailFolder,
            EnableTimelapse: EnableTimelapse,
            TimelapseInterval: TimelapseInterval,
            CleanupSchedule: CleanupSchedule,
            RecordingRetentionDays: RecordingRetentionDays,
            CleanupIncludeSnapshots: CleanupIncludeSnapshots,
            SnapshotRetentionDays: SnapshotRetentionDays,
            EnableDiskSpaceGuard: EnableDiskSpaceGuard,
            MinFreeSpaceMb: MinFreeSpaceMb,
            MaxRecordingStorageGb: MaxRecordingStorageGb,
            PlaybackShowFilename: PlaybackShowFilename,
            PlaybackFilenameColor: PlaybackFilenameColor,
            PlaybackShowTimestamp: PlaybackShowTimestamp,
            PlaybackTimestampColor: PlaybackTimestampColor,
            EnableDebugLogging: EnableDebugLogging,
            LogPath: LogPath);

    private static string OnOffHint(
        string on,
        string off)
        => string.Format(CultureInfo.CurrentCulture, Translations.RecordingLayoutOnOffHint2, on, off);

    /// <summary>
    /// Gets the concrete corner a placement resolves to for the current
    /// overlay position: the icon always uses a corner adjacent to the info box.
    /// </summary>
    private static string Corner(
        bool isTop,
        bool isLeft)
        => (isTop, isLeft) switch
        {
            (true, true) => Translations.OverlayPositionTopLeft,
            (true, false) => Translations.OverlayPositionTopRight,
            (false, true) => Translations.OverlayPositionBottomLeft,
            _ => Translations.OverlayPositionBottomRight,
        };

    public string GetConnectionTypeIconPlacementLabel(
        AppSettingsConnectionTypeIconPlacement placement)
    {
        var isTop = OverlayPosition.Value is AppSettingsOverlayPosition.Names.TopLeft or AppSettingsOverlayPosition.Names.TopRight;
        var isLeft = OverlayPosition.Value is AppSettingsOverlayPosition.Names.TopLeft or AppSettingsOverlayPosition.Names.BottomLeft;

        if (placement == AppSettingsConnectionTypeIconPlacement.SameColumn)
        {
            isTop = !isTop;
            return string.Format(CultureInfo.CurrentCulture, Translations.ConnectionTypeIconPositionSameColumnFormat1, Corner(isTop, isLeft));
        }

        isLeft = !isLeft;
        return string.Format(CultureInfo.CurrentCulture, Translations.ConnectionTypeIconPositionSameRowFormat1, Corner(isTop, isLeft));
    }
}