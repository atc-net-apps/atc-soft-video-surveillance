// ReSharper disable GCSuppressFinalizeForTypeWithoutDestructor
namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// ViewModel for the fullscreen camera window.
/// </summary>
public sealed partial class FullScreenCameraWindowViewModel : ViewModelDialogBase, IDisposable
{
    private readonly CameraConfiguration camera;
    private readonly IMotionDetectionService? motionDetectionService;
    private readonly IVideoPlayerFactory? videoPlayerFactory;
    private readonly bool ownsPlayer;
    private DispatcherTimer? overlayHideTimer;
    private bool disposed;

    [ObservableProperty]
    private IVideoPlayer? player;

    [ObservableProperty]
    private string cameraName = string.Empty;

    [ObservableProperty]
    private string cameraDescription = string.Empty;

    [ObservableProperty]
    private ConnectionState connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    private bool isOverlayVisible = true;

    [ObservableProperty]
    private bool showOverlayTitle = true;

    [ObservableProperty]
    private bool showOverlayDescription = true;

    [ObservableProperty]
    private bool showOverlayTime;

    [ObservableProperty]
    private bool showOverlayConnectionStatus = true;

    [ObservableProperty]
    private double overlayOpacity = 0.7;

    [ObservableProperty]
    private OverlayPosition overlayPosition = OverlayPosition.TopLeft;

    [ObservableProperty(DependentPropertyNames = [nameof(DisplayedConnectionType)])]
    private bool showConnectionTypeIcon;

    [ObservableProperty]
    private ConnectionTypeIconPlacement connectionTypeIconPlacement = ConnectionTypeIconPlacement.SameRow;

    /// <summary>
    /// Gets the connection type shown by the icon: the camera's effective
    /// type while the icon is enabled, otherwise <see cref="CameraConnectionType.None"/>.
    /// </summary>
    public CameraConnectionType DisplayedConnectionType
        => ShowConnectionTypeIcon
            ? camera.EffectiveConnectionType
            : CameraConnectionType.None;

    // Bounding box settings
    [ObservableProperty]
    private bool showBoundingBoxInFullScreen;

    [ObservableProperty]
    private string boundingBoxColor = "Red";

    [ObservableProperty]
    private int boundingBoxThickness = 2;

    [ObservableProperty]
    private double boundingBoxSmoothing = 0.3;

    [ObservableProperty]
    private IReadOnlyList<BoundingBox> currentBoundingBoxes = [];

    [ObservableProperty]
    private int analysisWidth = 320;

    [ObservableProperty]
    private int analysisHeight = 240;

    public FullScreenCameraWindowViewModel(
        CameraConfiguration camera,
        bool showOverlayTitle,
        bool showOverlayDescription,
        bool showOverlayTime,
        bool showOverlayConnectionStatus,
        double overlayOpacity,
        OverlayPosition overlayPosition,
        bool showBoundingBoxInFullScreen = false,
        string boundingBoxColor = "Red",
        int boundingBoxThickness = 2,
        double boundingBoxSmoothing = 0.3,
        IMotionDetectionService? motionDetectionService = null,
        IVideoPlayer? existingPlayer = null,
        IVideoPlayerFactory? videoPlayerFactory = null,
        bool showConnectionTypeIcon = false,
        ConnectionTypeIconPlacement connectionTypeIconPlacement = ConnectionTypeIconPlacement.SameRow)
    {
        ArgumentNullException.ThrowIfNull(camera);

        this.camera = camera;
        this.motionDetectionService = motionDetectionService;
        this.videoPlayerFactory = videoPlayerFactory;
        CameraName = camera.Display.DisplayName;
        CameraDescription = camera.Display.Description ?? string.Empty;

        // Apply overlay settings (per-camera overrides merged with app defaults)
        ShowOverlayTitle = showOverlayTitle;
        ShowOverlayDescription = showOverlayDescription;
        ShowOverlayTime = showOverlayTime;
        ShowOverlayConnectionStatus = showOverlayConnectionStatus;
        OverlayOpacity = overlayOpacity;
        OverlayPosition = overlayPosition;
        ShowConnectionTypeIcon = showConnectionTypeIcon;
        ConnectionTypeIconPlacement = connectionTypeIconPlacement;

        // Apply bounding box settings
        ShowBoundingBoxInFullScreen = showBoundingBoxInFullScreen;
        BoundingBoxColor = boundingBoxColor;
        BoundingBoxThickness = boundingBoxThickness;
        BoundingBoxSmoothing = boundingBoxSmoothing;

        // Get analysis resolution from motion detection service
        if (motionDetectionService is not null)
        {
            var (width, height) = motionDetectionService.GetAnalysisResolution(camera.Id);
            AnalysisWidth = width;
            AnalysisHeight = height;
            motionDetectionService.MotionDetected += OnMotionDetected;
        }

        // Use existing player (borrowed from CameraTile) or create a new one
        if (existingPlayer is not null)
        {
            Player = existingPlayer;
            Player.StateChanged += OnPlayerStateChanged;

            // Update connection state based on current player state
            ConnectionState = existingPlayer.State switch
            {
                PlayerState.Playing => ConnectionState.Connected,
                PlayerState.Opening => ConnectionState.Connecting,
                PlayerState.Stopped => ConnectionState.Disconnected,
                PlayerState.Error => ConnectionState.ConnectionFailed,
                _ => ConnectionState.Disconnected,
            };

            ownsPlayer = false;
        }
        else
        {
            InitializePlayer();
            ownsPlayer = true;
        }

        StartOverlayHideTimer();
    }

