// ReSharper disable UnusedMember.Local
// ReSharper disable InvertIf
// ReSharper disable ParameterHidesMember
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
// ReSharper disable GrammarMistakeInComment
namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// Control for displaying a single camera stream with overlay and context menu.
/// </summary>
/// <remarks>
/// CS0169 / CS0414 are suppressed via &lt;NoWarn&gt; in the .csproj because
/// [DependencyProperty] backing fields are read by the source-generated
/// property surface; compiler warnings cannot be silenced via [SuppressMessage].
/// </remarks>
public partial class CameraTile : IDisposable
{
    [DependencyProperty(PropertyChangedCallback = nameof(OnCameraChanged))]
    private CameraConfiguration? camera;

    [DependencyProperty(DefaultValue = ConnectionState.Disconnected, PropertyChangedCallback = nameof(OnConnectionStateChanged))]
    private ConnectionState connectionState;

    /// <summary>
    /// True when this tile's USB camera is currently unplugged. Set by
    /// the manager / dashboard from the
    /// <see cref="AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher"/> events; cleared automatically on
    /// <see cref="AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher.DeviceArrived"/>. Cascades to the
    /// overlay so the operator sees a distinct affordance without
    /// fighting the connection-state row (the two are orthogonal — see
    /// <see cref="CameraOverlay.IsDeviceUnplugged"/>).
    /// </summary>
    [DependencyProperty(DefaultValue = false, PropertyChangedCallback = nameof(OnIsDeviceUnpluggedChanged))]
    private bool isDeviceUnplugged;

    /// <summary>
    /// True when the underlying USB device opens but reads stall — typically
    /// "another app has the camera" (Teams, browser, OBS). Set by
    /// <see cref="OnPlayerStateChanged"/> when the engine reports
    /// <see cref="StreamFailureReason.DeviceBusy"/>; cleared on the next
    /// successful Connected transition (auto-reconnect path). Cascades to
    /// the overlay row the same way <see cref="IsDeviceUnplugged"/> does.
    /// </summary>
    [DependencyProperty(DefaultValue = false, PropertyChangedCallback = nameof(OnIsDeviceBusyChanged))]
    private bool isDeviceBusy;

    [DependencyProperty(PropertyChangedCallback = nameof(OnIsSelectedChanged))]
    private bool isSelected;

    [DependencyProperty(PropertyChangedCallback = nameof(OnCameraNameChanged))]
    private string cameraName = string.Empty;

    [DependencyProperty(PropertyChangedCallback = nameof(OnCameraDescriptionChanged))]
    private string cameraDescription = string.Empty;

    [DependencyProperty]
    private IVideoPlayer? player;

    [DependencyProperty(DefaultValue = false)]
    private bool isPlayerLent;

    [DependencyProperty(DefaultValue = HorizontalAlignment.Left)]
    private HorizontalAlignment overlayHorizontalAlignment;

    [DependencyProperty(DefaultValue = VerticalAlignment.Top)]
    private VerticalAlignment overlayVerticalAlignment;

    // Quick-action stack alignment is always the diagonal opposite of the
    // overlay info box so the two never overlap. Computed in
    // UpdateOverlayPosition alongside OverlayHorizontalAlignment/Vertical.
    [DependencyProperty(DefaultValue = HorizontalAlignment.Right)]
    private HorizontalAlignment quickActionsHorizontalAlignment;

    [DependencyProperty(DefaultValue = VerticalAlignment.Bottom)]
    private VerticalAlignment quickActionsVerticalAlignment;

    [DependencyProperty(DefaultValue = nameof(Brushes.Transparent))]
    private Brush selectionBorderBrush = Brushes.Transparent;

    [DependencyProperty(DefaultValue = true, PropertyChangedCallback = nameof(OnOverlaySettingsChanged))]
    private bool showOverlayTitle;

    [DependencyProperty(DefaultValue = true, PropertyChangedCallback = nameof(OnOverlaySettingsChanged))]
    private bool showOverlayDescription;

    [DependencyProperty(DefaultValue = true, PropertyChangedCallback = nameof(OnOverlaySettingsChanged))]
    private bool showOverlayConnectionStatus;

    [DependencyProperty(DefaultValue = true, PropertyChangedCallback = nameof(OnShowOverlayQuickActionsChanged))]
    private bool showOverlayQuickActions;

    [DependencyProperty(DefaultValue = false, PropertyChangedCallback = nameof(OnOverlaySettingsChanged))]
    private bool showOverlayTime;

    [DependencyProperty(DefaultValue = false, PropertyChangedCallback = nameof(OnConnectionTypeIconSettingsChanged))]
    private bool showOverlayConnectionTypeIcon;

    [DependencyProperty(DefaultValue = ConnectionTypeIconPlacement.SameRow, PropertyChangedCallback = nameof(OnConnectionTypeIconSettingsChanged))]
    private ConnectionTypeIconPlacement connectionTypeIconPlacement;

    // Connection-type icon corner — one of the two corners adjacent to the
    // info box, computed in UpdateOverlayPosition.
    [DependencyProperty(DefaultValue = HorizontalAlignment.Right)]
    private HorizontalAlignment connectionTypeIconHorizontalAlignment;

    [DependencyProperty(DefaultValue = VerticalAlignment.Top)]
    private VerticalAlignment connectionTypeIconVerticalAlignment;

    [DependencyProperty(DefaultValue = 0.6, PropertyChangedCallback = nameof(OnOverlaySettingsChanged))]
    private double overlayOpacity;

    [DependencyProperty]
    private string? snapshotPath;

    [DependencyProperty(DefaultValue = true)]
    private bool autoConnectOnLoad;

    // Connection settings
    [DependencyProperty(DefaultValue = 10)]
    private int connectionTimeoutSeconds;

    [DependencyProperty(DefaultValue = 10)]
    private int reconnectDelaySeconds;

    [DependencyProperty(DefaultValue = true)]
    private bool autoReconnectOnFailure;

    // Notification settings
    [DependencyProperty(DefaultValue = true)]
    private bool showNotificationOnDisconnect;

    [DependencyProperty(DefaultValue = false)]
    private bool showNotificationOnReconnect;

    [DependencyProperty(DefaultValue = false)]
    private bool playNotificationSound;

    // Performance settings
    [DependencyProperty(DefaultValue = "Auto", PropertyChangedCallback = nameof(OnPerformanceSettingChanged))]
    private string videoQuality = "Auto";

    [DependencyProperty(DefaultValue = true, PropertyChangedCallback = nameof(OnPerformanceSettingChanged))]
    private bool hardwareAcceleration;

    // Recording settings
    [DependencyProperty(DefaultValue = RecordingState.Idle, PropertyChangedCallback = nameof(OnRecordingStateChanged))]
    private RecordingState recordingState;

    [DependencyProperty]
    private TimeSpan recordingDuration;

    [DependencyProperty(DefaultValue = false)]
    private bool isMotionDetected;

    [DependencyProperty(DefaultValue = false, PropertyChangedCallback = nameof(OnRecordingOnMotionSettingChanged))]
    private bool enableRecordingOnMotion;

    [DependencyProperty(DefaultValue = false)]
    private bool enableRecordingOnConnect;

    // Read when the camera is opened: a change reaches the camera the next time it connects.
    [DependencyProperty(DefaultValue = VideoTranscodeCodec.None)]
    private VideoTranscodeCodec transcodeVideoCodec;

    // Bounding box settings
    [DependencyProperty(DefaultValue = false, PropertyChangedCallback = nameof(OnBoundingBoxSettingsChanged))]
    private bool showBoundingBoxInGrid;

    [DependencyProperty(DefaultValue = "Red", PropertyChangedCallback = nameof(OnBoundingBoxSettingsChanged))]
    private string boundingBoxColor = "Red";

    [DependencyProperty(DefaultValue = 2, PropertyChangedCallback = nameof(OnBoundingBoxSettingsChanged))]
    private int boundingBoxThickness;

    [DependencyProperty(DefaultValue = 0.3, PropertyChangedCallback = nameof(OnBoundingBoxSettingsChanged))]
    private double boundingBoxSmoothing;

    // Motion detection settings
    [DependencyProperty(DefaultValue = 30)]
    private int motionSensitivity;

    [DependencyProperty(DefaultValue = 2.0)]
    private double motionMinimumChangePercent;

    [DependencyProperty(DefaultValue = MotionDetectorOptions.DefaultSmallestObjectPercent)]
    private double motionSmallestObjectPercent;

    [DependencyProperty(DefaultValue = MotionDetectorOptions.DefaultStoppedObjectSeconds)]
    private int motionStoppedObjectSeconds;

    [DependencyProperty(DefaultValue = true)]
    private bool motionIgnoreLightingChanges;

    [DependencyProperty(DefaultValue = 2)]
    private int motionAnalysisFrameRate;

    // The longer side of the analysed picture, as the app sets it.
    [DependencyProperty(DefaultValue = MotionAnalysisSize.DefaultLongerSide)]
    private int motionAnalysisWidth;

    [DependencyProperty(DefaultValue = 10)]
    private int motionPostDurationSeconds;

    [DependencyProperty(DefaultValue = 5)]
    private int motionCooldownSeconds;

    [DependencyProperty(DefaultValue = 100)]
    private int motionBoundingBoxMinArea;

    [DependencyProperty(DefaultValue = 4)]
    private int motionBoundingBoxPadding;

    [DependencyProperty]
    private Func<IVideoPlayer, IMediaPipeline>? mediaPipelineFactory;

    // Recording and notification services
    private IRecordingService? recordingService;
    private IMotionDetectionService? motionDetectionService;
    private ITimelapseService? timelapseService;
    private IToastNotificationService? toastNotificationService;
    private ILogger logger = NullLogger.Instance;
    private DispatcherTimer? recordingDurationTimer;
    private IMediaPipeline? mediaPipeline;
    private CameraRotation appliedRotation;
    private IVideoPlayerFactory? videoPlayerFactory;

    // Zoom state
    private float currentZoom = 1.0f;
    private float currentPanX;
    private float currentPanY;
    private bool isPanning;
    private bool isSelecting;
    private Point panStartPoint;
    private Point selectionStartPoint;
    private float panStartX;
    private float panStartY;

    private bool disposed;
    private bool markedForRemoval;
    private Window? subscribedWindow;
    private CameraGrid? registeredWithGrid;
    private bool isReconnecting;
    private bool isSwapping;
    private bool isDragging;
    private Point dragStartPoint;
    private OverlayPosition currentOverlayPosition = OverlayPosition.TopLeft;
    private CameraOverlay? cachedOverlay;
    private MotionBoundingBoxOverlay? cachedMotionOverlay;
    private DispatcherTimer? reconnectTimer;
    private DispatcherTimer? autoReconnectTimer;
    private int reconnectCheckCount;
    private int reconnectAttemptCount;

    // Stream health monitoring
    private DispatcherTimer? streamHealthTimer;
    private long lastFramesDisplayed;
    private int staleStreamCheckCount;

    // Track previous performance override values to detect changes that require reconnection
    private string? previousOverrideVideoQuality;
    private bool? previousOverrideHardwareAcceleration;

    // Track previous motion detection override values to detect changes that require restart
    private bool? previousOverrideEnableRecordingOnMotion;
    private bool? previousOverrideShowBoundingBoxInGrid;
    private bool? previousOverrideShowBoundingBoxInFullScreen;
    private string? previousOverrideBoundingBoxColor;
    private int? previousOverrideBoundingBoxThickness;
    private int? previousOverrideBoundingBoxMinArea;
    private int? previousOverrideMotionSensitivity;
    private double? previousOverrideMotionMinimumChangePercent;
    private int? previousOverrideMotionAnalysisFrameRate;
    private int? previousOverrideMotionAnalysisWidth;
    private int? previousOverrideMotionAnalysisHeight;
    private int? previousOverrideMotionCooldownSeconds;
    private int? previousOverridePostMotionDurationSeconds;
    private (double? SmallestObjectPercent, int? StoppedObjectSeconds, bool? IgnoreLightingChanges) previousOverrideDetector;

    // Tracks whether the mouse is currently hovering the bottom-right
    // corner of the tile. Drives the fullscreen quick-action button's
    // visibility (show on hover, hide otherwise). Updated by the hover
    // poll timer below — WPF mouse events don't fire reliably for hover
    // detection inside the VideoHost overlay surface, so we poll the
    // global cursor position instead.
    private bool isTileHovered;
    private DispatcherTimer? hoverPollTimer;

    /// <summary>
    /// Gets whether motion detection should run (for recording or bounding box display).
    /// Uses effective values considering per-camera overrides.
    /// </summary>
    private bool ShouldRunMotionDetection =>
        GetEffectiveEnableRecordingOnMotion() || GetEffectiveShowBoundingBoxInGrid();

    /// <summary>
    /// Gets the effective EnableRecordingOnMotion value, considering camera override.
    /// </summary>
    private bool GetEffectiveEnableRecordingOnMotion()
        => Camera?.Overrides?.Recording.EnableRecordingOnMotion ?? EnableRecordingOnMotion;

    /// <summary>
    /// Gets the effective ShowBoundingBoxInGrid value, considering camera override.
    /// </summary>
    private bool GetEffectiveShowBoundingBoxInGrid()
        => Camera?.Overrides?.MotionDetection.BoundingBox.ShowInGrid ?? ShowBoundingBoxInGrid;

