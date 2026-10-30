namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// View model for the settings dialog.
/// Loads/saves settings via REST API (flat AppSettings record).
/// General settings are stored locally via IApplicationSettingsService.
/// </summary>
[SuppressMessage("", "SA1124: Do not use regions", Justification = "OK")]
[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public partial class SettingsDialogViewModel : ViewModelBase
{
    private readonly GatewayService gatewayService;
    private readonly AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService settingsService;

    // Snapshot of full API settings for preserving server-only fields on save
    private AppSettings? originalApiSettings;

    // Original theme/language values for restoration on cancel
    private string originalThemeBase = "Dark";
    private string originalThemeAccent = "Blue";
    private string originalLanguage = "1033";

    public SettingsDialogViewModel(
        GatewayService gatewayService,
        AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService settingsService)
    {
        this.gatewayService = gatewayService ?? throw new ArgumentNullException(nameof(gatewayService));
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    #region General Tab

    [ObservableProperty]
    private string selectedLanguage = "1033";

    [ObservableProperty]
    private bool connectCamerasOnStartup = true;

    [ObservableProperty]
    private bool startMaximized;

    [ObservableProperty]
    private bool startRibbonCollapsed;

    #endregion

    #region Camera Display Tab

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
            Enum.TryParse<AtcSoft.VideoSurveillance.Enums.OverlayPosition>(SelectedOverlayPosition, out var position)
                ? position
                : AtcSoft.VideoSurveillance.Enums.OverlayPosition.TopLeft);

    [ObservableProperty]
    private bool allowDragAndDropReorder = true;

    [ObservableProperty]
    private bool autoSaveLayoutChanges = true;

    [ObservableProperty]
    private DirectoryInfo? snapshotPath;

    #endregion

    #region Connection Tab

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

    #region Performance Tab

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

    #region Motion Detection Tab

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

    #endregion

    #region Capture Tab

    [ObservableProperty]
    private DirectoryInfo? recordingPath;

    [ObservableProperty]
    private string selectedRecordingFormat = DropDownItemsFactory.DefaultRecordingFormat;

    public IDictionary<string, string> RecordingFormatItems
        => DropDownItemsFactory.RecordingFormatItems;

    // Whether recordings are made as H.264 whatever the camera sends.
    [ObservableProperty]
    private string selectedTranscodeVideoCodec = AtcSoft.VideoSurveillance.Factories.DropDownItemsFactory.DefaultTranscodeVideoCodec;

    public IDictionary<string, string> TranscodeVideoCodecItems
        => AtcSoft.VideoSurveillance.Factories.DropDownItemsFactory.TranscodeVideoCodecItems;

    [ObservableProperty]
    private bool enableRecordingOnMotion;

    [ObservableProperty]
    private bool enableRecordingOnConnect;

    [ObservableProperty]
    private bool enableActivityIndex = true;

    // Storage layout
    [ObservableProperty(DependentPropertyNames = [nameof(RecordingLayoutExampleSummary), nameof(SeparateThumbnailFolderHint)])]
    private bool useDailySubfolders = true;

    [ObservableProperty(DependentPropertyNames = [nameof(RecordingLayoutExampleSummary), nameof(UseDailySubfoldersHint)])]
    private bool separateThumbnailFolder = true;

    /// <summary>
    /// Gets the example shown under the two layout switches. The server
    /// moves its recordings when the settings are saved.
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

    // Segmentation
    [ObservableProperty]
    private bool enableHourlySegmentation = true;

    [ObservableProperty]
    private string selectedMaxRecordingDuration = DropDownItemsFactory.DefaultMaxRecordingDuration.ToString(CultureInfo.InvariantCulture);

    public IDictionary<string, string> MaxRecordingDurationItems
        => DropDownItemsFactory.MaxRecordingDurationItems;

    // Thumbnails
    [ObservableProperty]
    private string selectedThumbnailTileCount = DropDownItemsFactory.DefaultThumbnailTileCount.ToString(CultureInfo.InvariantCulture);

    public IDictionary<string, string> ThumbnailTileCountItems
        => DropDownItemsFactory.ThumbnailTileCountItems;

    #endregion

    #region Timelapse Tab

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

    #endregion

    #region Storage Tab

    // Media Cleanup
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
    /// Gets a value indicating whether the minimum free space dropdown
    /// should be enabled: the disk guard is on. The guard runs whatever the
    /// cleanup schedule is.
    /// </summary>
    public bool IsDiskSpaceGuardControlEnabled
        => IsDiskSpaceGuardEnabled;

    // Playback Overlay
    [ObservableProperty]
    private bool showPlaybackFilename = true;

    [ObservableProperty]
    private string selectedPlaybackFilenameColor = "White";

    [ObservableProperty]
    private bool showPlaybackTimestamp = true;

    [ObservableProperty]
    private string selectedPlaybackTimestampColor = "White";

    #endregion

    #region Advanced Tab

    [ObservableProperty]
    private bool enableDebugLogging;

    [ObservableProperty]
    private DirectoryInfo? logPath;

    #endregion

    #region Load / Save

    /// <summary>
    /// Loads settings from the local service (General) and API server (everything else).
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        // Load general settings from local service
        var general = settingsService.General;
        originalThemeBase = general.ThemeBase;
        originalThemeAccent = general.ThemeAccent;
        originalLanguage = general.Language;
        SelectedLanguage = general.Language;
        ConnectCamerasOnStartup = general.ConnectCamerasOnStartup;
        StartMaximized = general.StartMaximized;
        StartRibbonCollapsed = general.StartRibbonCollapsed;

        try
        {
            var settings = await gatewayService
                .GetSettingsAsync()
                .ConfigureAwait(true);

            if (settings is null)
            {
                return;
            }

            originalApiSettings = settings;

            // Camera Display
            ShowCameraOverlayTitle = settings.ShowOverlayTitle;
            ShowCameraOverlayDescription = settings.ShowOverlayDescription;
            ShowCameraOverlayTime = settings.ShowOverlayTime;
            ShowCameraOverlayConnectionStatus = settings.ShowOverlayConnectionStatus;
            ShowCameraOverlayQuickActions = settings.ShowOverlayQuickActions;
            ShowCameraOverlayConnectionTypeIcon = settings.ShowOverlayConnectionTypeIcon;
            SelectedConnectionTypeIconPlacement = DropDownKey.For(settings.ConnectionTypeIconPlacement.ToWireValue(), ConnectionTypeIconPlacementItems, DropDownItemsFactory.DefaultConnectionTypeIconPlacement);
            SelectedOverlayOpacity = settings.OverlayOpacity.ToString("F1", CultureInfo.InvariantCulture);
            SelectedOverlayPosition = DropDownKey.For(settings.OverlayPosition.ToWireValue(), OverlayPositionItems, DropDownItemsFactory.DefaultOverlayPosition);
            AllowDragAndDropReorder = settings.AllowDragAndDropReorder;
            AutoSaveLayoutChanges = settings.AutoSaveLayoutChanges;
            SnapshotPath = !string.IsNullOrEmpty(settings.SnapshotPath) ? new DirectoryInfo(settings.SnapshotPath) : null;

            // Connection
            SelectedDefaultProtocol = DropDownKey.For(settings.DefaultProtocol.ToWireValue(), DefaultProtocolItems, DropDownItemsFactory.DefaultProtocol);
            DefaultPort = settings.DefaultPort;
            ConnectionTimeoutSeconds = settings.ConnectionTimeoutSeconds;
            ReconnectDelaySeconds = settings.ReconnectDelaySeconds;
            AutoReconnectOnFailure = settings.AutoReconnectOnFailure;
            ShowNotificationOnDisconnect = settings.ShowNotificationOnDisconnect;
            ShowNotificationOnReconnect = settings.ShowNotificationOnReconnect;
            PlayNotificationSound = settings.PlayNotificationSound;

            // Performance
            SelectedVideoQuality = AtcSoft.VideoSurveillance.Helpers.VideoQualityHelper.Normalize(settings.VideoQuality) ?? DropDownItemsFactory.DefaultVideoQuality;
            HardwareAcceleration = settings.HardwareAcceleration;
            LowLatencyMode = settings.LowLatencyMode;
            BufferDurationMs = settings.BufferDurationMs;
            SelectedRtspTransport = DropDownKey.For(settings.RtspTransport.ToWireValue(), RtspTransportItems, DropDownItemsFactory.DefaultRtspTransport);
            MaxLatencyMs = settings.MaxLatencyMs;

            // Motion Detection
            MotionSensitivity = settings.MotionSensitivity;
            SmallestObjectPercent = (decimal)settings.SmallestObjectPercent;
            StoppedObjectSeconds = settings.StoppedObjectSeconds;
            IgnoreLightingChanges = settings.IgnoreLightingChanges;
            PostMotionDurationSeconds = settings.PostMotionDurationSeconds;
            AnalysisFrameRate = settings.AnalysisFrameRate;
            SelectedAnalysisResolution = DropDownItemsFactory.FormatAnalysisResolution(settings.AnalysisWidth);
            CooldownSeconds = settings.CooldownSeconds;
            ShowBoundingBoxInGrid = settings.BoundingBoxShowInGrid;
            ShowBoundingBoxInFullScreen = settings.BoundingBoxShowInFullScreen;
            SelectedBoundingBoxColor = settings.BoundingBoxColor ?? DropDownItemsFactory.DefaultBoundingBoxColor;
            SelectedBoundingBoxThickness = settings.BoundingBoxThickness.ToString(CultureInfo.InvariantCulture);

            // Capture
            RecordingPath = !string.IsNullOrEmpty(settings.RecordingPath) ? new DirectoryInfo(settings.RecordingPath) : null;
            SelectedRecordingFormat = DropDownKey.For(settings.RecordingFormat.ToWireValue(), RecordingFormatItems, DropDownItemsFactory.DefaultRecordingFormat);
            SelectedTranscodeVideoCodec = settings.RecordAsH264 ? "H264" : "None";
            EnableRecordingOnMotion = settings.EnableRecordingOnMotion;
            EnableRecordingOnConnect = settings.EnableRecordingOnConnect;
            EnableActivityIndex = settings.EnableActivityIndex;
            EnableHourlySegmentation = settings.EnableHourlySegmentation;
            UseDailySubfolders = settings.UseDailySubfolders;
            SeparateThumbnailFolder = settings.SeparateThumbnailFolder;
            SelectedMaxRecordingDuration = settings.MaxRecordingDurationMinutes.ToString(CultureInfo.InvariantCulture);
            SelectedThumbnailTileCount = settings.ThumbnailTileCount.ToString(CultureInfo.InvariantCulture);

            // Timelapse
            EnableTimelapse = settings.EnableTimelapse;
            SelectedTimelapseInterval = settings.TimelapseInterval ?? DropDownItemsFactory.DefaultTimelapseInterval;

            // Storage
            SelectedCleanupSchedule = DropDownKey.For(settings.CleanupSchedule.ToWireValue(), CleanupScheduleItems, DropDownItemsFactory.DefaultMediaCleanupSchedule);
            SelectedRecordingRetention = settings.RecordingRetentionDays.ToString(CultureInfo.InvariantCulture);
            IncludeSnapshotsInCleanup = settings.CleanupIncludeSnapshots;
            SelectedSnapshotRetention = settings.SnapshotRetentionDays.ToString(CultureInfo.InvariantCulture);
            IsDiskSpaceGuardEnabled = settings.EnableDiskSpaceGuard;
            SelectedMinFreeSpace = settings.MinFreeSpaceMb > 0
                ? settings.MinFreeSpaceMb.ToString(CultureInfo.InvariantCulture)
                : DropDownItemsFactory.DefaultMinFreeSpaceMb;
            SelectedMaxRecordingStorage = settings.MaxRecordingStorageGb.ToString(CultureInfo.InvariantCulture);
            ShowPlaybackFilename = settings.PlaybackShowFilename;
            SelectedPlaybackFilenameColor = settings.PlaybackFilenameColor ?? "White";
            ShowPlaybackTimestamp = settings.PlaybackShowTimestamp;
            SelectedPlaybackTimestampColor = settings.PlaybackTimestampColor ?? "White";

            // Advanced
            EnableDebugLogging = settings.EnableDebugLogging;
            LogPath = !string.IsNullOrEmpty(settings.LogPath) ? new DirectoryInfo(settings.LogPath) : null;
        }
        catch
        {
            // Failed to load settings - use defaults
        }
    }

    #endregion

    #region Commands

    [RelayCommand("Save")]
    private async Task SaveAsync()
    {
        try
        {
            // Save general settings locally
            SaveGeneralSettings();

            // Save remaining settings to API
            var settings = BuildApiSettings();
            await gatewayService
                .UpdateSettingsAsync(settings)
                .ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            // The server refused the settings (a value out of range): say why,
            // and keep the dialog open so the value can be corrected.
            UserDialog.ShowError(string.Format(
                CultureInfo.CurrentCulture,
                Translations.FailedToSaveSettings1,
                ex.Message));
            return;
        }
        catch
        {
            // Save failed - could show error but settings dialog will close
        }

        CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
    }

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
        SelectedLanguage = "1033";
        ConnectCamerasOnStartup = true;
        StartMaximized = false;
        StartRibbonCollapsed = false;

        ThemeManager.Current.ChangeThemeBaseColor(Application.Current, "Dark");
        ThemeManager.Current.ChangeThemeColorScheme(Application.Current, "Blue");
        CultureManager.UiCulture = new CultureInfo(1033);

        // Camera Display Tab
        ShowCameraOverlayTitle = true;
        ShowCameraOverlayDescription = true;
        ShowCameraOverlayTime = false;
        ShowCameraOverlayConnectionStatus = true;
        ShowCameraOverlayQuickActions = true;
        SelectedOverlayOpacity = "0.7";
        SelectedOverlayPosition = "TopLeft";
        ShowCameraOverlayConnectionTypeIcon = false;
        SelectedConnectionTypeIconPlacement = DropDownItemsFactory.DefaultConnectionTypeIconPlacement;
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

        // Capture Tab
        RecordingPath = new DirectoryInfo(ApplicationPaths.DefaultRecordingsPath);
        SelectedRecordingFormat = "mp4";
        SelectedTranscodeVideoCodec = "None";
        EnableRecordingOnMotion = false;
        EnableRecordingOnConnect = false;
        EnableActivityIndex = true;
        EnableHourlySegmentation = true;
        UseDailySubfolders = true;
        SeparateThumbnailFolder = true;
        SelectedMaxRecordingDuration = DropDownItemsFactory.DefaultMaxRecordingDuration.ToString(CultureInfo.InvariantCulture);
        SelectedThumbnailTileCount = DropDownItemsFactory.DefaultThumbnailTileCount.ToString(CultureInfo.InvariantCulture);

        // Timelapse Tab
        EnableTimelapse = false;
        SelectedTimelapseInterval = DropDownItemsFactory.DefaultTimelapseInterval;

        // Storage Tab
        SelectedCleanupSchedule = DropDownItemsFactory.DefaultMediaCleanupSchedule;
        SelectedRecordingRetention = DropDownItemsFactory.DefaultRecordingRetentionDays.ToString(CultureInfo.InvariantCulture);
        IncludeSnapshotsInCleanup = false;
        SelectedSnapshotRetention = DropDownItemsFactory.DefaultSnapshotRetentionDays.ToString(CultureInfo.InvariantCulture);
        IsDiskSpaceGuardEnabled = true;
        SelectedMinFreeSpace = DropDownItemsFactory.DefaultMinFreeSpaceMb;
        SelectedMaxRecordingStorage = DropDownItemsFactory.DefaultMaxRecordingStorageGb;
        ShowPlaybackFilename = true;
        SelectedPlaybackFilenameColor = "White";
        ShowPlaybackTimestamp = true;
        SelectedPlaybackTimestampColor = "White";

        // Advanced Tab
        EnableDebugLogging = false;
        LogPath = new DirectoryInfo(ApplicationPaths.DefaultLogsPath);
    }

    #endregion

    #region General Settings

    private void SaveGeneralSettings()
    {
        var currentTheme = ThemeManager.Current.DetectTheme(Application.Current);
        var themeBase = currentTheme?.BaseColorScheme ?? "Dark";
        var themeAccent = currentTheme?.ColorScheme ?? "Blue";
        var language = CultureManager.UiCulture.LCID.ToString(CultureInfo.InvariantCulture);

        settingsService.SaveGeneral(new GeneralSettings
        {
            ThemeBase = themeBase,
            ThemeAccent = themeAccent,
            Language = language,
            ConnectCamerasOnStartup = ConnectCamerasOnStartup,
            StartMaximized = StartMaximized,
            StartRibbonCollapsed = StartRibbonCollapsed,

            // Chosen in the live view, not here: kept as it is.
            GridSize = settingsService.General.GridSize,
        });
    }

    private void RestoreOriginalThemeAndLanguage()
    {
        try
        {
            ThemeManager.Current.ChangeThemeBaseColor(Application.Current, originalThemeBase);
            ThemeManager.Current.ChangeThemeColorScheme(Application.Current, originalThemeAccent);

            if (int.TryParse(originalLanguage, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lcid))
            {
                CultureManager.UiCulture = new CultureInfo(lcid);
            }
        }
        catch
        {
            // Restore failed - ignore
        }
    }

    #endregion

    #region Private Helpers

    private UpdateSettingsRequest BuildApiSettings()
    {
        // Get current theme/accent from live state (Atc controls update these directly).
        // The language is not sent: it belongs to this client (client-prefs.json).
        var currentTheme = ThemeManager.Current.DetectTheme(Application.Current);
        var apiThemeBase = currentTheme?.BaseColorScheme ?? "Dark";
        var apiThemeAccent = currentTheme?.ColorScheme ?? "Blue";

        // Parse enum values from string selections
        var themeBase = KnownValue.FromName(AppSettingsThemeBase.KnownValues, apiThemeBase, AppSettingsThemeBase.Dark);
        var overlayPosition = KnownValue.FromName(AppSettingsOverlayPosition.KnownValues, SelectedOverlayPosition, AppSettingsOverlayPosition.TopLeft);
        var iconPlacement = KnownValue.FromName(AppSettingsConnectionTypeIconPlacement.KnownValues, SelectedConnectionTypeIconPlacement, AppSettingsConnectionTypeIconPlacement.SameRow);
        var protocol = KnownValue.FromName(AppSettingsDefaultProtocol.KnownValues, SelectedDefaultProtocol, AppSettingsDefaultProtocol.Rtsp);
        var rtspTransport = KnownValue.FromName(AppSettingsRtspTransport.KnownValues, SelectedRtspTransport, AppSettingsRtspTransport.Tcp);
        var recordingFormat = KnownValue.FromName(AppSettingsRecordingFormat.KnownValues, SelectedRecordingFormat, AppSettingsRecordingFormat.Mp4);
        var cleanupSchedule = KnownValue.FromName(AppSettingsCleanupSchedule.KnownValues, SelectedCleanupSchedule, AppSettingsCleanupSchedule.Disabled);
        _ = double.TryParse(SelectedOverlayOpacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity);
        _ = int.TryParse(SelectedMaxRecordingDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxRecordingDuration);
        _ = int.TryParse(SelectedThumbnailTileCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var thumbnailTileCount);
        _ = int.TryParse(SelectedRecordingRetention, NumberStyles.Integer, CultureInfo.InvariantCulture, out var recordingRetention);
        _ = int.TryParse(SelectedSnapshotRetention, NumberStyles.Integer, CultureInfo.InvariantCulture, out var snapshotRetention);
        if (!int.TryParse(SelectedMinFreeSpace, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minFreeSpace) || minFreeSpace <= 0)
        {
            minFreeSpace = int.Parse(DropDownItemsFactory.DefaultMinFreeSpaceMb, CultureInfo.InvariantCulture);
        }

        _ = int.TryParse(SelectedMaxRecordingStorage, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxRecordingStorage);

        _ = int.TryParse(SelectedBoundingBoxThickness, NumberStyles.Integer, CultureInfo.InvariantCulture, out var boundingBoxThickness);

        var analysisWidth = DropDownItemsFactory.ParseAnalysisResolution(SelectedAnalysisResolution);

        return new UpdateSettingsRequest(
            ThemeBase: themeBase,
            ThemeAccent: apiThemeAccent,
            ConnectOnStartup: ConnectCamerasOnStartup,
            StartMaximized: StartMaximized,
            ShowOverlayTitle: ShowCameraOverlayTitle,
            ShowOverlayDescription: ShowCameraOverlayDescription,
            ShowOverlayTime: ShowCameraOverlayTime,
            ShowOverlayConnectionStatus: ShowCameraOverlayConnectionStatus,
            ShowOverlayQuickActions: ShowCameraOverlayQuickActions,
            ShowOverlayConnectionTypeIcon: ShowCameraOverlayConnectionTypeIcon,
            ConnectionTypeIconPlacement: iconPlacement,
            OverlayOpacity: opacity,
            OverlayPosition: overlayPosition,
            AllowDragAndDropReorder: AllowDragAndDropReorder,
            AutoSaveLayoutChanges: AutoSaveLayoutChanges,
            SnapshotPath: SnapshotPath?.FullName ?? string.Empty,
            DefaultProtocol: protocol,
            DefaultPort: DefaultPort,
            ConnectionTimeoutSeconds: ConnectionTimeoutSeconds,
            ReconnectDelaySeconds: ReconnectDelaySeconds,
            AutoReconnectOnFailure: AutoReconnectOnFailure,
            ShowNotificationOnDisconnect: ShowNotificationOnDisconnect,
            ShowNotificationOnReconnect: ShowNotificationOnReconnect,
            PlayNotificationSound: PlayNotificationSound,
            VideoQuality: SelectedVideoQuality,
            HardwareAcceleration: HardwareAcceleration,
            LowLatencyMode: LowLatencyMode,
            BufferDurationMs: BufferDurationMs,
            RtspTransport: rtspTransport,
            MaxLatencyMs: MaxLatencyMs,
            MotionSensitivity: MotionSensitivity,
            SmallestObjectPercent: (double)SmallestObjectPercent,
            StoppedObjectSeconds: StoppedObjectSeconds,
            IgnoreLightingChanges: IgnoreLightingChanges,
            AnalysisFrameRate: AnalysisFrameRate,
            AnalysisWidth: analysisWidth,
            PostMotionDurationSeconds: PostMotionDurationSeconds,
            CooldownSeconds: CooldownSeconds,
            BoundingBoxShowInGrid: ShowBoundingBoxInGrid,
            BoundingBoxShowInFullScreen: ShowBoundingBoxInFullScreen,
            BoundingBoxColor: SelectedBoundingBoxColor,
            BoundingBoxThickness: boundingBoxThickness,
            BoundingBoxPadding: originalApiSettings?.BoundingBoxPadding,
            BoundingBoxSmoothing: originalApiSettings?.BoundingBoxSmoothing,
            RecordingPath: RecordingPath?.FullName ?? string.Empty,
            RecordingFormat: recordingFormat,
            RecordAsH264: string.Equals(SelectedTranscodeVideoCodec, "H264", StringComparison.OrdinalIgnoreCase),
            EnableRecordingOnMotion: EnableRecordingOnMotion,
            EnableRecordingOnConnect: EnableRecordingOnConnect,
            EnableActivityIndex: EnableActivityIndex,
            EnableHourlySegmentation: EnableHourlySegmentation,
            UseDailySubfolders: UseDailySubfolders,
            SeparateThumbnailFolder: SeparateThumbnailFolder,
            MaxRecordingDurationMinutes: maxRecordingDuration > 0 ? maxRecordingDuration : DropDownItemsFactory.DefaultMaxRecordingDuration,
            ThumbnailTileCount: thumbnailTileCount == 1 || thumbnailTileCount == 4 ? thumbnailTileCount : DropDownItemsFactory.DefaultThumbnailTileCount,
            EnableTimelapse: EnableTimelapse,
            TimelapseInterval: SelectedTimelapseInterval,
            CleanupSchedule: cleanupSchedule,
            RecordingRetentionDays: recordingRetention > 0 ? recordingRetention : DropDownItemsFactory.DefaultRecordingRetentionDays,
            CleanupIncludeSnapshots: IncludeSnapshotsInCleanup,
            SnapshotRetentionDays: snapshotRetention > 0 ? snapshotRetention : DropDownItemsFactory.DefaultSnapshotRetentionDays,
            EnableDiskSpaceGuard: IsDiskSpaceGuardEnabled,
            MinFreeSpaceMb: minFreeSpace,
            MaxRecordingStorageGb: Math.Max(0, maxRecordingStorage),
            PlaybackShowFilename: ShowPlaybackFilename,
            PlaybackFilenameColor: SelectedPlaybackFilenameColor,
            PlaybackShowTimestamp: ShowPlaybackTimestamp,
            PlaybackTimestampColor: SelectedPlaybackTimestampColor,
            EnableDebugLogging: EnableDebugLogging,
            LogPath: LogPath?.FullName ?? string.Empty);
    }

    #endregion
}