    /// <summary>
    /// Occurs when the window requests to be closed.
    /// </summary>
    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    /// <summary>
    /// Called when the mouse moves in the window.
    /// </summary>
    public void OnMouseMoved()
    {
        IsOverlayVisible = true;
        overlayHideTimer?.Stop();
        overlayHideTimer?.Start();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
    }

    /// <summary>
    /// Gets the clockwise rotation the camera is displayed with.
    /// </summary>
    public CameraRotation Rotation => camera.Display.Rotation;

    private void InitializePlayer()
    {
        if (videoPlayerFactory is null)
        {
            return;
        }

        Player = videoPlayerFactory.Create();
        Player.StateChanged += OnPlayerStateChanged;

        // A player created here starts unrotated; a borrowed one already
        // carries the tile's rotation.
        Player.SetRotation((VideoRotation)(int)camera.Display.Rotation);

        // Defer stream opening to avoid blocking UI during window creation
        var locator = camera.BuildSourceLocator();
        var options = new StreamOptions
        {
            Source = camera.Display.DisplayName,
            UseLowLatencyMode = camera.Stream.UseLowLatencyMode,
            MaxLatencyMs = camera.Stream.MaxLatencyMs,
            RtspTransport = camera.Stream.RtspTransport,
            BufferDurationMs = camera.Stream.BufferDurationMs,
            InputFormat = locator.InputFormat switch
            {
                "dshow" => InputFormatKind.Dshow,
                "v4l2" => InputFormatKind.V4l2,
                "avfoundation" => InputFormatKind.AVFoundation,
                _ => InputFormatKind.Auto,
            },
            RawDeviceSpec = locator.RawDeviceSpec,
            RecordAudio = locator.IncludesAudio,
            VideoSize = locator.VideoSize,
            FrameRate = locator.FrameRate,
            PixelFormat = locator.PixelFormat,
        };
        var uri = locator.Uri;

        _ = Task.Run(() =>
        {
            try
            {
                Player?.Open(uri, options);
            }
            catch
            {
                // State will be updated via StateChanged
            }
        });
    }

    private void OnPlayerStateChanged(
        object? sender,
        PlayerStateChangedEventArgs e)
    {
        ConnectionState = e.NewState switch
        {
            PlayerState.Playing => ConnectionState.Connected,
            PlayerState.Opening => ConnectionState.Connecting,
            PlayerState.Stopped => ConnectionState.Disconnected,
            PlayerState.Error => ConnectionState.ConnectionFailed,
            _ => ConnectionState.Disconnected,
        };
    }

    private void OnMotionDetected(
        object? sender,
        MotionDetectedEventArgs e)
    {
        // Only handle events for this camera
        if (e.CameraId != camera.Id)
        {
            return;
        }

        // Update bounding boxes on UI thread
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (e.IsMotionActive && e.HasBoundingBoxes)
            {
                CurrentBoundingBoxes = e.BoundingBoxes;
            }
            else
            {
                CurrentBoundingBoxes = [];
            }
        });
    }

    private void StartOverlayHideTimer()
    {
        overlayHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3),
        };

        overlayHideTimer.Tick += (_, _) =>
        {
            IsOverlayVisible = false;
            overlayHideTimer.Stop();
        };

        overlayHideTimer.Start();
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            overlayHideTimer?.Stop();
            overlayHideTimer = null;

            if (motionDetectionService is not null)
            {
                motionDetectionService.MotionDetected -= OnMotionDetected;
            }

            if (Player is not null)
            {
                Player.StateChanged -= OnPlayerStateChanged;

                // Only dispose the player if we own it (created it ourselves)
                // If it was borrowed from CameraTile, it will be returned and reused
                if (ownsPlayer)
                {
                    Player.Dispose();
                }

                Player = null;
            }
        }

        disposed = true;
    }
}