    /// <summary>
    /// Gets the effective ShowBoundingBoxInFullScreen value, considering camera override.
    /// </summary>
    private bool GetEffectiveShowBoundingBoxInFullScreen()
        => Camera?.Overrides?.MotionDetection.BoundingBox.ShowInFullScreen ?? false;

    /// <summary>
    /// Gets the effective BoundingBoxColor value, considering camera override.
    /// </summary>
    private string GetEffectiveBoundingBoxColor()
        => Camera?.Overrides?.MotionDetection.BoundingBox.Color ?? BoundingBoxColor;

    /// <summary>
    /// Gets the effective BoundingBoxThickness value, considering camera override.
    /// </summary>
    private int GetEffectiveBoundingBoxThickness()
        => Camera?.Overrides?.MotionDetection.BoundingBox.Thickness ?? BoundingBoxThickness;

    /// <summary>
    /// Gets the effective BoundingBoxSmoothing value, considering camera override.
    /// </summary>
    private double GetEffectiveBoundingBoxSmoothing()
        => Camera?.Overrides?.MotionDetection.BoundingBox.Smoothing ?? BoundingBoxSmoothing;

    /// <summary>
    /// Gets the effective BoundingBoxMinArea value, considering camera override.
    /// </summary>
    private int GetEffectiveBoundingBoxMinArea()
        => Camera?.Overrides?.MotionDetection.BoundingBox.MinArea ?? MotionBoundingBoxMinArea;

    /// <summary>
    /// Gets the effective BoundingBoxPadding value, considering camera override.
    /// </summary>
    private int GetEffectiveBoundingBoxPadding()
        => Camera?.Overrides?.MotionDetection.BoundingBox.Padding ?? MotionBoundingBoxPadding;

    /// <summary>
    /// Gets the effective MotionSensitivity value, considering camera override.
    /// </summary>
    private int GetEffectiveMotionSensitivity()
        => Camera?.Overrides?.MotionDetection.Sensitivity ?? MotionSensitivity;

    /// <summary>
    /// Gets the effective MotionMinimumChangePercent value, considering camera override.
    /// </summary>
    private double GetEffectiveMotionMinimumChangePercent()
        => Camera?.Overrides?.MotionDetection.MinimumChangePercent ?? MotionMinimumChangePercent;

    /// <summary>
    /// Gets the effective smallest object, in percent of the picture, considering camera override.
    /// </summary>
    private double GetEffectiveMotionSmallestObjectPercent()
        => Camera?.Overrides?.MotionDetection.SmallestObjectPercent ?? MotionSmallestObjectPercent;

    /// <summary>
    /// Gets how long something that stops is still seen, considering camera override.
    /// </summary>
    private int GetEffectiveMotionStoppedObjectSeconds()
        => Camera?.Overrides?.MotionDetection.StoppedObjectSeconds ?? MotionStoppedObjectSeconds;

    /// <summary>
    /// Gets whether lighting changes are ignored, considering camera override.
    /// </summary>
    private bool GetEffectiveMotionIgnoreLightingChanges()
        => Camera?.Overrides?.MotionDetection.IgnoreLightingChanges ?? MotionIgnoreLightingChanges;

    // The camera's own detector settings, to tell when one changed.
    private static (double? SmallestObjectPercent, int? StoppedObjectSeconds, bool? IgnoreLightingChanges) DetectorOverrides(
        CameraConfiguration? camera)
    {
        var motion = camera?.Overrides?.MotionDetection;
        return (motion?.SmallestObjectPercent, motion?.StoppedObjectSeconds, motion?.IgnoreLightingChanges);
    }

    /// <summary>
    /// Gets the effective MotionAnalysisFrameRate value, considering camera override.
    /// </summary>
    private int GetEffectiveMotionAnalysisFrameRate()
        => Camera?.Overrides?.MotionDetection.AnalysisFrameRate ?? MotionAnalysisFrameRate;

    /// <summary>
    /// Gets the effective MotionPostDurationSeconds value, considering camera override.
    /// </summary>
    private int GetEffectiveMotionPostDurationSeconds()
        => Camera?.Overrides?.MotionDetection.PostMotionDurationSeconds ?? MotionPostDurationSeconds;

    /// <summary>
    /// Gets the effective MotionCooldownSeconds value, considering camera override.
    /// </summary>
    private int GetEffectiveMotionCooldownSeconds()
        => Camera?.Overrides?.MotionDetection.CooldownSeconds ?? MotionCooldownSeconds;

    /// <summary>
    /// Gets the effective EnableRecordingOnConnect value, considering camera override.
    /// </summary>
    private bool GetEffectiveEnableRecordingOnConnect()
        => Camera?.Overrides?.Recording.EnableRecordingOnConnect ?? EnableRecordingOnConnect;

    /// <summary>
    /// Gets the effective AutoReconnectOnFailure value, considering camera override.
    /// </summary>
    private bool GetEffectiveAutoReconnectOnFailure()
        => Camera?.Overrides?.Connection.AutoReconnectOnFailure ?? AutoReconnectOnFailure;

    /// <summary>
    /// Gets the effective ReconnectDelaySeconds value, considering camera override.
    /// </summary>
    private int GetEffectiveReconnectDelaySeconds()
        => Camera?.Overrides?.Connection.ReconnectDelaySeconds ?? ReconnectDelaySeconds;

    /// <summary>
    /// Gets the longer side of the analysed picture, considering camera override; the other side
    /// follows the picture's shape.
    /// </summary>
    private int GetEffectiveMotionAnalysisWidth()
        => Camera?.Overrides?.MotionDetection.AnalysisWidth is > 0 and var own ? own : MotionAnalysisWidth;

    /// <summary>
    /// Occurs when a full screen request is made.
    /// </summary>
    public event EventHandler<FullScreenRequestedEventArgs>? FullScreenRequested;

    /// <summary>
    /// Occurs when the zoom state changes on this tile.
    /// </summary>
    public event EventHandler? ZoomStateChanged;

    /// <summary>
    /// Occurs when a swap left request is made.
    /// </summary>
    public event EventHandler<CameraConfiguration>? SwapLeftRequested;

    /// <summary>
    /// Occurs when a swap right request is made.
    /// </summary>
    public event EventHandler<CameraConfiguration>? SwapRightRequested;

    /// <summary>
    /// Occurs when the connection state changes.
    /// </summary>
    public event EventHandler<CameraConnectionChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Occurs when an edit camera request is made.
    /// </summary>
    public event EventHandler<CameraConfiguration>? EditCameraRequested;

    /// <summary>
    /// Occurs when a delete camera request is made.
    /// </summary>
    public event EventHandler<CameraConfiguration>? DeleteCameraRequested;

    /// <summary>
    /// Occurs when a camera is dropped onto this tile.
    /// </summary>
    public event EventHandler<CameraConfiguration>? CameraDropped;

    /// <summary>
    /// Occurs when the camera configuration is mutated in-place by a tile-level
    /// command (e.g., the Rotate context-menu submenu). The host should persist
    /// the updated configuration.
    /// </summary>
    public event EventHandler<CameraConfiguration>? CameraConfigurationChanged;

    /// <summary>
    /// Occurs when the recording state changes.
    /// </summary>
    public event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChangedEvent;

    // An app that uses this control from the package may never touch Translations, and
    // Atc.Wpf finds the texts only in assemblies already loaded: load them before the XAML.
    static CameraTile()
    {
        _ = Translations.ResourceManager;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraTile"/> class.
    /// </summary>
    public CameraTile()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        VideoPlayer.OverlayCreated += OnOverlayCreated;
        VideoPlayer.SurfaceCreated += OnSurfaceCreated;
        InputManager.Current.PreProcessInput += OnPreProcessInput;
    }

    /// <summary>
    /// Initializes the recording, motion detection, timelapse, and toast notification services.
    /// </summary>
    /// <param name="recordingService">The recording service.</param>
    /// <param name="motionDetectionService">The motion detection service.</param>
    /// <param name="timelapseService">The timelapse service.</param>
    /// <param name="toastNotificationService">The toast notification service.</param>
    /// <param name="videoPlayerFactory">The video player factory.</param>
    /// <param name="mediaPipelineFactory">The media pipeline factory.</param>
    /// <param name="logger">The logger for tile events.</param>
    public void InitializeServices(
        IRecordingService? recordingService,
        IMotionDetectionService? motionDetectionService,
        ITimelapseService? timelapseService = null,
        IToastNotificationService? toastNotificationService = null,
        IVideoPlayerFactory? videoPlayerFactory = null,
        Func<IVideoPlayer, IMediaPipeline>? mediaPipelineFactory = null,
        ILogger? logger = null)
    {
        this.logger = logger ?? NullLogger.Instance;

        // Unsubscribe from previous services
        if (this.recordingService is not null)
        {
            this.recordingService.RecordingStateChanged -= OnServiceRecordingStateChanged;
        }

        if (this.motionDetectionService is not null)
        {
            this.motionDetectionService.MotionDetected -= OnMotionDetected;
        }

        this.recordingService = recordingService;
        this.motionDetectionService = motionDetectionService;
        this.timelapseService = timelapseService;
        this.toastNotificationService = toastNotificationService;
        this.videoPlayerFactory = videoPlayerFactory;
        MediaPipelineFactory = mediaPipelineFactory;

        // Create media pipeline if the player already exists but pipeline was not yet created
        if (this.mediaPipeline is null && Player is not null && MediaPipelineFactory is not null)
        {
            this.mediaPipeline = MediaPipelineFactory.Invoke(Player);
            ApplyRotation();
        }

        // Subscribe to new services
        if (this.recordingService is not null)
        {
            this.recordingService.RecordingStateChanged += OnServiceRecordingStateChanged;
        }

        if (this.motionDetectionService is not null)
        {
            this.motionDetectionService.MotionDetected += OnMotionDetected;
        }

        // Auto-start recording if camera is already connected and settings require it
        // This handles the race condition where camera connects before services are initialized
        if (ConnectionState == ConnectionState.Connected &&
            GetEffectiveEnableRecordingOnConnect() &&
            this.recordingService is not null &&
            this.mediaPipeline is not null &&
            Camera is not null &&
            RecordingState == RecordingState.Idle)
        {
            this.recordingService.StartRecording(Camera, this.mediaPipeline);
        }

        // Start motion detection if camera is already connected and settings require it
        // This handles the race condition where camera connects before services are initialized
        if (ConnectionState == ConnectionState.Connected && ShouldRunMotionDetection)
        {
            StartMotionDetection();
        }

        // Start timelapse if camera is already connected and enabled
        if (ConnectionState == ConnectionState.Connected)
        {
            StartTimelapse();
        }

        // Complete deferred player initialization if Camera was set before the factory was available
        if (this.videoPlayerFactory is not null && Camera is not null && Player is null)
        {
            InitializePlayer();
        }
    }

    /// <summary>
    /// Starts playing the camera stream.
    /// </summary>
    public void Play()
    {
        if (Camera is null || Player is null)
        {
            return;
        }

        try
        {
            var locator = Camera.BuildSourceLocator();
            var options = BuildStreamOptions(Camera);
            Player.Open(locator.Uri, options);
        }
        catch (Exception ex)
        {
            UpdateConnectionState(ConnectionState.ConnectionFailed, ex.Message);
        }
    }

    /// <summary>
    /// Stops the camera stream.
    /// </summary>
    public void Stop()
    {
        Player?.Close();
    }

    /// <summary>
    /// Prepares the tile for a swap operation.
    /// During swap, the player will not be disposed when the camera changes.
    /// </summary>
    public void PrepareForSwap()
    {
        isSwapping = true;
    }

    /// <summary>
    /// Completes the swap operation, restoring normal behavior.
    /// </summary>
    public void CompleteSwap()
    {
        isSwapping = false;
    }

