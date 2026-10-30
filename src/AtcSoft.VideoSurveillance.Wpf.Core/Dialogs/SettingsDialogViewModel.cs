namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// View model for the settings dialog.
/// </summary>
[SuppressMessage("", "SA1124: Do not use regions", Justification = "OK")]
[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public partial class SettingsDialogViewModel : ViewModelDialogBase
{
    private readonly IApplicationSettingsService settingsService;
    private readonly IRecordingLayoutSynchronizer? recordingLayoutSynchronizer;
    private readonly ILogger logger;

    // Original values for restoration on cancel
    private string originalThemeBase = "Dark";
    private string originalThemeAccent = "Blue";
    private string originalLanguage = "1033";

    public SettingsDialogViewModel(
        IApplicationSettingsService settingsService,
        IRecordingLayoutSynchronizer? recordingLayoutSynchronizer = null,
        ILogger<SettingsDialogViewModel>? logger = null)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.recordingLayoutSynchronizer = recordingLayoutSynchronizer;
        this.logger = logger ?? (ILogger)NullLogger.Instance;
        LoadSettings();
    }

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    public static string DialogTitle => Translations.Settings;

    #region General Tab Settings

    /// <summary>
    /// Gets or sets the selected language LCID (e.g., "1033" for en-US).
    /// </summary>
    [ObservableProperty]
    private string selectedLanguage = "1033";

    [ObservableProperty]
    private bool connectCamerasOnStartup = true;

    [ObservableProperty]
    private bool startMaximized;

    [ObservableProperty]
    private bool startRibbonCollapsed;

    #endregion

    #region Camera Display Tab Settings

    [ObservableProperty]
    private bool showCameraOverlayTitle = true;

    [ObservableProperty]
    private bool showCameraOverlayDescription = true;

    [ObservableProperty]
    private bool showCameraOverlayTime;

    [ObservableProperty]
    private bool showCameraOverlayConnectionStatus = true;

    [ObservableProperty]
    private bool showCameraOverlayQuickActions = true;

    [ObservableProperty]
    private string selectedOverlayOpacity = DropDownItemsFactory.DefaultOverlayOpacity;

    public IDictionary<string, string> OverlayOpacityItems
        => DropDownItemsFactory.OverlayOpacityItems;

    [ObservableProperty(DependentPropertyNames = [nameof(ConnectionTypeIconPlacementItems)])]
    private string selectedOverlayPosition = DropDownItemsFactory.DefaultOverlayPosition;

    public IDictionary<string, string> OverlayPositionItems
        => DropDownItemsFactory.OverlayPositionItems;

    [ObservableProperty]
    private bool showCameraOverlayConnectionTypeIcon;

    [ObservableProperty]
    private string selectedConnectionTypeIconPlacement = DropDownItemsFactory.DefaultConnectionTypeIconPlacement;

    /// <summary>
    /// Gets the icon placements, labelled with the concrete corner they resolve
    /// to for the currently selected default overlay position.
    /// </summary>
    public IDictionary<string, string> ConnectionTypeIconPlacementItems
        => DropDownItemsFactory.GetConnectionTypeIconPlacementItems(
            Enum.TryParse<OverlayPosition>(SelectedOverlayPosition, out var position)
                ? position
                : OverlayPosition.TopLeft);

    [ObservableProperty]
    private bool allowDragAndDropReorder = true;

    [ObservableProperty]
    private bool autoSaveLayoutChanges = true;

    [ObservableProperty]
    private DirectoryInfo? snapshotPath;

    #endregion

    #region Connection Tab Settings

    [ObservableProperty]
    private string selectedDefaultProtocol = DropDownItemsFactory.DefaultProtocol;

    public IDictionary<string, string> DefaultProtocolItems
        => DropDownItemsFactory.ProtocolItems;

    [ObservableProperty]
    private int defaultPort = 554;

    [ObservableProperty]
    private int connectionTimeoutSeconds = 10;

    [ObservableProperty]
    private int reconnectDelaySeconds = 10;

    [ObservableProperty]
    private bool autoReconnectOnFailure = true;

    [ObservableProperty]
    private bool showNotificationOnDisconnect = true;

    [ObservableProperty]
    private bool showNotificationOnReconnect;

    [ObservableProperty]
    private bool playNotificationSound;

    #endregion

    #region Performance Tab Settings

    [ObservableProperty]
    private string selectedVideoQuality = DropDownItemsFactory.DefaultVideoQuality;

    public IDictionary<string, string> VideoQualityItems
        => DropDownItemsFactory.VideoQualityItems;

    [ObservableProperty]
    private bool hardwareAcceleration = true;

    [ObservableProperty]
    private bool lowLatencyMode;

    [ObservableProperty]
    private int bufferDurationMs = 500;

    [ObservableProperty]
    private string selectedRtspTransport = DropDownItemsFactory.DefaultRtspTransport;

    public IDictionary<string, string> RtspTransportItems
        => DropDownItemsFactory.RtspTransportItems;

    [ObservableProperty]
    private int maxLatencyMs = 500;

    #endregion

    #region Motion Detection Tab Settings

    [ObservableProperty]
    private int motionSensitivity = DropDownItemsFactory.DefaultMotionSensitivity;

    // Decimal, as the decimal box takes it.
    [ObservableProperty]
    private decimal smallestObjectPercent = (decimal)MotionDetectorOptions.DefaultSmallestObjectPercent;

    [ObservableProperty]
    private int stoppedObjectSeconds = MotionDetectorOptions.DefaultStoppedObjectSeconds;

    [ObservableProperty]
    private bool ignoreLightingChanges = true;

    [ObservableProperty]
    private int postMotionDurationSeconds = DropDownItemsFactory.DefaultPostMotionDuration;

    [ObservableProperty]
    private int analysisFrameRate = 2;

    [ObservableProperty(DependentPropertyNames = [nameof(AnalysisResolutionItems)])]
    private string selectedAnalysisResolution = DropDownItemsFactory.DefaultMotionAnalysisResolution;

    public IDictionary<string, string> AnalysisResolutionItems
        => DropDownItemsFactory.MotionAnalysisResolutionItemsWith(SelectedAnalysisResolution);

    [ObservableProperty]
    private int cooldownSeconds = 5;

    [ObservableProperty]
    private bool showBoundingBoxInGrid;

    [ObservableProperty]
    private bool showBoundingBoxInFullScreen;

    [ObservableProperty]
    private string selectedBoundingBoxColor = DropDownItemsFactory.DefaultBoundingBoxColor;

    [ObservableProperty]
    private string selectedBoundingBoxThickness = DropDownItemsFactory.DefaultBoundingBoxThickness.ToString(CultureInfo.InvariantCulture);

    public IDictionary<string, string> BoundingBoxThicknessItems
        => DropDownItemsFactory.BoundingBoxThicknessItems;

    [ObservableProperty]
    private string selectedBoundingBoxMinArea = DropDownItemsFactory.DefaultBoundingBoxMinArea.ToString(CultureInfo.InvariantCulture);

    public IDictionary<string, string> BoundingBoxMinAreaItems
        => DropDownItemsFactory.BoundingBoxMinAreaItems;

    #endregion

    #region Recording Tab Settings

    [ObservableProperty]
    private DirectoryInfo? recordingPath;

    [ObservableProperty(DependentPropertyNames = [nameof(RecordingPathExample), nameof(ThumbnailPathExample), nameof(RecordingLayoutExampleSummary), nameof(UseDailySubfoldersHint), nameof(SeparateThumbnailFolderHint)])]
    private string selectedRecordingFormat = DropDownItemsFactory.DefaultRecordingFormat;

    public IDictionary<string, string> RecordingFormatItems
        => DropDownItemsFactory.RecordingFormatItems;

    [ObservableProperty]
    private bool enableRecordingOnMotion;

    [ObservableProperty]
    private bool enableRecordingOnConnect;

    [ObservableProperty]
    private bool enableActivityIndex = true;

    // Recording Storage Layout Settings
    [ObservableProperty(DependentPropertyNames = [nameof(RecordingPathExample), nameof(ThumbnailPathExample), nameof(RecordingLayoutExampleSummary), nameof(SeparateThumbnailFolderHint)])]
    private bool useDailySubfolders = true;

    [ObservableProperty(DependentPropertyNames = [nameof(RecordingPathExample), nameof(ThumbnailPathExample), nameof(RecordingLayoutExampleSummary), nameof(UseDailySubfoldersHint)])]
    private bool separateThumbnailFolder = true;

    /// <summary>
    /// Gets where a recording goes with the current choice, relative to the recording folder.
    /// </summary>
    public string RecordingPathExample
        => RecordingLayoutExamples.Video(UseDailySubfolders, SeparateThumbnailFolder, SelectedRecordingFormat);

    /// <summary>
    /// Gets where a thumbnail goes with the current choice, relative to the recording folder.
    /// </summary>
    public string ThumbnailPathExample
        => RecordingLayoutExamples.Thumbnail(UseDailySubfolders, SeparateThumbnailFolder, SelectedRecordingFormat);

    /// <summary>
    /// Gets the example shown under the two layout switches.
    /// </summary>
    public string RecordingLayoutExampleSummary
        => RecordingLayoutExamples.Summary(UseDailySubfolders, SeparateThumbnailFolder, SelectedRecordingFormat);

    /// <summary>
    /// Gets the hover help of the "folder per day" switch.
    /// </summary>
    public string UseDailySubfoldersHint
        => RecordingLayoutExamples.DailySubfoldersHint(SeparateThumbnailFolder, SelectedRecordingFormat);

    /// <summary>
    /// Gets the hover help of the "separate thumbnail folder" switch.
    /// </summary>
    public string SeparateThumbnailFolderHint
        => RecordingLayoutExamples.SeparateThumbnailFolderHint(UseDailySubfolders, SelectedRecordingFormat);

    /// <summary>
    /// Whether the recordings are being moved to a changed folder layout.
    /// The dialog shows a busy indicator and stays open meanwhile.
    /// </summary>
    [ObservableProperty]
    private bool isApplyingRecordingLayout;

    // Recording Segmentation Settings
    [ObservableProperty]
    private bool enableHourlySegmentation = true;

    [ObservableProperty]
    private string selectedMaxRecordingDuration = DropDownItemsFactory.DefaultMaxRecordingDuration.ToString(CultureInfo.InvariantCulture);

    public IDictionary<string, string> MaxRecordingDurationItems
        => DropDownItemsFactory.MaxRecordingDurationItems;

    // Whether recordings are made as H.264 whatever the camera sends.
    [ObservableProperty]
    private string selectedTranscodeVideoCodec = AtcSoft.VideoSurveillance.Factories.DropDownItemsFactory.DefaultTranscodeVideoCodec;

    public IDictionary<string, string> TranscodeVideoCodecItems
        => AtcSoft.VideoSurveillance.Factories.DropDownItemsFactory.TranscodeVideoCodecItems;

    // Thumbnail Settings
    [ObservableProperty]
    private string selectedThumbnailTileCount = DropDownItemsFactory.DefaultThumbnailTileCount.ToString(CultureInfo.InvariantCulture);

    public IDictionary<string, string> ThumbnailTileCountItems
        => DropDownItemsFactory.ThumbnailTileCountItems;

    // Timelapse Settings
    [ObservableProperty(DependentPropertyNames = [nameof(IsTimelapseIntervalEnabled)])]
    private bool enableTimelapse;

    [ObservableProperty]
    private string selectedTimelapseInterval = DropDownItemsFactory.DefaultTimelapseInterval;

    public IDictionary<string, string> TimelapseIntervalItems
        => DropDownItemsFactory.TimelapseIntervalItems;

    /// <summary>
    /// Gets a value indicating whether the timelapse interval dropdown should be enabled.
    /// </summary>
    public bool IsTimelapseIntervalEnabled
        => EnableTimelapse;

    // Media Cleanup Settings
    [ObservableProperty(DependentPropertyNames = [nameof(IsCleanupEnabled), nameof(IsSnapshotRetentionEnabled), nameof(IsDiskSpaceGuardControlEnabled)])]
    private string selectedCleanupSchedule = DropDownItemsFactory.DefaultMediaCleanupSchedule;

    public IDictionary<string, string> CleanupScheduleItems
        => DropDownItemsFactory.MediaCleanupScheduleItems;

    [ObservableProperty]
    private string selectedRecordingRetention = DropDownItemsFactory.DefaultRecordingRetentionDays.ToString(CultureInfo.InvariantCulture);

    public IDictionary<string, string> RecordingRetentionItems
        => DropDownItemsFactory.MediaRetentionPeriodItems;

    [ObservableProperty(DependentPropertyNames = [nameof(IsSnapshotRetentionEnabled)])]
    private bool includeSnapshotsInCleanup;

    [ObservableProperty]
    private string selectedSnapshotRetention = DropDownItemsFactory.DefaultSnapshotRetentionDays.ToString(CultureInfo.InvariantCulture);

    public IDictionary<string, string> SnapshotRetentionItems
        => DropDownItemsFactory.MediaRetentionPeriodItems;

    /// <summary>
    /// Gets a value indicating whether cleanup is enabled (schedule is not Disabled).
    /// </summary>
    public bool IsCleanupEnabled
        => !string.Equals(SelectedCleanupSchedule, "Disabled", StringComparison.Ordinal);

    /// <summary>
    /// Gets a value indicating whether snapshot retention dropdown should be enabled.
    /// </summary>
    public bool IsSnapshotRetentionEnabled
        => IsCleanupEnabled && IncludeSnapshotsInCleanup;

    [ObservableProperty(DependentPropertyNames = [nameof(IsDiskSpaceGuardControlEnabled)])]
    private bool isDiskSpaceGuardEnabled = true;

    [ObservableProperty]
    private string selectedMinFreeSpace = DropDownItemsFactory.DefaultMinFreeSpaceMb;

    public IDictionary<string, string> MinFreeSpaceItems
        => DropDownItemsFactory.MinFreeSpaceItems;

    [ObservableProperty]
    private string selectedMaxRecordingStorage = DropDownItemsFactory.DefaultMaxRecordingStorageGb;

    public IDictionary<string, string> MaxRecordingStorageItems
        => DropDownItemsFactory.MaxRecordingStorageItems;

    /// <summary>
    /// Gets a value indicating whether the minimum free-space dropdown
    /// should be enabled: the disk guard is on. The guard runs whatever the
    /// cleanup schedule is.
    /// </summary>
    public bool IsDiskSpaceGuardControlEnabled
        => IsDiskSpaceGuardEnabled;

    // Recordings Browser Settings
    [ObservableProperty]
    private bool closeRecordingsBrowserOnPlay;

    [ObservableProperty]
    private bool rememberRecordingsBrowserState = true;

    // Playback Overlay Settings
    [ObservableProperty]
    private bool showPlaybackFilename = true;

    [ObservableProperty]
    private string selectedPlaybackFilenameColor = "White";

    [ObservableProperty]
    private bool showPlaybackTimestamp = true;

    [ObservableProperty]
    private string selectedPlaybackTimestampColor = "White";

    #endregion

    #region Advanced Tab Settings

    [ObservableProperty]
    private bool enableDebugLogging;

    [ObservableProperty]
    private DirectoryInfo? logPath;

    #endregion

    #region Commands

    /// <summary>
    /// Saves every tab. When the recording folder layout changed, the
    /// existing recordings are moved to match before the dialog closes.
    /// </summary>
    [RelayCommand("Save")]
    public async Task SaveAsync()
    {
        var layoutBefore = (settingsService.Recording.UseDailySubfolders, settingsService.Recording.SeparateThumbnailFolder);

        SaveGeneralSettings();
        SaveCameraDisplaySettings();
        SaveConnectionSettings();
        SavePerformanceSettings();
        SaveMotionDetectionSettings();
        SaveRecordingSettings();
        SaveAdvancedSettings();

        var layoutAfter = (settingsService.Recording.UseDailySubfolders, settingsService.Recording.SeparateThumbnailFolder);
        if (recordingLayoutSynchronizer is not null && layoutBefore != layoutAfter)
        {
            await ApplyRecordingLayoutAsync().ConfigureAwait(true);
        }

        CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
    }

    private async Task ApplyRecordingLayoutAsync()
    {
        IsApplyingRecordingLayout = true;
        try
        {
            await recordingLayoutSynchronizer!.SynchronizeAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // The settings are saved; files that could not be moved stay
            // readable where they are and are picked up at the next start.
            LogRecordingLayoutMoveFailed(ex);
        }
        finally
        {
            IsApplyingRecordingLayout = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moving recordings to the new folder layout failed; the files left behind are moved at the next start")]
    private partial void LogRecordingLayoutMoveFailed(Exception ex);

    [RelayCommand]
    private void Cancel()
    {
        RestoreOriginalThemeAndLanguage();
        CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: false));
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        // General Tab
        SelectedLanguage = "1033"; // en-US
        ConnectCamerasOnStartup = true;
        StartMaximized = false;
        StartRibbonCollapsed = false;

        // Apply default theme/accent/language immediately
        ThemeManager.Current.ChangeThemeBaseColor(Application.Current, "Dark");
        ThemeManager.Current.ChangeThemeColorScheme(Application.Current, "Blue");
        CultureManager.UiCulture = new CultureInfo(1033);

        // Camera Display Tab
        ShowCameraOverlayTitle = true;
        ShowCameraOverlayDescription = true;
        ShowCameraOverlayTime = false;
        ShowCameraOverlayConnectionStatus = true;
        ShowCameraOverlayQuickActions = true;
        ShowCameraOverlayConnectionTypeIcon = false;
        SelectedConnectionTypeIconPlacement = DropDownItemsFactory.DefaultConnectionTypeIconPlacement;
        SelectedOverlayOpacity = "0.7";
        SelectedOverlayPosition = "TopLeft";
        AllowDragAndDropReorder = true;
        AutoSaveLayoutChanges = true;
        SnapshotPath = new DirectoryInfo(ApplicationPaths.DefaultSnapshotsPath);

        // Connection Tab
        SelectedDefaultProtocol = "Rtsp";
        DefaultPort = 554;
        ConnectionTimeoutSeconds = 10;
        ReconnectDelaySeconds = 10;
        AutoReconnectOnFailure = true;
        ShowNotificationOnDisconnect = true;
        ShowNotificationOnReconnect = false;
        PlayNotificationSound = false;

        // Performance Tab
        SelectedVideoQuality = "Auto";
        HardwareAcceleration = true;
        LowLatencyMode = false;
        BufferDurationMs = 500;
        SelectedRtspTransport = "tcp";
        MaxLatencyMs = 500;

        // Motion Detection Tab
        MotionSensitivity = DropDownItemsFactory.DefaultMotionSensitivity;
        SmallestObjectPercent = (decimal)MotionDetectorOptions.DefaultSmallestObjectPercent;
        StoppedObjectSeconds = MotionDetectorOptions.DefaultStoppedObjectSeconds;
        IgnoreLightingChanges = true;
        PostMotionDurationSeconds = DropDownItemsFactory.DefaultPostMotionDuration;
        AnalysisFrameRate = 15;
        SelectedAnalysisResolution = DropDownItemsFactory.DefaultMotionAnalysisResolution;
        CooldownSeconds = 5;
        ShowBoundingBoxInGrid = false;
        ShowBoundingBoxInFullScreen = false;
        SelectedBoundingBoxColor = DropDownItemsFactory.DefaultBoundingBoxColor;
        SelectedBoundingBoxThickness = DropDownItemsFactory.DefaultBoundingBoxThickness.ToString(CultureInfo.InvariantCulture);
        SelectedBoundingBoxMinArea = DropDownItemsFactory.DefaultBoundingBoxMinArea.ToString(CultureInfo.InvariantCulture);

        // Recording Tab
        RecordingPath = new DirectoryInfo(ApplicationPaths.DefaultRecordingsPath);
        SelectedRecordingFormat = "mp4";
        SelectedTranscodeVideoCodec = AtcSoft.VideoSurveillance.Factories.DropDownItemsFactory.DefaultTranscodeVideoCodec;
        EnableRecordingOnMotion = false;
        EnableRecordingOnConnect = false;
        EnableActivityIndex = true;

        // Recording Storage Layout
        UseDailySubfolders = true;
        SeparateThumbnailFolder = true;

        // Recording Segmentation
        EnableHourlySegmentation = true;
        SelectedMaxRecordingDuration = DropDownItemsFactory.DefaultMaxRecordingDuration.ToString(CultureInfo.InvariantCulture);

        // Thumbnail Settings
        SelectedThumbnailTileCount = DropDownItemsFactory.DefaultThumbnailTileCount.ToString(CultureInfo.InvariantCulture);

        // Timelapse Settings
        EnableTimelapse = false;
        SelectedTimelapseInterval = DropDownItemsFactory.DefaultTimelapseInterval;

        // Media Cleanup
        SelectedCleanupSchedule = DropDownItemsFactory.DefaultMediaCleanupSchedule;
        SelectedRecordingRetention = DropDownItemsFactory.DefaultRecordingRetentionDays.ToString(CultureInfo.InvariantCulture);
        IncludeSnapshotsInCleanup = false;
        SelectedSnapshotRetention = DropDownItemsFactory.DefaultSnapshotRetentionDays.ToString(CultureInfo.InvariantCulture);
        IsDiskSpaceGuardEnabled = true;
        SelectedMinFreeSpace = DropDownItemsFactory.DefaultMinFreeSpaceMb;
        SelectedMaxRecordingStorage = DropDownItemsFactory.DefaultMaxRecordingStorageGb;

        // Recordings Browser
        CloseRecordingsBrowserOnPlay = false;
        RememberRecordingsBrowserState = true;

        // Playback Overlay
        ShowPlaybackFilename = true;
        SelectedPlaybackFilenameColor = "White";
        ShowPlaybackTimestamp = true;
        SelectedPlaybackTimestampColor = "White";

        // Advanced Tab
        EnableDebugLogging = false;
        LogPath = new DirectoryInfo(ApplicationPaths.DefaultLogsPath);
    }

    #endregion

    #region Private Methods

    private void LoadSettings()
    {
        var general = settingsService.General;

        // Store original values for cancel restoration
        originalThemeBase = general.ThemeBase;
        originalThemeAccent = general.ThemeAccent;
        originalLanguage = general.Language;

        // Load General Tab settings
        SelectedLanguage = general.Language;
        ConnectCamerasOnStartup = general.ConnectCamerasOnStartup;
        StartMaximized = general.StartMaximized;
        StartRibbonCollapsed = general.StartRibbonCollapsed;

        // Load Camera Display Tab settings
        var cameraDisplay = settingsService.CameraDisplay;
        ShowCameraOverlayTitle = cameraDisplay.ShowOverlayTitle;
        ShowCameraOverlayDescription = cameraDisplay.ShowOverlayDescription;
        ShowCameraOverlayTime = cameraDisplay.ShowOverlayTime;
        ShowCameraOverlayConnectionStatus = cameraDisplay.ShowOverlayConnectionStatus;
        ShowCameraOverlayQuickActions = cameraDisplay.ShowOverlayQuickActions;
        ShowCameraOverlayConnectionTypeIcon = cameraDisplay.ShowOverlayConnectionTypeIcon;
        SelectedConnectionTypeIconPlacement = cameraDisplay.ConnectionTypeIconPlacement.ToString();
        SelectedOverlayOpacity = cameraDisplay.OverlayOpacity.ToString("F1", CultureInfo.InvariantCulture);
        SelectedOverlayPosition = cameraDisplay.OverlayPosition.ToString();
        AllowDragAndDropReorder = cameraDisplay.AllowDragAndDropReorder;
        AutoSaveLayoutChanges = cameraDisplay.AutoSaveLayoutChanges;
        SnapshotPath = new DirectoryInfo(cameraDisplay.SnapshotPath);

        // Load Connection Tab settings
        var connection = settingsService.Connection;
        SelectedDefaultProtocol = connection.DefaultProtocol.ToString();
        DefaultPort = connection.DefaultPort;
        ConnectionTimeoutSeconds = connection.ConnectionTimeoutSeconds;
        ReconnectDelaySeconds = connection.ReconnectDelaySeconds;
        AutoReconnectOnFailure = connection.AutoReconnectOnFailure;
        ShowNotificationOnDisconnect = connection.ShowNotificationOnDisconnect;
        ShowNotificationOnReconnect = connection.ShowNotificationOnReconnect;
        PlayNotificationSound = connection.PlayNotificationSound;

        // Load Performance Tab settings
        var performance = settingsService.Performance;
        SelectedVideoQuality = performance.VideoQuality;
        HardwareAcceleration = performance.HardwareAcceleration;
        LowLatencyMode = performance.LowLatencyMode;
        BufferDurationMs = performance.BufferDurationMs;
        SelectedRtspTransport = performance.RtspTransport;
        MaxLatencyMs = performance.MaxLatencyMs;

        // Load Motion Detection Tab settings
        var motionDetection = settingsService.MotionDetection;
        MotionSensitivity = motionDetection.Sensitivity;
        SmallestObjectPercent = (decimal)motionDetection.SmallestObjectPercent;
        StoppedObjectSeconds = motionDetection.StoppedObjectSeconds;
        IgnoreLightingChanges = motionDetection.IgnoreLightingChanges;
        PostMotionDurationSeconds = motionDetection.PostMotionDurationSeconds;
        AnalysisFrameRate = motionDetection.AnalysisFrameRate;
        SelectedAnalysisResolution = DropDownItemsFactory.FormatAnalysisResolution(motionDetection.AnalysisWidth);
        CooldownSeconds = motionDetection.CooldownSeconds;
        ShowBoundingBoxInGrid = motionDetection.BoundingBox.ShowInGrid;
        ShowBoundingBoxInFullScreen = motionDetection.BoundingBox.ShowInFullScreen;
        SelectedBoundingBoxColor = motionDetection.BoundingBox.Color;
        SelectedBoundingBoxThickness = motionDetection.BoundingBox.Thickness.ToString(CultureInfo.InvariantCulture);
        SelectedBoundingBoxMinArea = motionDetection.BoundingBox.MinArea.ToString(CultureInfo.InvariantCulture);

        // Load Recording Tab settings
        var recording = settingsService.Recording;
        RecordingPath = new DirectoryInfo(recording.RecordingPath);
        SelectedRecordingFormat = recording.RecordingFormat;
        SelectedTranscodeVideoCodec = recording.TranscodeVideoCodec.ToString();
        EnableRecordingOnMotion = recording.EnableRecordingOnMotion;
        EnableRecordingOnConnect = recording.EnableRecordingOnConnect;
        EnableActivityIndex = recording.EnableActivityIndex;

        // Recording Storage Layout Settings
        UseDailySubfolders = recording.UseDailySubfolders;
        SeparateThumbnailFolder = recording.SeparateThumbnailFolder;

        // Recording Segmentation Settings
        EnableHourlySegmentation = recording.EnableHourlySegmentation;
        SelectedMaxRecordingDuration = recording.MaxRecordingDurationMinutes.ToString(CultureInfo.InvariantCulture);

        // Thumbnail Settings
        SelectedThumbnailTileCount = recording.ThumbnailTileCount.ToString(CultureInfo.InvariantCulture);

        // Timelapse Settings
        EnableTimelapse = recording.EnableTimelapse;
        SelectedTimelapseInterval = recording.TimelapseInterval;

        // Media Cleanup Settings
        SelectedCleanupSchedule = recording.Cleanup.Schedule.ToString();
        SelectedRecordingRetention = recording.Cleanup.RecordingRetentionDays.ToString(CultureInfo.InvariantCulture);
        IncludeSnapshotsInCleanup = recording.Cleanup.IncludeSnapshots;
        SelectedSnapshotRetention = recording.Cleanup.SnapshotRetentionDays.ToString(CultureInfo.InvariantCulture);
        IsDiskSpaceGuardEnabled = recording.Cleanup.EnableDiskSpaceGuard;
        SelectedMinFreeSpace = recording.Cleanup.MinFreeSpaceMb.ToString(CultureInfo.InvariantCulture);
        SelectedMaxRecordingStorage = recording.Cleanup.MaxRecordingStorageGb.ToString(CultureInfo.InvariantCulture);

        // Recordings Browser Settings
        CloseRecordingsBrowserOnPlay = recording.CloseRecordingsBrowserOnPlay;
        RememberRecordingsBrowserState = recording.RememberRecordingsBrowserState;

        // Playback Overlay Settings
        ShowPlaybackFilename = recording.PlaybackOverlay.ShowFilename;
        SelectedPlaybackFilenameColor = recording.PlaybackOverlay.FilenameColor;
        ShowPlaybackTimestamp = recording.PlaybackOverlay.ShowTimestamp;
        SelectedPlaybackTimestampColor = recording.PlaybackOverlay.TimestampColor;

        // Load Advanced Tab settings
        var advanced = settingsService.Advanced;
        EnableDebugLogging = advanced.EnableDebugLogging;
        LogPath = new DirectoryInfo(advanced.LogPath);
    }

    private void SaveGeneralSettings()
    {
        // Get current theme/accent from ThemeManager (since selectors update it directly)
        // Without a running application (no theme to detect) the loaded values stand.
        var currentTheme = Application.Current is null
            ? null
            : ThemeManager.Current.DetectTheme(Application.Current);
        var themeBase = currentTheme?.BaseColorScheme ?? originalThemeBase;
        var themeAccent = currentTheme?.ColorScheme ?? originalThemeAccent;

        // Get language from CultureManager (since LabelLanguageSelector updates it directly via UpdateUiCultureOnChangeEvent)
        var language = CultureManager.UiCulture.LCID.ToString(CultureInfo.InvariantCulture);

        var settings = new GeneralSettings
        {
            ThemeBase = themeBase,
            ThemeAccent = themeAccent,
            Language = language,
            ConnectCamerasOnStartup = ConnectCamerasOnStartup,
            StartMaximized = StartMaximized,
            StartRibbonCollapsed = StartRibbonCollapsed,

            // Chosen in the main window, not here: kept as it is.
            GridSize = settingsService.General.GridSize,
        };

        settingsService.SaveGeneral(settings);
    }

    private void SaveCameraDisplaySettings()
    {
        _ = double.TryParse(SelectedOverlayOpacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity);
        _ = Enum.TryParse<OverlayPosition>(SelectedOverlayPosition, out var overlayPosition);
        _ = Enum.TryParse<ConnectionTypeIconPlacement>(SelectedConnectionTypeIconPlacement, out var iconPlacement);

        var settings = new CameraDisplayAppSettings
        {
            ShowOverlayTitle = ShowCameraOverlayTitle,
            ShowOverlayDescription = ShowCameraOverlayDescription,
            ShowOverlayTime = ShowCameraOverlayTime,
            ShowOverlayConnectionStatus = ShowCameraOverlayConnectionStatus,
            ShowOverlayQuickActions = ShowCameraOverlayQuickActions,
            ShowOverlayConnectionTypeIcon = ShowCameraOverlayConnectionTypeIcon,
            ConnectionTypeIconPlacement = iconPlacement,
            OverlayOpacity = opacity,
            OverlayPosition = overlayPosition,
            AllowDragAndDropReorder = AllowDragAndDropReorder,
            AutoSaveLayoutChanges = AutoSaveLayoutChanges,
            SnapshotPath = SnapshotPath?.FullName ?? ApplicationPaths.DefaultSnapshotsPath,
        };

        settingsService.SaveCameraDisplay(settings);
    }

    private void SaveConnectionSettings()
    {
        _ = Enum.TryParse<CameraProtocol>(SelectedDefaultProtocol, out var protocol);

        var settings = new ConnectionAppSettings
        {
            DefaultProtocol = protocol,
            DefaultPort = DefaultPort,
            ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
            ReconnectDelaySeconds = ReconnectDelaySeconds,
            AutoReconnectOnFailure = AutoReconnectOnFailure,
            ShowNotificationOnDisconnect = ShowNotificationOnDisconnect,
            ShowNotificationOnReconnect = ShowNotificationOnReconnect,
            PlayNotificationSound = PlayNotificationSound,
        };

        settingsService.SaveConnection(settings);
    }

    private void SavePerformanceSettings()
    {
        var settings = new PerformanceSettings
        {
            VideoQuality = SelectedVideoQuality,
            HardwareAcceleration = HardwareAcceleration,
            LowLatencyMode = LowLatencyMode,
            BufferDurationMs = BufferDurationMs,
            RtspTransport = SelectedRtspTransport,
            MaxLatencyMs = MaxLatencyMs,
        };

        settingsService.SavePerformance(settings);
    }

    private void SaveMotionDetectionSettings()
    {
        var analysisWidth = DropDownItemsFactory.ParseAnalysisResolution(SelectedAnalysisResolution);

        // What the dialog does not show is kept as it was.
        var current = settingsService.MotionDetection;
        var settings = new MotionDetectionSettings
        {
            Sensitivity = MotionSensitivity,
            SmallestObjectPercent = (double)SmallestObjectPercent,
            StoppedObjectSeconds = StoppedObjectSeconds,
            IgnoreLightingChanges = IgnoreLightingChanges,
            MinimumChangePercent = current.MinimumChangePercent,
            PostMotionDurationSeconds = PostMotionDurationSeconds,
            AnalysisFrameRate = AnalysisFrameRate,
            AnalysisWidth = analysisWidth,
            AnalysisHeight = current.AnalysisHeight,
            CooldownSeconds = CooldownSeconds,
            BoundingBox = new BoundingBoxSettings
            {
                ShowInGrid = ShowBoundingBoxInGrid,
                ShowInFullScreen = ShowBoundingBoxInFullScreen,
                Color = SelectedBoundingBoxColor,
                Thickness = int.TryParse(SelectedBoundingBoxThickness, NumberStyles.Integer, CultureInfo.InvariantCulture, out var thickness) ? thickness : DropDownItemsFactory.DefaultBoundingBoxThickness,
                MinArea = int.TryParse(SelectedBoundingBoxMinArea, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minArea) ? minArea : DropDownItemsFactory.DefaultBoundingBoxMinArea,
                Padding = current.BoundingBox.Padding,
                Smoothing = current.BoundingBox.Smoothing,
            },
        };

        settingsService.SaveMotionDetection(settings);
    }

    private void SaveRecordingSettings()
    {
        _ = Enum.TryParse<MediaCleanupSchedule>(SelectedCleanupSchedule, out var cleanupSchedule);
        _ = int.TryParse(SelectedRecordingRetention, NumberStyles.Integer, CultureInfo.InvariantCulture, out var recordingRetention);
        _ = int.TryParse(SelectedSnapshotRetention, NumberStyles.Integer, CultureInfo.InvariantCulture, out var snapshotRetention);
        _ = int.TryParse(SelectedMaxRecordingDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxRecordingDuration);
        _ = int.TryParse(SelectedThumbnailTileCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var thumbnailTileCount);

        var settings = new RecordingSettings
        {
            RecordingPath = RecordingPath?.FullName ?? ApplicationPaths.DefaultRecordingsPath,
            RecordingFormat = SelectedRecordingFormat,
            TranscodeVideoCodec = Enum.TryParse<VideoTranscodeCodec>(SelectedTranscodeVideoCodec, ignoreCase: true, out var transcodeCodec)
                ? transcodeCodec
                : VideoTranscodeCodec.None,
            EnableRecordingOnMotion = EnableRecordingOnMotion,
            EnableRecordingOnConnect = EnableRecordingOnConnect,
            EnableActivityIndex = EnableActivityIndex,
            EnableHourlySegmentation = EnableHourlySegmentation,
            UseDailySubfolders = UseDailySubfolders,
            SeparateThumbnailFolder = SeparateThumbnailFolder,
            CloseRecordingsBrowserOnPlay = CloseRecordingsBrowserOnPlay,
            RememberRecordingsBrowserState = RememberRecordingsBrowserState,
            MaxRecordingDurationMinutes = maxRecordingDuration > 0 ? maxRecordingDuration : DropDownItemsFactory.DefaultMaxRecordingDuration,
            ThumbnailTileCount = thumbnailTileCount == 1 || thumbnailTileCount == 4 ? thumbnailTileCount : DropDownItemsFactory.DefaultThumbnailTileCount,
            EnableTimelapse = EnableTimelapse,
            TimelapseInterval = SelectedTimelapseInterval,
            Cleanup = new MediaCleanupSettings
            {
                Schedule = cleanupSchedule,
                RecordingRetentionDays = recordingRetention > 0 ? recordingRetention : DropDownItemsFactory.DefaultRecordingRetentionDays,
                IncludeSnapshots = IncludeSnapshotsInCleanup,
                SnapshotRetentionDays = snapshotRetention > 0 ? snapshotRetention : DropDownItemsFactory.DefaultSnapshotRetentionDays,
                EnableDiskSpaceGuard = IsDiskSpaceGuardEnabled,
                MinFreeSpaceMb = int.TryParse(SelectedMinFreeSpace, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minFreeSpaceMb) && minFreeSpaceMb > 0
                    ? minFreeSpaceMb
                    : int.Parse(DropDownItemsFactory.DefaultMinFreeSpaceMb, CultureInfo.InvariantCulture),
                MaxRecordingStorageGb = int.TryParse(SelectedMaxRecordingStorage, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxStorageGb) && maxStorageGb > 0
                    ? maxStorageGb
                    : 0,
            },
            PlaybackOverlay = new PlaybackOverlaySettings
            {
                ShowFilename = ShowPlaybackFilename,
                FilenameColor = SelectedPlaybackFilenameColor,
                ShowTimestamp = ShowPlaybackTimestamp,
                TimestampColor = SelectedPlaybackTimestampColor,
            },
        };

        settingsService.SaveRecording(settings);
    }

    private void SaveAdvancedSettings()
    {
        var settings = new AdvancedSettings
        {
            EnableDebugLogging = EnableDebugLogging,
            LogPath = LogPath?.FullName ?? ApplicationPaths.DefaultLogsPath,
        };

        settingsService.SaveAdvanced(settings);
    }

    private void RestoreOriginalThemeAndLanguage()
    {
        // Restore theme
        ThemeManager.Current.ChangeThemeBaseColor(Application.Current, originalThemeBase);
        ThemeManager.Current.ChangeThemeColorScheme(Application.Current, originalThemeAccent);

        // Restore language
        if (int.TryParse(originalLanguage, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lcid))
        {
            CultureManager.UiCulture = new CultureInfo(lcid);
        }
    }

    #endregion
}