    /// <summary>
    /// Swaps the Player instance with another CameraTile to keep streams alive
    /// during a position swap. Both tiles should already have their Camera DPs
    /// updated (via collection swap with isSwapping=true). After this call, each
    /// tile's Player plays the stream matching its new Camera.
    /// </summary>
    /// <param name="other">The other tile to swap players with.</param>
    public void SwapPlayerWith(CameraTile other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var myPlayer = Player;
        var otherPlayer = other.Player;
        var myPipeline = mediaPipeline;
        var otherPipeline = other.mediaPipeline;

        // Detach StateChanged handlers from current players
        if (myPlayer is not null)
        {
            myPlayer.StateChanged -= OnPlayerStateChanged;
        }

        if (otherPlayer is not null)
        {
            otherPlayer.StateChanged -= other.OnPlayerStateChanged;
        }

        // Stop stream health monitoring on both (will be restarted after swap)
        StopStreamHealthCheck();
        other.StopStreamHealthCheck();

        // Detach both players from FlyleafHost to avoid a Player being
        // attached to two hosts simultaneously
        Player = null;
        other.Player = null;

        // Reattach swapped players
        Player = otherPlayer;
        other.Player = myPlayer;

        // Swap media pipelines
        mediaPipeline = otherPipeline;
        other.mediaPipeline = myPipeline;

        // Reattach StateChanged handlers to the new players
        if (Player is not null)
        {
            Player.StateChanged += OnPlayerStateChanged;
        }

        if (other.Player is not null)
        {
            other.Player.StateChanged += other.OnPlayerStateChanged;
        }

        // Swap recording state (tracked per camera by IRecordingService)
        (RecordingState, other.RecordingState) = (other.RecordingState, RecordingState);
        (RecordingDuration, other.RecordingDuration) = (other.RecordingDuration, RecordingDuration);

        // Stop recording duration timers — their Tick closures reference the original tile,
        // so we must recreate them rather than swapping references
        recordingDurationTimer?.Stop();
        recordingDurationTimer = null;
        other.recordingDurationTimer?.Stop();
        other.recordingDurationTimer = null;
        UpdateRecordingDurationTimer();
        other.UpdateRecordingDurationTimer();

        // Swap motion detection state
        (IsMotionDetected, other.IsMotionDetected) = (other.IsMotionDetected, IsMotionDetected);

        // Restart stream health monitoring with new players
        StartStreamHealthCheck();
        other.StartStreamHealthCheck();

        // Update overlays to reflect swapped state
        UpdateOverlayRecordingState();
        other.UpdateOverlayRecordingState();
        UpdateOverlayMotionIndicator();
        other.UpdateOverlayMotionIndicator();
        UpdateOverlayRecordingOnMotion();
        other.UpdateOverlayRecordingOnMotion();
        ApplyBoundingBoxSettings();
        other.ApplyBoundingBoxSettings();

        // The camera change during the swap applied each tile's new rotation
        // to the player it was about to hand over. Apply it to the player it
        // received instead.
        ApplyRotation();
        other.ApplyRotation();
    }

    /// <summary>
    /// Lends the player to another control (e.g., fullscreen window).
    /// The player is removed from this tile and the tile shows a placeholder.
    /// </summary>
    /// <returns>The player instance, or null if no player is available.</returns>
    public IVideoPlayer? LendPlayer()
    {
        if (Player is null || IsPlayerLent)
        {
            return null;
        }

        var lentPlayer = Player;
        Player = null;
        IsPlayerLent = true;
        return lentPlayer;
    }

    /// <summary>
    /// Returns a previously lent player to this tile.
    /// </summary>
    /// <param name="returnedPlayer">The player to return.</param>
    public void ReturnPlayer(IVideoPlayer? returnedPlayer)
    {
        if (returnedPlayer is null)
        {
            IsPlayerLent = false;
            return;
        }

        Player = returnedPlayer;
        IsPlayerLent = false;
    }

    /// <summary>
    /// Reconnects the camera stream.
    /// </summary>
    public void Reconnect()
    {
        if (Camera is null || Player is null)
        {
            return;
        }

        // Set flag to skip Disconnected state during reconnection
        isReconnecting = true;

        // Update state immediately
        UpdateConnectionState(ConnectionState.Connecting);

        // Defer player operations to allow UI to render the "Connecting" state first
        // Player.Open() can block the UI thread, so we use BeginInvoke to let the render pass complete
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            ReconnectPlayer);
    }

    /// <summary>
    /// Disposes of the control resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            LogTileDispose(
                Camera?.Display.DisplayName ?? "<no-camera>",
                ConnectionState.ToString());

            // Release the RecordingService session for this camera *before* the
            // player goes away. Without this, the service's sessions dictionary
            // keeps a stale entry pointing at the now-finalized file, and a new
            // tile for the same camera (RDP-driven zombie replacement) cannot
            // start a fresh recording — StartRecording bails on the duplicate.
            if (recordingService is not null && Camera is not null)
            {
                recordingService.StopRecording(Camera.Id);
            }

            // Fire disconnect event if camera was connected
            if (Camera is not null && ConnectionState != ConnectionState.Disconnected)
            {
                var oldState = ConnectionState;
                ConnectionState = ConnectionState.Disconnected;
                ConnectionStateChanged?.Invoke(
                    this,
                    new CameraConnectionChangedEventArgs(Camera, oldState, ConnectionState.Disconnected));
            }

            if (reconnectTimer is not null)
            {
                reconnectTimer.Stop();
                reconnectTimer.Tick -= OnReconnectTimerTick;
                reconnectTimer = null;
            }

            autoReconnectTimer?.Stop();
            autoReconnectTimer = null;

            // Stop stream health monitoring
            StopStreamHealthCheck();

            recordingDurationTimer?.Stop();
            recordingDurationTimer = null;

            if (hoverPollTimer is not null)
            {
                hoverPollTimer.Stop();
                hoverPollTimer.Tick -= OnHoverPollTick;
                hoverPollTimer = null;
            }

            // Stop motion detection
            StopMotionDetection();

            // Unsubscribe from services
            if (recordingService is not null)
            {
                recordingService.RecordingStateChanged -= OnServiceRecordingStateChanged;
            }

            if (motionDetectionService is not null)
            {
                motionDetectionService.MotionDetected -= OnMotionDetected;
            }

            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            if (subscribedWindow is not null)
            {
                subscribedWindow.Closing -= OnAncestorWindowClosing;
                subscribedWindow = null;
            }

            if (registeredWithGrid is not null)
            {
                registeredWithGrid.UnregisterTile(this);
                registeredWithGrid = null;
            }

            VideoPlayer.OverlayCreated -= OnOverlayCreated;
            VideoPlayer.SurfaceCreated -= OnSurfaceCreated;
            InputManager.Current.PreProcessInput -= OnPreProcessInput;

            if (VideoPlayer.Overlay is not null)
            {
                VideoPlayer.Overlay.PreviewMouseRightButtonUp -= OnMouseRightButtonUp;
                VideoPlayer.Overlay.PreviewMouseWheel -= OnOverlayMouseWheel;
            }

            if (VideoPlayer.Surface is not null)
            {
                VideoPlayer.Surface.PreviewMouseRightButtonUp -= OnMouseRightButtonUp;
                VideoPlayer.Surface.PreviewMouseWheel -= OnOverlayMouseWheel;
            }

            if (Camera is not null)
            {
                Camera.PropertyChanged -= OnCameraPropertyChanged;
            }

            mediaPipeline?.Dispose();
            mediaPipeline = null;

            Player?.Dispose();
            Player = null;
        }

        disposed = true;
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        LogTileLoaded(
            Camera?.Display.DisplayName ?? "<no-camera>",
            window?.WindowState.ToString() ?? "<no-window>",
            window is not null);

        // Register with the owning CameraGrid so it can reach this tile without
        // a visual-tree walk. RDP-driven Unloaded leaves the tile alive but
        // orphaned from the tree, so visual lookup fails and recovery paths
        // (session-switch driven RecreatePlayer) need this registry to find us.
        if (registeredWithGrid is null)
        {
            registeredWithGrid = FindParentCameraGrid();
            registeredWithGrid?.RegisterTile(this);
        }

        // Subscribe (once per ancestor window) to Closing so we can distinguish a
        // real teardown from a transient unload (RDP session change, virtualization,
        // theme reload). Without this, OnUnloaded cannot tell the two apart and
        // disposing the player on a transient unload kills the live RTSP stream.
        if (window is not null && !ReferenceEquals(subscribedWindow, window))
        {
            if (subscribedWindow is not null)
            {
                subscribedWindow.Closing -= OnAncestorWindowClosing;
            }

            subscribedWindow = window;
            subscribedWindow.Closing += OnAncestorWindowClosing;
        }

        // Try to subscribe if windows already exist
        SubscribeToMouseEvents();

        // Delay overlay caching to ensure the visual tree is fully built
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            CacheOverlayReference);

        // After a transient WPF unload (RDP session change, theme reload,
        // virtualization, etc.) the player may have lost its stream while the
        // visual tree was torn down — D3D11 device-removed during desktop
        // session change is a common trigger. Force a reconnect on reload if
        // the tile didn't come back in a Connected/Connecting state, so 24/7
        // operation recovers automatically without operator intervention.
        if (AutoConnectOnLoad &&
            Camera is not null &&
            Player is not null &&
            ConnectionState is not ConnectionState.Connected
                and not ConnectionState.Connecting)
        {
            LogReconnectingAfterReload(Camera.Display.DisplayName, ConnectionState.ToString());
            Reconnect();
        }
    }

    private void OnAncestorWindowClosing(
        object? sender,
        CancelEventArgs e)
    {
        // The user is closing the parent window. Subsequent Unloaded fires are
        // real teardown (not transient), so allow Dispose to run.
        markedForRemoval = true;
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        LogTileUnloaded(
            Camera?.Display.DisplayName ?? "<no-camera>",
            ConnectionState.ToString(),
            IsLoaded,
            window?.WindowState.ToString() ?? "<no-window>",
            window is not null,
            VisualTreeHelper.GetParent(this)?.GetType().Name ?? "<no-parent>");

        // RDP session changes, layout virtualization, theme reloads, and other
        // transient WPF rebuilds cause Unloaded to fire on a still-live tile.
        // Disposing the player on those events kills 24/7 recording until the
        // user manually intervenes. Only dispose when the tile has been
        // explicitly removed (MarkForRemoval) or its window is closing.
        if (markedForRemoval)
        {
            Dispose();
        }
    }

    /// <summary>
    /// Marks this tile for real disposal on the next <see cref="OnUnloaded"/>.
    /// Call this from <see cref="CameraGrid"/> just before removing the tile's
    /// camera from the bound collection, so legitimate removals tear down the
    /// player while transient WPF unload/reload cycles do not.
    /// </summary>
    public void MarkForRemoval()
    {
        markedForRemoval = true;
    }

    private CameraGrid? FindParentCameraGrid()
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is CameraGrid grid)
            {
                return grid;
            }
        }

        return null;
    }

    private void OnOverlayCreated(
        object? sender,
        EventArgs e)
    {
        SubscribeToMouseEvents();

        // Delay overlay caching to ensure the visual tree is fully built
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            CacheOverlayReference);
    }

    private void OnSurfaceCreated(
        object? sender,
        EventArgs e)
        => SubscribeToMouseEvents();

    private void OnMouseRightButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        // Don't show context menu while reconnecting
        if (isReconnecting)
        {
            e.Handled = true;
            return;
        }

        var contextMenu = new ContextMenu();

        contextMenu.Items.Add(new MenuItem { Header = Translations.ShowInFullScreen, Command = FullScreenCommand });
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(new MenuItem { Header = Translations.SwapLeft, Command = SwapLeftCommand });
        contextMenu.Items.Add(new MenuItem { Header = Translations.SwapRight, Command = SwapRightCommand });
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(new MenuItem { Header = Translations.TakeSnapshot, Command = SnapshotCommand });

        // Recording menu items
        contextMenu.Items.Add(RecordingState == RecordingState.Idle
            ? new MenuItem { Header = Translations.StartRecording, Command = StartRecordingCommand }
            : new MenuItem { Header = Translations.StopRecording, Command = StopRecordingCommand });

        contextMenu.Items.Add(new MenuItem { Header = Translations.Reconnect, Command = ReconnectCommand });
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(new MenuItem { Header = Translations.ResetZoom, Command = ZoomResetCommand });
        contextMenu.Items.Add(BuildRotateMenu());
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(new MenuItem { Header = Translations.EditCamera, Command = EditCameraCommand });

        contextMenu.IsOpen = true;
        e.Handled = true;
    }

    private static void OnCameraChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile)
        {
            if (e.OldValue is CameraConfiguration oldCamera)
            {
                oldCamera.PropertyChanged -= tile.OnCameraPropertyChanged;
            }

            if (e.NewValue is CameraConfiguration newCamera)
            {
                newCamera.PropertyChanged += tile.OnCameraPropertyChanged;
            }

            tile.OnCameraChangedInternal(e.NewValue as CameraConfiguration);
        }
    }

    private static void OnIsSelectedChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile)
        {
            tile.SelectionBorderBrush = (bool)e.NewValue
                ? Brushes.DodgerBlue
                : Brushes.Transparent;
        }
    }

    private static void OnConnectionStateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile && e.NewValue is ConnectionState newState)
        {
            tile.UpdateOverlayConnectionState(newState);
            tile.UpdateConnectionTypeIcon();
            tile.UpdateQuickActionVisibility();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private static void OnIsDeviceUnpluggedChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile && e.NewValue is bool unplugged)
        {
            var overlay = tile.GetCameraOverlay();
            if (overlay is not null)
            {
                overlay.IsDeviceUnplugged = unplugged;
            }
        }
    }

    private static void OnIsDeviceBusyChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile && e.NewValue is bool busy)
        {
            var overlay = tile.GetCameraOverlay();
            if (overlay is not null)
            {
                overlay.IsDeviceBusy = busy;
            }
        }
    }

    private static void OnCameraNameChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile && e.NewValue is string newName)
        {
            tile.UpdateOverlayTitle(newName);
        }
    }

    private static void OnCameraDescriptionChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile)
        {
            tile.UpdateOverlayDescription(e.NewValue as string ?? string.Empty);
        }
    }

    private static void OnOverlaySettingsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile)
        {
            tile.ApplyOverlaySettings();
        }
    }

    private static void OnConnectionTypeIconSettingsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile)
        {
            tile.UpdateOverlayPosition(tile.currentOverlayPosition);
            tile.UpdateConnectionTypeIcon();
        }
    }

    private static void OnPerformanceSettingChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        // Performance settings require player recreation - recreate if currently connected
        if (d is not CameraTile tile)
        {
            return;
        }

        // Skip if value didn't actually change (safety for binding re-evaluation during layout switches)
        if (Equals(e.OldValue, e.NewValue))
        {
            return;
        }

        // Use Dispatcher to ensure we're on the UI thread and to defer the recreation
        // until after the binding updates have completed
        _ = tile.Dispatcher.BeginInvoke(() =>
        {
            if (tile is { ConnectionState: ConnectionState.Connected, Camera: not null })
            {
                tile.RecreatePlayer();
            }
        });
    }

    private static void OnBoundingBoxSettingsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not CameraTile tile)
        {
            return;
        }

        // Start or stop motion detection based on current settings
        // This handles the case where ShowBoundingBoxInGrid changes while camera is connected
        if (tile.ConnectionState != ConnectionState.Connected)
        {
            // Not connected - just apply settings for visual update
            tile.ApplyBoundingBoxSettings();
            return;
        }

        if (tile.ShouldRunMotionDetection)
        {
            // StartMotionDetection will call ApplyBoundingBoxSettings after starting
            tile.StartMotionDetection();
        }
        else
        {
            tile.StopMotionDetection();
            tile.ApplyBoundingBoxSettings();
        }
    }

    private static void OnRecordingOnMotionSettingChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not CameraTile tile)
        {
            return;
        }

        // Update overlay to show/hide recording indicator row
        tile.UpdateOverlayRecordingOnMotion();

        // Start or stop motion detection based on current settings
        // This handles the case where EnableRecordingOnMotion changes while camera is connected
        if (tile.ConnectionState == ConnectionState.Connected)
        {
            if (tile.ShouldRunMotionDetection)
            {
                tile.StartMotionDetection();
            }
            else
            {
                tile.StopMotionDetection();
            }
        }
    }

    private void OnCameraPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(CameraConfiguration.CanSwapLeft):
                case nameof(CameraConfiguration.CanSwapRight):
                    CommandManager.InvalidateRequerySuggested();
                    break;
                case nameof(CameraConfiguration.Display):
                    CameraName = Camera?.Display.DisplayName ?? string.Empty;
                    CameraDescription = Camera?.Display.Description ?? string.Empty;
                    if (Camera is not null)
                    {
                        UpdateOverlayPosition(GetEffectiveOverlayPosition(Camera));
                    }

                    break;
                case nameof(CameraConfiguration.EffectiveConnectionType):
                    // Display-only: no player recreation.
                    UpdateConnectionTypeIcon();
                    break;
                case nameof(CameraConfiguration.Connection):
                case nameof(CameraConfiguration.Authentication):
                case nameof(CameraConfiguration.Stream):
                    // These settings require player recreation to take effect
                    RecreatePlayer();
                    break;
                case nameof(CameraConfiguration.Overrides):
                    // Only reconnect if performance-related overrides changed
                    // Display overrides (overlay settings) don't require reconnection
                    HandleOverridesChanged();
                    break;
            }
        });
    }

    private void InitializeOverrideTracking(CameraConfiguration cameraConfig)
    {
        previousOverrideVideoQuality = cameraConfig.Overrides?.Performance.VideoQuality;
        previousOverrideHardwareAcceleration = cameraConfig.Overrides?.Performance.HardwareAcceleration;
        previousOverrideEnableRecordingOnMotion = cameraConfig.Overrides?.Recording.EnableRecordingOnMotion;
        previousOverrideShowBoundingBoxInGrid = cameraConfig.Overrides?.MotionDetection.BoundingBox.ShowInGrid;
        previousOverrideShowBoundingBoxInFullScreen = cameraConfig.Overrides?.MotionDetection.BoundingBox.ShowInFullScreen;
        previousOverrideBoundingBoxColor = cameraConfig.Overrides?.MotionDetection.BoundingBox.Color;
        previousOverrideBoundingBoxThickness = cameraConfig.Overrides?.MotionDetection.BoundingBox.Thickness;
        previousOverrideBoundingBoxMinArea = cameraConfig.Overrides?.MotionDetection.BoundingBox.MinArea;
        previousOverrideMotionSensitivity = cameraConfig.Overrides?.MotionDetection.Sensitivity;
        previousOverrideMotionMinimumChangePercent = cameraConfig.Overrides?.MotionDetection.MinimumChangePercent;
        previousOverrideMotionAnalysisFrameRate = cameraConfig.Overrides?.MotionDetection.AnalysisFrameRate;
        previousOverrideMotionAnalysisWidth = cameraConfig.Overrides?.MotionDetection.AnalysisWidth;
        previousOverrideMotionAnalysisHeight = cameraConfig.Overrides?.MotionDetection.AnalysisHeight;
        previousOverrideMotionCooldownSeconds = cameraConfig.Overrides?.MotionDetection.CooldownSeconds;
        previousOverridePostMotionDurationSeconds = cameraConfig.Overrides?.MotionDetection.PostMotionDurationSeconds;
        previousOverrideDetector = DetectorOverrides(cameraConfig);
    }

    private void HandleOverridesChanged()
    {
        var currentVideoQuality = Camera?.Overrides?.Performance.VideoQuality;
        var currentHardwareAcceleration = Camera?.Overrides?.Performance.HardwareAcceleration;

        var performanceChanged =
            currentVideoQuality != previousOverrideVideoQuality ||
            currentHardwareAcceleration != previousOverrideHardwareAcceleration;

        // Update tracked values
        previousOverrideVideoQuality = currentVideoQuality;
        previousOverrideHardwareAcceleration = currentHardwareAcceleration;

        if (performanceChanged)
        {
            // Performance settings changed - need to recreate player
            RecreatePlayer();
        }

        // Check for motion detection override changes
        var currentEnableRecordingOnMotion = Camera?.Overrides?.Recording.EnableRecordingOnMotion;
        var currentShowBoundingBoxInGrid = Camera?.Overrides?.MotionDetection.BoundingBox.ShowInGrid;
        var currentShowBoundingBoxInFullScreen = Camera?.Overrides?.MotionDetection.BoundingBox.ShowInFullScreen;
        var currentBoundingBoxColor = Camera?.Overrides?.MotionDetection.BoundingBox.Color;
        var currentBoundingBoxThickness = Camera?.Overrides?.MotionDetection.BoundingBox.Thickness;
        var currentBoundingBoxMinArea = Camera?.Overrides?.MotionDetection.BoundingBox.MinArea;
        var currentMotionSensitivity = Camera?.Overrides?.MotionDetection.Sensitivity;
        var currentMotionMinimumChangePercent = Camera?.Overrides?.MotionDetection.MinimumChangePercent;
        var currentMotionAnalysisFrameRate = Camera?.Overrides?.MotionDetection.AnalysisFrameRate;
        var currentMotionAnalysisWidth = Camera?.Overrides?.MotionDetection.AnalysisWidth;
        var currentMotionAnalysisHeight = Camera?.Overrides?.MotionDetection.AnalysisHeight;
        var currentMotionCooldownSeconds = Camera?.Overrides?.MotionDetection.CooldownSeconds;
        var currentPostMotionDurationSeconds = Camera?.Overrides?.MotionDetection.PostMotionDurationSeconds;
        var currentDetector = DetectorOverrides(Camera);

        var motionDetectionChanged =
            currentEnableRecordingOnMotion != previousOverrideEnableRecordingOnMotion ||
            currentShowBoundingBoxInGrid != previousOverrideShowBoundingBoxInGrid ||
            currentShowBoundingBoxInFullScreen != previousOverrideShowBoundingBoxInFullScreen ||
            currentBoundingBoxColor != previousOverrideBoundingBoxColor ||
            currentBoundingBoxThickness != previousOverrideBoundingBoxThickness ||
            currentBoundingBoxMinArea != previousOverrideBoundingBoxMinArea ||
            currentMotionSensitivity != previousOverrideMotionSensitivity ||
            !currentMotionMinimumChangePercent.IsEqual(previousOverrideMotionMinimumChangePercent) ||
            currentMotionAnalysisFrameRate != previousOverrideMotionAnalysisFrameRate ||
            currentMotionAnalysisWidth != previousOverrideMotionAnalysisWidth ||
            currentMotionAnalysisHeight != previousOverrideMotionAnalysisHeight ||
            currentMotionCooldownSeconds != previousOverrideMotionCooldownSeconds ||
            currentPostMotionDurationSeconds != previousOverridePostMotionDurationSeconds ||
            currentDetector != previousOverrideDetector;

        // Update tracked values
        previousOverrideEnableRecordingOnMotion = currentEnableRecordingOnMotion;
        previousOverrideShowBoundingBoxInGrid = currentShowBoundingBoxInGrid;
        previousOverrideShowBoundingBoxInFullScreen = currentShowBoundingBoxInFullScreen;
        previousOverrideBoundingBoxColor = currentBoundingBoxColor;
        previousOverrideBoundingBoxThickness = currentBoundingBoxThickness;
        previousOverrideBoundingBoxMinArea = currentBoundingBoxMinArea;
        previousOverrideMotionSensitivity = currentMotionSensitivity;
        previousOverrideMotionMinimumChangePercent = currentMotionMinimumChangePercent;
        previousOverrideMotionAnalysisFrameRate = currentMotionAnalysisFrameRate;
        previousOverrideMotionAnalysisWidth = currentMotionAnalysisWidth;
        previousOverrideMotionAnalysisHeight = currentMotionAnalysisHeight;
        previousOverrideMotionCooldownSeconds = currentMotionCooldownSeconds;
        previousOverrideDetector = currentDetector;
        previousOverridePostMotionDurationSeconds = currentPostMotionDurationSeconds;

        if (motionDetectionChanged)
        {
            // Always update overlay when EnableRecordingOnMotion override changes
            UpdateOverlayRecordingOnMotion();

            if (ConnectionState == ConnectionState.Connected)
            {
                // Motion detection settings changed - restart motion detection with new settings
                StopMotionDetection();

                if (ShouldRunMotionDetection)
                {
                    // StartMotionDetection will call ApplyBoundingBoxSettings after starting
                    StartMotionDetection();
                }
                else
                {
                    // If not running motion detection, still apply bounding box settings to hide the overlay
                    ApplyBoundingBoxSettings();
                }
            }
        }

        // Display overrides are handled via bindings automatically
        // No action needed here for overlay settings
    }

    /// <summary>
    /// Recreates the player with current settings. Used when performance settings change.
    /// </summary>
    public void RecreatePlayer()
    {
        if (Camera is null)
        {
            return;
        }

        // Release any active recording session tied to the old pipeline before
        // we tear it down. Otherwise RecordingService keeps a stale session
        // entry for this camera, and the new player's recording-on-connect
        // will be silently rejected (StartRecording bails on duplicate keys).
        if (recordingService is not null && recordingService.IsRecording(Camera.Id))
        {
            recordingService.StopRecording(Camera.Id);
        }

        // Keep reference to old player and pipeline
        var oldPlayer = Player;
        var oldPipeline = mediaPipeline;

        // Create new player with updated settings FIRST
        var newPlayer = CreatePlayer();
        newPlayer.StateChanged += OnPlayerStateChanged;

        // Swap to new player (without intermediate null state)
        Player = newPlayer;
        mediaPipeline = MediaPipelineFactory?.Invoke(newPlayer);

        // A new player starts unrotated.
        ApplyRotation();

        // Now cleanup old player and pipeline
        oldPipeline?.Dispose();

        if (oldPlayer is not null)
        {
            oldPlayer.StateChanged -= OnPlayerStateChanged;
            oldPlayer.Dispose();
        }

        // Use the reconnect flow for the new player (includes timeout handling)
        isReconnecting = true;
        UpdateConnectionState(ConnectionState.Connecting);

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            ReconnectPlayer);
    }

    private void OnCameraChangedInternal(CameraConfiguration? cameraConfig)
    {
        // During swap, the player is preserved - only update visual properties
        if (isSwapping)
        {
            if (cameraConfig is null)
            {
                CameraName = string.Empty;
                CameraDescription = string.Empty;
                return;
            }

            CameraName = cameraConfig.Display.DisplayName;
            CameraDescription = cameraConfig.Display.Description ?? string.Empty;
            UpdateOverlayPosition(GetEffectiveOverlayPosition(cameraConfig));
            UpdateConnectionTypeIcon();
            ApplyRotation();
            InitializeOverrideTracking(cameraConfig);
            return;
        }

        // Cleanup previous player and pipeline
        mediaPipeline?.Dispose();
        mediaPipeline = null;

        if (Player is not null)
        {
            Player.StateChanged -= OnPlayerStateChanged;
            Player.Dispose();
            Player = null;
        }

        if (cameraConfig is null)
        {
            CameraName = string.Empty;
            CameraDescription = string.Empty;
            reconnectAttemptCount = 0;
            previousOverrideVideoQuality = null;
            previousOverrideHardwareAcceleration = null;
            previousOverrideEnableRecordingOnMotion = null;
            previousOverrideShowBoundingBoxInGrid = null;
            previousOverrideShowBoundingBoxInFullScreen = null;
            previousOverrideBoundingBoxColor = null;
            previousOverrideBoundingBoxThickness = null;
            previousOverrideBoundingBoxMinArea = null;
            previousOverrideMotionSensitivity = null;
            previousOverrideMotionMinimumChangePercent = null;
            previousOverrideMotionAnalysisFrameRate = null;
            previousOverrideMotionAnalysisWidth = null;
            previousOverrideMotionAnalysisHeight = null;
            previousOverrideMotionCooldownSeconds = null;
            previousOverridePostMotionDurationSeconds = null;
            previousOverrideDetector = default;
            return;
        }

        // Initialize tracked override values for change detection
        InitializeOverrideTracking(cameraConfig);

        CameraName = cameraConfig.Display.DisplayName;
        CameraDescription = cameraConfig.Display.Description ?? string.Empty;

        UpdateOverlayPosition(GetEffectiveOverlayPosition(cameraConfig));
        UpdateConnectionTypeIcon();

        // Defer player creation if the factory hasn't been injected yet.
        // InitializeServices will call InitializePlayer when the factory arrives.
        if (videoPlayerFactory is null)
        {
            UpdateConnectionState(ConnectionState.Disconnected);
            return;
        }

        InitializePlayer();
    }

    private void InitializePlayer()
    {
        Player = CreatePlayer();
        Player.StateChanged += OnPlayerStateChanged;
        mediaPipeline = MediaPipelineFactory?.Invoke(Player);
        ApplyRotation();

        // Only auto-connect if enabled
        if (AutoConnectOnLoad)
        {
            // Use the same reconnect flow for initial connection
            // This ensures consistent timeout and retry behavior
            Reconnect();
        }
        else
        {
            // Stay disconnected until user manually connects
            UpdateConnectionState(ConnectionState.Disconnected);
        }
    }

    private IVideoPlayer CreatePlayer()
        => videoPlayerFactory!.Create();

    private StreamOptions BuildStreamOptions(CameraConfiguration camera)
    {
        var locator = camera.BuildSourceLocator();
        return new StreamOptions
        {
            Source = camera.Display.DisplayName,
            UseLowLatencyMode = camera.Stream.UseLowLatencyMode,
            MaxLatencyMs = camera.Stream.MaxLatencyMs,
            RtspTransport = camera.Stream.RtspTransport,
            BufferDurationMs = camera.Stream.BufferDurationMs,
            HardwareAcceleration = HardwareAcceleration,
            MaxVerticalResolution = DropDownItemsFactory.GetMaxResolutionFromQuality(VideoQuality),
            InputFormat = MapInputFormat(locator.InputFormat),
            RawDeviceSpec = locator.RawDeviceSpec,
            RecordAudio = locator.IncludesAudio,
            TranscodeRecordingToH264 = TranscodeVideoCodec == VideoTranscodeCodec.H264,
            VideoSize = locator.VideoSize,
            FrameRate = locator.FrameRate,
            PixelFormat = locator.PixelFormat,
        };
    }

    private static InputFormatKind MapInputFormat(string? name)
        => name switch
        {
            "dshow" => InputFormatKind.Dshow,
            "v4l2" => InputFormatKind.V4l2,
            "avfoundation" => InputFormatKind.AVFoundation,
            _ => InputFormatKind.Auto,
        };

    private void OnPlayerStateChanged(
        object? sender,
        PlayerStateChangedEventArgs e)
    {
        // Capture state immediately - don't re-read inside BeginInvoke
        var capturedState = e.NewState;
        var capturedReason = e.Reason;
        var reconnecting = isReconnecting;

        _ = Dispatcher.BeginInvoke(() =>
        {
            switch (capturedState)
            {
                case PlayerState.Playing:
                    isReconnecting = false;

                    // Reads are flowing again — clear any sticky device-busy
                    // hint from a prior failed open. The unplugged flag is
                    // owned by IUsbCameraWatcher and stays untouched here.
                    IsDeviceBusy = false;
                    UpdateConnectionState(ConnectionState.Connected);
                    break;
                case PlayerState.Opening:
                    UpdateConnectionState(ConnectionState.Connecting);
                    break;
                case PlayerState.Stopped:
                    // Skip Disconnected state during reconnection
                    // (player stops briefly before reopening)
                    if (!reconnecting)
                    {
                        UpdateConnectionState(ConnectionState.Disconnected);
                    }

                    break;
                case PlayerState.Error:
                    isReconnecting = false;
                    IsDeviceBusy = capturedReason == StreamFailureReason.DeviceBusy;
                    UpdateConnectionState(ConnectionState.ConnectionFailed, e.ErrorMessage);
                    break;
                default:
                    throw new SwitchCaseDefaultException(capturedState);
            }
        });
    }

    private void CacheOverlayReference()
    {
        if (VideoPlayer.Overlay is null)
        {
            return;
        }

        // Try to find CameraOverlay in overlay window content
        if (VideoPlayer.Overlay.Content is CameraOverlay directOverlay)
        {
            cachedOverlay = directOverlay;
        }
        else if (VideoPlayer.Overlay.Content is DependencyObject content)
        {
            // Search both visual and logical trees
            cachedOverlay = content
                .FindChildren<CameraOverlay>()
                .FirstOrDefault();
        }

        // If still not found, try to find in overlay window itself
        cachedOverlay ??= VideoPlayer.Overlay
            .FindChildren<CameraOverlay>()
            .FirstOrDefault();

        // If found, sync current state
        if (cachedOverlay is not null)
        {
            cachedOverlay.Title = CameraName;
            cachedOverlay.Description = CameraDescription;
            cachedOverlay.ConnectionState = ConnectionState;
            cachedOverlay.IsDeviceUnplugged = IsDeviceUnplugged;
            ApplyOverlaySettings();
            UpdateOverlayRecordingOnMotion();
        }

        // Cache motion bounding box overlay
        CacheMotionOverlayReference();
    }

    private void CacheMotionOverlayReference()
    {
        if (VideoPlayer.Overlay is null)
        {
            return;
        }

        // Try to find MotionBoundingBoxOverlay in overlay window content
        if (VideoPlayer.Overlay.Content is MotionBoundingBoxOverlay directOverlay)
        {
            cachedMotionOverlay = directOverlay;
        }
        else if (VideoPlayer.Overlay.Content is DependencyObject content)
        {
            // Search both visual and logical trees
            cachedMotionOverlay = content
                .FindChildren<MotionBoundingBoxOverlay>()
                .FirstOrDefault();
        }

        // If still not found, try to find in overlay window itself
        cachedMotionOverlay ??= VideoPlayer.Overlay
            .FindChildren<MotionBoundingBoxOverlay>()
            .FirstOrDefault();

        // If found, apply current settings
        if (cachedMotionOverlay is not null)
        {
            ApplyBoundingBoxSettings();
        }
    }

    private void ApplyOverlaySettings()
    {
        var overlay = GetCameraOverlay();
        if (overlay is null)
        {
            return;
        }

        overlay.ShowTitle = ShowOverlayTitle;
        overlay.ShowDescription = ShowOverlayDescription;
        overlay.ShowConnectionStatus = ShowOverlayConnectionStatus;
        overlay.ShowTime = ShowOverlayTime;
        overlay.OverlayOpacity = OverlayOpacity;
        UpdateConnectionTypeIcon();
    }

    /// <summary>
    /// Shows the connection-type icon (effective type, tinted by connection
    /// state) when enabled, and hands the legacy "USB" pill over to it: the
    /// pill only shows while the icon is off.
    /// </summary>
    private void UpdateConnectionTypeIcon()
    {
        var effectiveType = Camera?.EffectiveConnectionType ?? CameraConnectionType.None;

        if (ConnectionTypeIconControl is not null)
        {
            ConnectionTypeIconControl.ConnectionType = ShowOverlayConnectionTypeIcon
                ? effectiveType
                : CameraConnectionType.None;
            ConnectionTypeIconControl.ConnectionState = ConnectionState;
            ConnectionTypeIconControl.Opacity = OverlayOpacity;
        }

        UpdateOverlayIsUsbSource();
    }

    private void SubscribeToMouseEvents()
    {
        if (VideoPlayer.Overlay is not null)
        {
            VideoPlayer.Overlay.PreviewMouseRightButtonUp -= OnMouseRightButtonUp;
            VideoPlayer.Overlay.PreviewMouseRightButtonUp += OnMouseRightButtonUp;
            VideoPlayer.Overlay.PreviewMouseWheel -= OnOverlayMouseWheel;
            VideoPlayer.Overlay.PreviewMouseWheel += OnOverlayMouseWheel;
        }

        if (VideoPlayer.Surface is not null)
        {
            VideoPlayer.Surface.PreviewMouseRightButtonUp -= OnMouseRightButtonUp;
            VideoPlayer.Surface.PreviewMouseRightButtonUp += OnMouseRightButtonUp;
            VideoPlayer.Surface.PreviewMouseWheel -= OnOverlayMouseWheel;
            VideoPlayer.Surface.PreviewMouseWheel += OnOverlayMouseWheel;
        }

        EnsureHoverPollTimer();
    }

    /// <summary>
    /// Polling-based hover detection for the bottom-right corner zone.
    /// WPF mouse events on the VideoHost overlay surface don't fire
    /// reliably for hover-only scenarios (right-click and wheel are routed
    /// via Preview events that DO fire, but plain MouseEnter on the inner
    /// Grid was empirically broken). Mouse.GetPosition reads the current
    /// cursor position straight from Win32, so it's immune to whatever
    /// hit-test corner case is biting us. ~7 ticks/second per tile.
    /// </summary>
    private void EnsureHoverPollTimer()
    {
        if (hoverPollTimer is null)
        {
            hoverPollTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(150),
            };
            hoverPollTimer.Tick += OnHoverPollTick;
        }

        if (ShowOverlayQuickActions)
        {
            if (!hoverPollTimer.IsEnabled)
            {
                hoverPollTimer.Start();
            }
        }
        else if (hoverPollTimer.IsEnabled)
        {
            hoverPollTimer.Stop();

            if (isTileHovered)
            {
                isTileHovered = false;
                UpdateQuickActionVisibility();
            }
        }
    }

    private void OnHoverPollTick(
        object? sender,
        EventArgs e)
    {
        if (QuickActionsContainer is null || !QuickActionsContainer.IsLoaded)
        {
            return;
        }

        var width = QuickActionsContainer.ActualWidth;
        var height = QuickActionsContainer.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var pos = Mouse.GetPosition(QuickActionsContainer);
        var inside = pos.X >= 0 && pos.X <= width && pos.Y >= 0 && pos.Y <= height;

        if (inside == isTileHovered)
        {
            return;
        }

        isTileHovered = inside;
        UpdateQuickActionVisibility();
    }

    private static void OnShowOverlayQuickActionsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile)
        {
            tile.EnsureHoverPollTimer();
            tile.UpdateQuickActionVisibility();
        }
    }

    /// <summary>
    /// Recomputes visibility for the two quick-action buttons (zoom-reset,
    /// fullscreen). Called from every input that affects the inputs:
    /// hover events, zoom changes, connection state changes, and the
    /// ShowOverlayQuickActions DP changing.
    /// </summary>
    private void UpdateQuickActionVisibility()
    {
        if (FullScreenQuickButton is null || ZoomResetQuickButton is null)
        {
            return;
        }

        if (!ShowOverlayQuickActions)
        {
            // Feature disabled — collapse both buttons completely so their
            // space is reclaimed.
            FullScreenQuickButton.Visibility = Visibility.Collapsed;
            ZoomResetQuickButton.Visibility = Visibility.Collapsed;
            return;
        }

        // Fullscreen button: only meaningful when the camera is connected
        // (otherwise FullScreenCommand.CanExecute is false anyway), and only
        // shown while the user is hovering the tile. Use Hidden (not
        // Collapsed) when the show-conditions aren't met so the button's
        // 32x32 + margin slot stays reserved in the StackPanel — otherwise
        // the zoom-reset and zoom indicator below it would jump down each
        // time the cursor approaches in top-anchored mode (where the stack
        // grows downward instead of upward).
        FullScreenQuickButton.Visibility =
            isTileHovered &&
            !isReconnecting &&
            ConnectionState == ConnectionState.Connected
                ? Visibility.Visible
                : Visibility.Hidden;

        // Zoom-reset: only meaningful when zoomed; visible regardless of
        // hover so the user can always recover the default view.
        ZoomResetQuickButton.Visibility = IsZoomed
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateConnectionState(
        ConnectionState newState,
        string? errorMessage = null)
    {
        if (ConnectionState == newState)
        {
            return;
        }

        // The state being left. (This used to report the state before that
        // one, so listeners saw 'Disconnected -> Connected' for a camera that
        // was 'Connecting', and a drop from 'Connected' was not recognised as
        // a disconnect by the notification below.)
        var previous = ConnectionState;
        ConnectionState = newState;

        // Force update overlay directly
        var overlay = GetCameraOverlay();
        if (overlay is not null)
        {
            overlay.ConnectionState = newState;
        }

        if (Camera is not null)
        {
            ConnectionStateChanged?.Invoke(
                this,
                new CameraConnectionChangedEventArgs(Camera, previous, newState, errorMessage));
        }

        // Handle notifications and auto-reconnect
        HandleConnectionStateChange(previous, newState);
    }

    private void HandleConnectionStateChange(
        ConnectionState previousState,
        ConnectionState newState)
    {
        // Handle notifications
        if (newState == ConnectionState.Disconnected || newState == ConnectionState.ConnectionFailed)
        {
            if (previousState == ConnectionState.Connected && ShowNotificationOnDisconnect)
            {
                ShowNotification(
                    string.Format(CultureInfo.CurrentCulture, Translations.CameraDisconnected1, Camera?.Display.DisplayName ?? string.Empty));
            }

            // Stop stream health monitoring
            StopStreamHealthCheck();

            // Stop motion detection when disconnected
            StopMotionDetection();

            // Stop timelapse capture when disconnected
            StopTimelapse();

            // Try auto-reconnect on failure (but not on manual disconnect)
            if (newState == ConnectionState.ConnectionFailed)
            {
                TryAutoReconnect();
            }
        }
        else if (newState == ConnectionState.Connected)
        {
            // Successful connection — reset backoff so the next failure series starts fresh.
            reconnectAttemptCount = 0;

            if (previousState is ConnectionState.Disconnected or ConnectionState.ConnectionFailed or ConnectionState.Connecting
                && ShowNotificationOnReconnect)
            {
                ShowNotification(
                    string.Format(CultureInfo.CurrentCulture, Translations.CameraConnected1, Camera?.Display.DisplayName ?? string.Empty));
            }

            // Start stream health monitoring to detect if stream goes offline
            StartStreamHealthCheck();

            // Auto-start recording on connect if enabled (respects per-camera override)
            if (GetEffectiveEnableRecordingOnConnect() &&
                recordingService is not null &&
                mediaPipeline is not null &&
                Camera is not null &&
                RecordingState == RecordingState.Idle)
            {
                LogRecordingOnConnectStarting(Camera.Display.DisplayName);
                recordingService.StartRecording(Camera, mediaPipeline);
            }
            else if (GetEffectiveEnableRecordingOnConnect() && Camera is not null)
            {
                LogRecordingOnConnectSkipped(Camera.Display.DisplayName, mediaPipeline is not null, recordingService is not null, RecordingState);
            }

            // Auto-start motion detection if enabled (for motion-triggered recording or bounding box display)
            if (ShouldRunMotionDetection)
            {
                StartMotionDetection();
            }

            // Auto-start timelapse capture if enabled
            StartTimelapse();
        }
    }

    private void TryAutoReconnect()
    {
        // Use effective value to respect per-camera override
        if (!GetEffectiveAutoReconnectOnFailure() || Camera is null)
        {
            isReconnecting = false; // Clear flag since we won't reconnect
            return;
        }

        reconnectAttemptCount++;

        // Exponential backoff: base delay doubles each consecutive failure, capped at 15 min.
        var delay = ReconnectBackoff.ComputeDelay(
            reconnectAttemptCount,
            TimeSpan.FromSeconds(GetEffectiveReconnectDelaySeconds()));

        LogAutoReconnectScheduled(Camera.Display.DisplayName, (int)delay.TotalSeconds);

        autoReconnectTimer?.Stop();
        autoReconnectTimer = new DispatcherTimer
        {
            Interval = delay,
        };

        autoReconnectTimer.Tick += (_, _) =>
        {
            autoReconnectTimer?.Stop();

            // Only reconnect if still in failed state
            if (ConnectionState == ConnectionState.ConnectionFailed && Camera is not null)
            {
                LogAutoReconnectAttempting(Camera.Display.DisplayName);
                Reconnect();
            }
        };

        autoReconnectTimer.Start();
    }

    private void StartStreamHealthCheck()
    {
        StopStreamHealthCheck();

        if (Player is null)
        {
            return;
        }

        // Initialize tracking - use -1 to skip first check
        // This gives the stream time to establish
        lastFramesDisplayed = -1;
        staleStreamCheckCount = 0;

        // Check stream health every 5 seconds
        streamHealthTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        streamHealthTimer.Tick += OnStreamHealthTimerTick;
        streamHealthTimer.Start();
    }

    private void StopStreamHealthCheck()
    {
        if (streamHealthTimer is not null)
        {
            streamHealthTimer.Stop();
            streamHealthTimer.Tick -= OnStreamHealthTimerTick;
            streamHealthTimer = null;
        }

        staleStreamCheckCount = 0;
        lastFramesDisplayed = -1;
    }

    private void OnStreamHealthTimerTick(
        object? sender,
        EventArgs e)
    {
        // Don't check during reconnection
        if (Player is null || ConnectionState != ConnectionState.Connected || isReconnecting)
        {
            StopStreamHealthCheck();
            return;
        }

        var playerState = Player.State;
        var currentFps = Player.CurrentFps;
        var currentFrames = Player.FramesDecoded;

        // Only check if player is in a state where we expect video
        if (playerState != PlayerState.Playing)
        {
            return;
        }

        // First check - skip to allow stream to establish
        if (lastFramesDisplayed < 0)
        {
            lastFramesDisplayed = currentFrames;
            return;
        }

        // Detect stale stream using two indicators:
        // 1. FPS is very low (< 1) - indicates no/few frames being decoded
        // 2. FramesDisplayed not advancing - DWM not presenting new frames
        // Both must be true to avoid false positives
        var fpsIsLow = currentFps < 1.0;
        var framesNotAdvancing = currentFrames == lastFramesDisplayed;

        // Stream is stale if FPS is low AND frames aren't advancing
        var isStale = fpsIsLow && framesNotAdvancing;

        if (isStale)
        {
            staleStreamCheckCount++;

            // If stale for 3 consecutive checks (15 seconds), trigger reconnect
            if (staleStreamCheckCount >= 3)
            {
                LogStreamStaleDetected(Camera?.Display.DisplayName ?? "Unknown");

                StopStreamHealthCheck();

                // Set reconnecting flag to prevent OnPlayerStateChanged from
                // changing state when we stop the player. Keep it true until
                // the auto-reconnect timer fires to ignore all state changes.
                isReconnecting = true;

                // Stop the player to prevent auto-recovering
                try
                {
                    Player?.Close();
                }
                catch
                {
                    // Ignore stop errors
                }

                // Update state to ConnectionFailed (keep isReconnecting = true)
                // The flag will be managed by TryAutoReconnect/Reconnect
                UpdateConnectionState(ConnectionState.ConnectionFailed, "Stream stopped responding");
            }
        }
        else
        {
            // Stream is healthy - reset counter
            staleStreamCheckCount = 0;
        }

        lastFramesDisplayed = currentFrames;
    }

    private void ShowNotification(string message)
    {
        if (PlayNotificationSound)
        {
            PlaySystemNotificationSound();
        }

        // Show Windows toast notification
        ShowToastNotification(message);
    }

    private static void PlaySystemNotificationSound()
    {
        try
        {
            SystemSounds.Exclamation.Play();
        }
        catch
        {
            // Ignore sound playback errors
        }
    }

    private void ShowToastNotification(string message)
    {
        if (toastNotificationService is null)
        {
            return;
        }

        toastNotificationService.ShowInformation(
            Camera?.Display.DisplayName ?? Translations.ApplicationTitle,
            message,
            useDesktop: true,
            expirationTime: TimeSpan.FromSeconds(5));
    }

    private void UpdateOverlayConnectionState(ConnectionState state)
    {
        var overlay = GetCameraOverlay();
        overlay?.ConnectionState = state;
    }

    private void UpdateOverlayDescription(string description)
    {
        var overlay = GetCameraOverlay();
        overlay?.Description = description;
    }

    /// <summary>
    /// Pushes the current camera's rotation through the media pipeline so the
    /// VideoProcessor + recording metadata both reflect the latest setting.
    /// Safe to call before the pipeline is created or before a stream is open.
    /// </summary>
    private void ApplyRotation()
    {
        var rotation = Camera?.Display.Rotation ?? CameraRotation.None;
        mediaPipeline?.SetRotation(rotation);

        // Boxes found before the turn are in the old orientation.
        if (rotation != appliedRotation)
        {
            appliedRotation = rotation;
            GetMotionBoundingBoxOverlay()?.HideBoundingBoxes();
        }
    }

    /// <summary>
    /// Re-reads display-time properties from the bound <see cref="Camera"/>
    /// without recreating the player. Used after the Edit Camera dialog mutates
    /// the camera in place (since the <c>Camera</c> dependency property reference
    /// is unchanged, the change-callback doesn't fire on its own).
    /// </summary>
    public void RefreshFromCameraSettings()
    {
        if (Camera is null)
        {
            return;
        }

        CameraName = Camera.Display.DisplayName;
        CameraDescription = Camera.Display.Description ?? string.Empty;
        UpdateOverlayPosition(GetEffectiveOverlayPosition(Camera));
        UpdateConnectionTypeIcon();
        ApplyRotation();
    }

    /// <summary>
    /// Builds the "Rotate" submenu shown in the camera tile's context menu.
    /// The current rotation is shown with a checkmark; selecting another value
    /// invokes <see cref="RotateCommand"/>.
    /// </summary>
    private MenuItem BuildRotateMenu()
    {
        var current = Camera?.Display.Rotation ?? CameraRotation.None;
        var menu = new MenuItem { Header = Translations.Rotate };
        AddRotateItem(menu, Translations.RotationNone, CameraRotation.None, current);
        AddRotateItem(menu, Translations.RotationRotate90Degrees, CameraRotation.Rotate90, current);
        AddRotateItem(menu, Translations.RotationRotate180Degrees, CameraRotation.Rotate180, current);
        AddRotateItem(menu, Translations.RotationRotate270Degrees, CameraRotation.Rotate270, current);
        return menu;
    }

    private void AddRotateItem(
        MenuItem parent,
        string header,
        CameraRotation rotation,
        CameraRotation current)
    {
        parent.Items.Add(new MenuItem
        {
            Header = header,
            IsChecked = rotation == current,
            IsCheckable = true,
            StaysOpenOnClick = false,
            Command = RotateCommand,
            CommandParameter = rotation,
        });
    }

    /// <summary>
    /// Returns the effective overlay position for this camera, preferring
    /// the per-camera override (set in the Overrides tab) when present and
    /// falling back to the basic display setting otherwise.
    /// </summary>
    private static OverlayPosition GetEffectiveOverlayPosition(
        CameraConfiguration cameraConfig)
        => cameraConfig.Overrides?.CameraDisplay.OverlayPosition ?? cameraConfig.Display.OverlayPosition;

    private void UpdateOverlayPosition(OverlayPosition position)
    {
        currentOverlayPosition = position;

        // Connection-type icon: one of the two corners adjacent to the info box,
        // so it never collides with the info box or the quick actions.
        var iconCorner = CameraConnectionTypeHelper.ResolveIconCorner(position, ConnectionTypeIconPlacement);
        ConnectionTypeIconHorizontalAlignment = iconCorner is OverlayPosition.TopLeft or OverlayPosition.BottomLeft
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        ConnectionTypeIconVerticalAlignment = iconCorner is OverlayPosition.TopLeft or OverlayPosition.TopRight
            ? VerticalAlignment.Top
            : VerticalAlignment.Bottom;

        if (ConnectionTypeIconControl is not null)
        {
            ConnectionTypeIconControl.HorizontalAlignment = ConnectionTypeIconHorizontalAlignment;
            ConnectionTypeIconControl.VerticalAlignment = ConnectionTypeIconVerticalAlignment;
        }

        OverlayHorizontalAlignment = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft => HorizontalAlignment.Left,
            OverlayPosition.TopRight or OverlayPosition.BottomRight => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };

        OverlayVerticalAlignment = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopRight => VerticalAlignment.Top,
            OverlayPosition.BottomLeft or OverlayPosition.BottomRight => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Top,
        };

        // Quick-action stack lives in the diagonal opposite corner.
        QuickActionsHorizontalAlignment = OverlayHorizontalAlignment == HorizontalAlignment.Left
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;

        QuickActionsVerticalAlignment = OverlayVerticalAlignment == VerticalAlignment.Top
            ? VerticalAlignment.Bottom
            : VerticalAlignment.Top;
    }

    private void UpdateOverlayTitle(string title)
    {
        var overlay = GetCameraOverlay();
        overlay?.Title = title;
    }

    private void UpdateOverlayIsUsbSource()
    {
        var overlay = GetCameraOverlay();
        if (overlay is not null)
        {
            // Legacy "USB" pill — superseded by the connection-type icon while
            // that is enabled. Scheduled for removal (connection-icon roadmap, Phase 8).
            overlay.IsUsbSource = Camera?.Connection.Source == CameraSource.Usb &&
                                  !ShowOverlayConnectionTypeIcon;
        }
    }

    private CameraOverlay? GetCameraOverlay()
    {
        // Return cached reference if available
        if (cachedOverlay is not null)
        {
            return cachedOverlay;
        }

        // Try to cache it now
        CacheOverlayReference();
        return cachedOverlay;
    }

    private void ReconnectPlayer()
    {
        if (Camera is null || Player is null)
        {
            return;
        }

        // Capture references
        var capturedPlayer = Player;
        var capturedCamera = Camera;

        // Close the player before reopening
        try
        {
            capturedPlayer.Close();
        }
        catch
        {
            // Ignore Close() errors - we'll try to Open() anyway
        }

        // Build locator and options on the UI thread (they access DependencyProperties)
        var locator = capturedCamera.BuildSourceLocator();
        var uri = locator.Uri;
        var options = BuildStreamOptions(capturedCamera);

        // Start status check timer before background operation
        StartReconnectStatusCheck();

        // Run Open on background thread to keep UI responsive
        _ = Task.Run(() =>
        {
            try
            {
                capturedPlayer.Open(uri, options);
            }
            catch (Exception ex)
            {
                _ = Dispatcher.BeginInvoke(() =>
                {
                    isReconnecting = false;
                    UpdateConnectionState(ConnectionState.ConnectionFailed, ex.Message);
                });
            }
        });
    }

    private void StartReconnectStatusCheck()
    {
        // Stop any existing timer
        reconnectTimer?.Stop();

        // Reset check count (use instance field to avoid closure issues)
        reconnectCheckCount = 0;

        // Create timer on UI thread
        reconnectTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };

        reconnectTimer.Tick += OnReconnectTimerTick;
        reconnectTimer.Start();
    }

    private void OnReconnectTimerTick(
        object? sender,
        EventArgs e)
    {
        reconnectCheckCount++;

        // Calculate max checks (at least 10 seconds)
        var maxChecks = Math.Max(ConnectionTimeoutSeconds * 2, 20);

        if (!isReconnecting)
        {
            // Already handled by Status property change
            reconnectTimer?.Stop();
            return;
        }

        if (Player?.State == PlayerState.Playing)
        {
            isReconnecting = false;
            reconnectTimer?.Stop();
            UpdateConnectionState(ConnectionState.Connected);
            return;
        }

        if (Player?.State == PlayerState.Error || reconnectCheckCount >= maxChecks)
        {
            LogConnectionTimeout(Camera?.Display.DisplayName ?? "Unknown", reconnectCheckCount / 2);

            isReconnecting = false;
            reconnectTimer?.Stop();
            UpdateConnectionState(ConnectionState.ConnectionFailed, Translations.ConnectionTimedOut);
        }
    }

    private bool CanExecuteCameraCommand()
        => Camera is not null;

    [RelayCommand(CanExecute = nameof(CanExecuteCameraCommand))]
    private void EditCamera()
    {
        if (Camera is not null)
        {
            EditCameraRequested?.Invoke(this, Camera);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCameraCommand))]
    private void DeleteCamera()
    {
        if (Camera is not null)
        {
            DeleteCameraRequested?.Invoke(this, Camera);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteWhenConnected))]
    private void FullScreen()
    {
        if (Camera is not null)
        {
            FullScreenRequested?.Invoke(this, new FullScreenRequestedEventArgs(Camera, this));
        }
    }

    private bool CanExecuteSwapLeft()
        => Camera is { CanSwapLeft: true };

    [RelayCommand(CanExecute = nameof(CanExecuteSwapLeft))]
    private void SwapLeft()
    {
        if (Camera is not null)
        {
            SwapLeftRequested?.Invoke(this, Camera);
        }
    }

    private bool CanExecuteSwapRight()
        => Camera is { CanSwapRight: true };

    [RelayCommand(CanExecute = nameof(CanExecuteSwapRight))]
    private void SwapRight()
    {
        if (Camera is not null)
        {
            SwapRightRequested?.Invoke(this, Camera);
        }
    }

    private bool CanExecuteWhenConnected()
        => Camera is not null &&
           ConnectionState == ConnectionState.Connected;

    /// <summary>
    /// Takes the picture the tile shows now, turned by the camera's rotation, without saving it.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The picture as PNG, or <see langword="null"/> when the tile shows none.</returns>
    public Task<byte[]?> CapturePictureAsync(
        CancellationToken cancellationToken = default)
        => CanExecuteWhenConnected() && Player is { } shownPlayer
            ? shownPlayer.CaptureFrameAsync(cancellationToken)
            : Task.FromResult<byte[]?>(null);

    [RelayCommand("Snapshot", CanExecute = nameof(CanExecuteWhenConnected))]
    private async Task SnapshotAsync()
    {
        if (Player is null || Camera is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = Translations.SaveSnapshot,
            Filter = "PNG Image|*.png|JPEG Image|*.jpg",
            FileName = $"{Camera.Display.DisplayName}_{DateTime.Now:yyyyMMdd_HHmmss}",
        };

        // Use configured snapshot path as initial directory if available
        if (!string.IsNullOrEmpty(SnapshotPath))
        {
            // Create the directory if it doesn't exist
            if (!Directory.Exists(SnapshotPath))
            {
                try
                {
                    Directory.CreateDirectory(SnapshotPath);
                }
                catch
                {
                    // If we can't create the directory, fall back to default behavior
                }
            }

            if (Directory.Exists(SnapshotPath))
            {
                dialog.InitialDirectory = SnapshotPath;
            }
        }

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var frameData = await Player.CaptureFrameAsync().ConfigureAwait(false);
                if (frameData is not null)
                {
                    await File.WriteAllBytesAsync(dialog.FileName, frameData).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogSnapshotFailed(ex);
            }
        }
    }

    [RelayCommand(nameof(Reconnect), CanExecute = nameof(CanExecuteCameraCommand))]
    private void ExecuteReconnect()
    {
        Reconnect();
    }

    private bool CanExecuteStartRecording()
        => Camera is not null &&
           ConnectionState == ConnectionState.Connected &&
           RecordingState == RecordingState.Idle &&
           recordingService is not null;

    [RelayCommand(CanExecute = nameof(CanExecuteStartRecording))]
    private void StartRecording()
    {
        if (Camera is null || mediaPipeline is null || recordingService is null)
        {
            return;
        }

        recordingService.StartRecording(Camera, mediaPipeline);
    }

    private bool CanExecuteStopRecording()
        => Camera is not null &&
           RecordingState != RecordingState.Idle &&
           recordingService is not null;

    [RelayCommand(CanExecute = nameof(CanExecuteStopRecording))]
    private void StopRecording()
    {
        if (Camera is null || recordingService is null)
        {
            return;
        }

        recordingService.StopRecording(Camera.Id);
    }

    private void OnServiceRecordingStateChanged(
        object? sender,
        RecordingStateChangedEventArgs e)
    {
        // Marshal to the dispatcher BEFORE touching DependencyProperties.
        // RecordingService now fires this event from a thread-pool thread
        // (RecordingSegmentationService runs on a System.Threading.Timer),
        // so any DP access on the calling thread throws.
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (Camera is null || e.CameraId != Camera.Id)
            {
                return;
            }

            RecordingState = e.NewState;
            UpdateRecordingDurationTimer();
            UpdateOverlayRecordingState();

            RecordingStateChangedEvent?.Invoke(this, e);
            CommandManager.InvalidateRequerySuggested();
        });
    }

    private void OnMotionDetected(
        object? sender,
        MotionDetectedEventArgs e)
    {
        // Marshal to the dispatcher BEFORE touching DependencyProperties.
        // MotionDetectionService fires from a thread-pool thread.
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (Camera is null || e.CameraId != Camera.Id)
            {
                return;
            }

            // Update motion state based on IsMotionActive
            var wasMotionDetected = IsMotionDetected;
            IsMotionDetected = e.IsMotionActive;
            UpdateOverlayMotionIndicator();

            // Update bounding box overlay
            if (e is { IsMotionActive: true, HasBoundingBoxes: true })
            {
                UpdateMotionBoundingBoxes(e.BoundingBoxes, e.AnalysisWidth, e.AnalysisHeight);
            }
            else
            {
                UpdateMotionBoundingBoxes(boundingBoxes: null, e.AnalysisWidth, e.AnalysisHeight);
            }

            // Trigger motion recording if enabled and motion is active (use effective value for override)
            if (e.IsMotionActive && GetEffectiveEnableRecordingOnMotion() && recordingService is not null && mediaPipeline is not null)
            {
                recordingService.TriggerMotionRecording(Camera, mediaPipeline);
            }

            // If motion just stopped, schedule a delayed check to hide the bounding box
            if (wasMotionDetected && !e.IsMotionActive)
            {
                // Reset motion indicator after the configured post-motion duration
                var postMotionSeconds = GetEffectiveMotionPostDurationSeconds();
                var resetTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(postMotionSeconds),
                };
                resetTimer.Tick += (_, _) =>
                {
                    resetTimer.Stop();
                    if (motionDetectionService is null || !motionDetectionService.IsMotionDetected(Camera?.Id ?? Guid.Empty))
                    {
                        IsMotionDetected = false;
                        UpdateOverlayMotionIndicator();
                        UpdateMotionBoundingBoxes(boundingBoxes: null);
                    }
                };
                resetTimer.Start();
            }
        });
    }

    private static void OnRecordingStateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTile tile && e.NewValue is RecordingState)
        {
            tile.UpdateOverlayRecordingState();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void UpdateRecordingDurationTimer()
    {
        if (RecordingState == RecordingState.Idle)
        {
            // Stop timer
            recordingDurationTimer?.Stop();
            recordingDurationTimer = null;
            RecordingDuration = TimeSpan.Zero;
        }
        else if (recordingDurationTimer is null)
        {
            // Start timer
            recordingDurationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            recordingDurationTimer.Tick += (_, _) => UpdateRecordingDuration();
            recordingDurationTimer.Start();
            UpdateRecordingDuration();
        }
    }

    private void UpdateRecordingDuration()
    {
        if (Camera is null || recordingService is null)
        {
            return;
        }

        var session = recordingService.GetSession(Camera.Id);
        if (session is not null)
        {
            RecordingDuration = session.Duration;

            // Update overlay with new duration
            var overlay = GetCameraOverlay();
            if (overlay is not null)
            {
                overlay.RecordingDuration = RecordingDuration;
            }
        }
    }

    private void UpdateOverlayRecordingState()
    {
        var overlay = GetCameraOverlay();
        if (overlay is not null)
        {
            overlay.IsRecording = RecordingState != RecordingState.Idle;
            overlay.RecordingDuration = RecordingDuration;
        }
    }

    private void UpdateOverlayMotionIndicator()
    {
        var overlay = GetCameraOverlay();
        if (overlay is not null)
        {
            overlay.IsMotionDetected = IsMotionDetected;
        }
    }

    private void UpdateOverlayRecordingOnMotion()
    {
        var overlay = GetCameraOverlay();
        if (overlay is not null)
        {
            overlay.EnableRecordingOnMotion = GetEffectiveEnableRecordingOnMotion();
        }
    }

    private void ApplyBoundingBoxSettings()
    {
        var motionOverlay = GetMotionBoundingBoxOverlay();
        if (motionOverlay is null)
        {
            return;
        }

        // Use effective values that consider per-camera overrides
        var effectiveShowInGrid = GetEffectiveShowBoundingBoxInGrid();
        var effectiveColor = GetEffectiveBoundingBoxColor();
        var effectiveThickness = GetEffectiveBoundingBoxThickness();
        var effectiveSmoothing = GetEffectiveBoundingBoxSmoothing();

        motionOverlay.IsOverlayEnabled = effectiveShowInGrid;
        motionOverlay.BoxColor = effectiveColor;
        motionOverlay.BoxThickness = effectiveThickness;
        motionOverlay.SmoothingFactor = effectiveSmoothing;

        // Set analysis resolution from motion detection service if available
        if (motionDetectionService is not null && Camera is not null)
        {
            var (width, height) = motionDetectionService.GetAnalysisResolution(Camera.Id);
            motionOverlay.AnalysisWidth = width;
            motionOverlay.AnalysisHeight = height;
        }
    }

    private MotionBoundingBoxOverlay? GetMotionBoundingBoxOverlay()
    {
        // Return cached reference if available
        if (cachedMotionOverlay is not null)
        {
            return cachedMotionOverlay;
        }

        // Try to cache it now
        CacheMotionOverlayReference();
        return cachedMotionOverlay;
    }

    private void UpdateMotionBoundingBoxes(
        IReadOnlyList<BoundingBox>? boundingBoxes,
        int analysisWidth = 320,
        int analysisHeight = 240)
    {
        var motionOverlay = GetMotionBoundingBoxOverlay();
        if (motionOverlay is null)
        {
            LogMotionOverlayNotFound(Camera?.Display.DisplayName ?? "<no-camera>");
            return;
        }

        // Update the overlay's analysis resolution to match the event's resolution
        // This ensures correct coordinate scaling
        motionOverlay.AnalysisWidth = analysisWidth;
        motionOverlay.AnalysisHeight = analysisHeight;

        // Set the video stream dimensions for letterbox-aware coordinate mapping
        // Boxes are found in the rotated picture, so letterbox with the
        // displayed size, not the stream's own (unrotated) size.
        var streamInfo = Player?.StreamInfo;
        if (streamInfo is { Width: > 0, Height: > 0 })
        {
            var (displayWidth, displayHeight) = MotionBoxMapper.GetDisplaySize(
                streamInfo.Width,
                streamInfo.Height,
                Camera?.Display.Rotation ?? CameraRotation.None);
            motionOverlay.VideoWidth = displayWidth;
            motionOverlay.VideoHeight = displayHeight;
        }

        // Get the video container size for coordinate mapping
        var containerSize = new Size(VideoPlayer.ActualWidth, VideoPlayer.ActualHeight);
        if ((containerSize.Width <= 0 || containerSize.Height <= 0) &&
            VideoPlayer.Overlay is not null)
        {
            // Try to get size from the overlay window
            containerSize = new Size(VideoPlayer.Overlay.ActualWidth, VideoPlayer.Overlay.ActualHeight);
        }

        motionOverlay.UpdateMotionBoxes(boundingBoxes, containerSize);
    }

    /// <summary>
    /// Starts motion detection for this camera if enabled (for recording or bounding box display).
    /// </summary>
    public void StartMotionDetection()
    {
        if (Camera is null || mediaPipeline is null || motionDetectionService is null || !ShouldRunMotionDetection)
        {
            return;
        }

        // Build settings using effective values that consider per-camera overrides
        var settings = new MotionDetectionSettings
        {
            Sensitivity = GetEffectiveMotionSensitivity(),
            SmallestObjectPercent = GetEffectiveMotionSmallestObjectPercent(),
            StoppedObjectSeconds = GetEffectiveMotionStoppedObjectSeconds(),
            IgnoreLightingChanges = GetEffectiveMotionIgnoreLightingChanges(),
            MinimumChangePercent = GetEffectiveMotionMinimumChangePercent(),
            AnalysisFrameRate = GetEffectiveMotionAnalysisFrameRate(),
            AnalysisWidth = GetEffectiveMotionAnalysisWidth(),
            PostMotionDurationSeconds = GetEffectiveMotionPostDurationSeconds(),
            CooldownSeconds = GetEffectiveMotionCooldownSeconds(),
            BoundingBox = new BoundingBoxSettings
            {
                ShowInGrid = GetEffectiveShowBoundingBoxInGrid(),
                ShowInFullScreen = GetEffectiveShowBoundingBoxInFullScreen(),
                Color = GetEffectiveBoundingBoxColor(),
                Thickness = GetEffectiveBoundingBoxThickness(),
                MinArea = GetEffectiveBoundingBoxMinArea(),
                Padding = GetEffectiveBoundingBoxPadding(),
                Smoothing = GetEffectiveBoundingBoxSmoothing(),
            },
        };

        motionDetectionService.StartDetection(Camera.Id, mediaPipeline, settings, Camera.MotionZones);

        // Apply bounding box settings AFTER starting detection so the overlay gets the correct resolution
        ApplyBoundingBoxSettings();
    }

    /// <summary>
    /// Stops motion detection for this camera.
    /// </summary>
    public void StopMotionDetection()
    {
        if (Camera is null || motionDetectionService is null)
        {
            return;
        }

        motionDetectionService.StopDetection(Camera.Id);
    }

    /// <summary>
    /// Starts timelapse capture for this camera if enabled.
    /// </summary>
    public void StartTimelapse()
    {
        if (Camera is null || mediaPipeline is null || timelapseService is null)
        {
            return;
        }

        if (!timelapseService.GetEffectiveEnabled(Camera))
        {
            return;
        }

        timelapseService.StartCapture(Camera, mediaPipeline);
    }

    /// <summary>
    /// Stops timelapse capture for this camera.
    /// </summary>
    public void StopTimelapse()
    {
        if (Camera is null || timelapseService is null)
        {
            return;
        }

        timelapseService.StopCapture(Camera.Id);
    }

    private void OnDragCaptureLayerMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        dragStartPoint = e.GetPosition(this);

        // Handle double-click: reset zoom if zoomed, otherwise fullscreen
        if (e.ClickCount == 2 && Camera is not null && ConnectionState == ConnectionState.Connected)
        {
            if (IsZoomed)
            {
                ResetZoom();
            }
            else
            {
                FullScreenRequested?.Invoke(this, new FullScreenRequestedEventArgs(Camera, this));
            }

            e.Handled = true;
        }
    }

    private void OnDragCaptureLayerMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || isDragging || Camera is null)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        var diff = dragStartPoint - currentPosition;

        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        isDragging = true;
        DragDrop.DoDragDrop(this, Camera, DragDropEffects.Move);
        isDragging = false;
    }

    private void OnDragCaptureLayerDragOver(
        object sender,
        DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(CameraConfiguration)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDragCaptureLayerDrop(
        object sender,
        DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(CameraConfiguration)))
        {
            return;
        }

        var sourceCamera = (CameraConfiguration)e.Data.GetData(typeof(CameraConfiguration))!;
        if (Camera is null || sourceCamera.Id == Camera.Id)
        {
            return;
        }

        CameraDropped?.Invoke(this, sourceCamera);
        e.Handled = true;
    }

    private void OnOverlayMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        // Ctrl+Scroll = zoom toward mouse pointer
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        var wasZoomed = IsZoomed;
        var oldZoom = currentZoom;
        var factor = e.Delta > 0 ? 1.15f : 0.87f;
        var newZoom = Math.Clamp(currentZoom * factor, 1.0f, 10.0f);

        // Adjust pan so the point under the mouse stays fixed
        if (sender is Window window && newZoom > 1.01f)
        {
            var mousePos = e.GetPosition(window);
            var mx = (float)(mousePos.X / window.ActualWidth);
            var my = (float)(mousePos.Y / window.ActualHeight);

            // Convert mouse position to normalized offset from center (-1..+1)
            var mxNorm = (mx - 0.5f) * 2f;
            var myNorm = (my - 0.5f) * 2f;

            // Shift pan so content under mouse stays stationary
            currentPanX += mxNorm * (1f - (oldZoom / newZoom));
            currentPanY += myNorm * (1f - (oldZoom / newZoom));
            currentPanX = Math.Clamp(currentPanX, -1f, 1f);
            currentPanY = Math.Clamp(currentPanY, -1f, 1f);
        }

        currentZoom = newZoom;

        if (currentZoom <= 1.01f)
        {
            // Snap to fit
            currentZoom = 1.0f;
            currentPanX = 0f;
            currentPanY = 0f;
        }

        VideoPlayer.SetZoom(currentZoom, currentPanX, currentPanY);
        UpdateZoomVisuals();

        if (wasZoomed != IsZoomed)
        {
            ZoomStateChanged?.Invoke(this, EventArgs.Empty);
        }

        e.Handled = true;
    }

    private void OnPreProcessInput(
        object sender,
        PreProcessInputEventArgs e)
    {
        if (disposed)
        {
            return;
        }

        switch (e.StagingItem.Input)
        {
            case KeyEventArgs { Key: Key.Escape } when IsZoomed:
                if (VideoPlayer.Overlay is not null && IsMouseOverWindow(VideoPlayer.Overlay))
                {
                    ResetZoom();
                }

                break;

            // Button events must be checked before MouseEventArgs (inherits from it)
            case MouseButtonEventArgs { ChangedButton: MouseButton.Left } mouseButton:
                HandleZoomMouseButton(mouseButton);
                break;

            case MouseEventArgs mouseMove when isSelecting:
                HandleSelectionMouseMove(mouseMove);
                break;

            case MouseEventArgs mouseMove when isPanning:
                HandlePanMouseMove(mouseMove);
                break;
        }
    }

    private void HandleZoomMouseButton(MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed
            && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Verify mouse is over this tile's overlay
            if (VideoPlayer.Overlay is null || !IsMouseOverWindow(VideoPlayer.Overlay))
            {
                return;
            }

            if (IsZoomed)
            {
                // Ctrl+drag when zoomed = pan
                isPanning = true;
                panStartPoint = e.GetPosition(VideoPlayer.Overlay);
                panStartX = currentPanX;
                panStartY = currentPanY;
            }
            else
            {
                // Ctrl+drag when not zoomed = rectangle selection zoom
                isSelecting = true;
                selectionStartPoint = e.GetPosition(VideoPlayer.Overlay);
                ZoomSelectionCanvas.Visibility = Visibility.Visible;
                ZoomSelectionRect.Width = 0;
                ZoomSelectionRect.Height = 0;
                Canvas.SetLeft(ZoomSelectionRect, selectionStartPoint.X);
                Canvas.SetTop(ZoomSelectionRect, selectionStartPoint.Y);
            }
        }
        else if (e.ButtonState == MouseButtonState.Released)
        {
            if (isPanning)
            {
                isPanning = false;
            }

            if (isSelecting)
            {
                CompleteSelectionZoom(e);
            }
        }
    }

    private void HandleSelectionMouseMove(MouseEventArgs e)
    {
        if (VideoPlayer.Overlay is null)
        {
            return;
        }

        var current = e.GetPosition(VideoPlayer.Overlay);
        var x = Math.Min(selectionStartPoint.X, current.X);
        var y = Math.Min(selectionStartPoint.Y, current.Y);
        var w = Math.Abs(current.X - selectionStartPoint.X);
        var h = Math.Abs(current.Y - selectionStartPoint.Y);

        Canvas.SetLeft(ZoomSelectionRect, x);
        Canvas.SetTop(ZoomSelectionRect, y);
        ZoomSelectionRect.Width = w;
        ZoomSelectionRect.Height = h;
    }

    private void CompleteSelectionZoom(MouseButtonEventArgs e)
    {
        isSelecting = false;
        ZoomSelectionCanvas.Visibility = Visibility.Collapsed;

        if (VideoPlayer.Overlay is null)
        {
            return;
        }

        var endPoint = e.GetPosition(VideoPlayer.Overlay);
        var overlayW = VideoPlayer.Overlay.ActualWidth;
        var overlayH = VideoPlayer.Overlay.ActualHeight;

        if (overlayW <= 0 || overlayH <= 0)
        {
            return;
        }

        var selW = Math.Abs(endPoint.X - selectionStartPoint.X);
        var selH = Math.Abs(endPoint.Y - selectionStartPoint.Y);

        // Ignore tiny selections (accidental clicks)
        if (selW < 10 || selH < 10)
        {
            return;
        }

        // Calculate zoom level from selection size
        var zoomX = (float)(overlayW / selW);
        var zoomY = (float)(overlayH / selH);
        var newZoom = Math.Clamp(Math.Min(zoomX, zoomY), 1.0f, 10.0f);

        // Calculate pan to center the selection
        var centerX = (Math.Min(selectionStartPoint.X, endPoint.X) + (selW / 2)) / overlayW;
        var centerY = (Math.Min(selectionStartPoint.Y, endPoint.Y) + (selH / 2)) / overlayH;

        currentZoom = newZoom;
        currentPanX = Math.Clamp(((float)centerX - 0.5f) * 2f, -1f, 1f);
        currentPanY = Math.Clamp(((float)centerY - 0.5f) * 2f, -1f, 1f);

        VideoPlayer.SetZoom(currentZoom, currentPanX, currentPanY);
        UpdateZoomVisuals();
        ZoomStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandlePanMouseMove(MouseEventArgs e)
    {
        if (VideoPlayer.Overlay is null)
        {
            return;
        }

        var current = e.GetPosition(VideoPlayer.Overlay);
        var dx = (float)((panStartPoint.X - current.X) / VideoPlayer.Overlay.ActualWidth) * 2f;
        var dy = (float)((panStartPoint.Y - current.Y) / VideoPlayer.Overlay.ActualHeight) * 2f;

        currentPanX = Math.Clamp(panStartX + dx, -1f, 1f);
        currentPanY = Math.Clamp(panStartY + dy, -1f, 1f);

        VideoPlayer.SetZoom(currentZoom, currentPanX, currentPanY);
    }

    private static bool IsMouseOverWindow(Window window)
    {
        var pos = Mouse.GetPosition(window);
        return pos.X >= 0 && pos.Y >= 0
            && pos.X <= window.ActualWidth
            && pos.Y <= window.ActualHeight;
    }

    /// <summary>
    /// Gets a value indicating whether this tile is zoomed beyond the default fit view.
    /// </summary>
    public bool IsZoomed => currentZoom > 1.01f;

    [RelayCommand(CanExecute = nameof(CanResetZoom))]
    private void ZoomReset()
        => ResetZoom();

    private bool CanResetZoom()
        => IsZoomed;

    [RelayCommand]
    private void Rotate(CameraRotation rotation)
    {
        if (Camera is null || Camera.Display.Rotation == rotation)
        {
            return;
        }

        Camera.Display.Rotation = rotation;
        ApplyRotation();
        CameraConfigurationChanged?.Invoke(this, Camera);
    }

    /// <summary>
    /// Resets the zoom level back to fit-to-view.
    /// </summary>
    public void ResetZoom()
    {
        var wasZoomed = IsZoomed;
        currentZoom = 1.0f;
        currentPanX = 0f;
        currentPanY = 0f;
        VideoPlayer.ResetZoom();
        UpdateZoomVisuals();

        if (wasZoomed)
        {
            ZoomStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateZoomVisuals()
    {
        // Motion overlay: hide when zoomed
        var overlay = GetMotionBoundingBoxOverlay();
        if (overlay is not null)
        {
            overlay.Visibility = IsZoomed ? Visibility.Collapsed : Visibility.Visible;
        }

        // Zoom indicator text
        if (IsZoomed)
        {
            var percent = (int)(currentZoom * 100);
            ZoomIndicator.Text = $"Zoom: {percent.ToString(CultureInfo.InvariantCulture)}%";
            ZoomIndicatorBorder.Visibility = Visibility.Visible;
        }
        else
        {
            ZoomIndicatorBorder.Visibility = Visibility.Collapsed;
        }

        // Zoom-reset quick-action button mirrors IsZoomed (gated by
        // ShowOverlayQuickActions inside the recompute).
        UpdateQuickActionVisibility();
    }
}