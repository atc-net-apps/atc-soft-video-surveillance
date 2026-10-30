// ReSharper disable GCSuppressFinalizeForTypeWithoutDestructor
using ConnectionState = Atc.Network.ConnectionState;

namespace AtcSoft.VideoSurveillance.Wpf.Windows;

/// <summary>
/// ViewModel for the fullscreen camera window.
/// Simplified version — no direct RTSP connection or local motion detection.
/// The player is borrowed from the CameraTile (not owned by this VM).
/// </summary>
public sealed partial class FullScreenCameraWindowViewModel : ViewModelBase, IDisposable
{
    private readonly SurveillanceEventService eventService;
    private readonly Guid cameraId;
    private readonly AtcSoft.VideoSurveillance.Enums.CameraRotation rotation;
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

    private bool isRecording;
    private DispatcherTimer? recordingTimer;
    private DateTime recordingStartUtc;

    [ObservableProperty]
    private string recordingDurationText = "00:00:00";

    [ObservableProperty]
    private bool isMotionDetected;

    [ObservableProperty]
    private IReadOnlyList<AtcSoft.VideoSurveillance.Models.BoundingBox> currentBoundingBoxes = [];

    [ObservableProperty]
    private int analysisWidth = 320;

    [ObservableProperty]
    private int analysisHeight = 240;

    // The picture's size as shown, so the boxes land on a rotated picture too.
    [ObservableProperty]
    private int motionVideoWidth;

    [ObservableProperty]
    private int motionVideoHeight;

    public FullScreenCameraWindowViewModel(
        SurveillanceEventService eventService,
        Guid cameraId,
        IVideoPlayer? existingPlayer,
        string cameraName,
        string cameraDescription,
        AtcSoft.VideoSurveillance.Enums.CameraRotation rotation)
    {
        ArgumentNullException.ThrowIfNull(eventService);

        this.eventService = eventService;
        this.cameraId = cameraId;
        this.rotation = rotation;
        CameraName = cameraName;
        CameraDescription = cameraDescription;

        // Borrow existing player from tile (we do NOT own it)
        if (existingPlayer is not null)
        {
            Player = existingPlayer;
            Player.StateChanged += OnPlayerStateChanged;

            ConnectionState = existingPlayer.State switch
            {
                PlayerState.Playing => ConnectionState.Connected,
                PlayerState.Opening => ConnectionState.Connecting,
                PlayerState.Stopped => ConnectionState.Disconnected,
                PlayerState.Error => ConnectionState.ConnectionFailed,
                _ => ConnectionState.Disconnected,
            };
        }

        UpdateMotionVideoSize();

        eventService.OnConnectionStateChanged += OnHubConnectionStateChanged;
        eventService.OnRecordingStateChanged += OnHubRecordingStateChanged;
        eventService.OnMotionDetected += OnHubMotionDetected;

        StartOverlayHideTimer();
    }

    /// <summary>
    /// Occurs when the window requests to be closed.
    /// </summary>
    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    public bool IsRecording
    {
        get => isRecording;
        set
        {
            if (isRecording == value)
            {
                return;
            }

            isRecording = value;
            RaisePropertyChanged(nameof(IsRecording));
            UpdateRecordingDurationTimer(value);
        }
    }

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

    private void OnPlayerStateChanged(
        object? sender,
        PlayerStateChangedEventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ConnectionState = e.NewState switch
            {
                PlayerState.Playing => ConnectionState.Connected,
                PlayerState.Opening => ConnectionState.Connecting,
                PlayerState.Stopped => ConnectionState.Disconnected,
                PlayerState.Error => ConnectionState.ConnectionFailed,
                _ => ConnectionState.Disconnected,
            };
        });
    }

    private void OnHubConnectionStateChanged(ConnectionStateChangedEvent e)
    {
        if (e.CameraId != cameraId)
        {
            return;
        }

        Application.Current?.Dispatcher.Invoke(() =>
        {
            ConnectionState = HubStates.ToConnectionState(e.NewState);
        });
    }

    private void OnHubRecordingStateChanged(RecordingStateChangedEvent e)
    {
        if (e.CameraId != cameraId)
        {
            return;
        }

        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsRecording = HubStates.IsRecording(e.NewState);
        });
    }

    private void OnHubMotionDetected(MotionDetectedEvent e)
    {
        if (e.CameraId != cameraId)
        {
            return;
        }

        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsMotionDetected = e.IsMotionActive;
            AnalysisWidth = e.AnalysisWidth;
            AnalysisHeight = e.AnalysisHeight;
            UpdateMotionVideoSize();

            if (e.IsMotionActive && e.BoundingBoxes.Count > 0)
            {
                // With its track id, so the overlay smooths each box against its own track.
                CurrentBoundingBoxes = e.BoundingBoxes
                    .Select(b => new AtcSoft.VideoSurveillance.Models.BoundingBox { X = b.X, Y = b.Y, Width = b.Width, Height = b.Height, TrackId = b.TrackId })
                    .ToList();
            }
            else
            {
                CurrentBoundingBoxes = [];
            }
        });
    }

    private void UpdateMotionVideoSize()
    {
        if (Player?.StreamInfo is { Width: > 0, Height: > 0 } stream)
        {
            (MotionVideoWidth, MotionVideoHeight) = AtcSoft.VideoSurveillance.Helpers.MotionBoxMapper.GetDisplaySize(stream.Width, stream.Height, rotation);
        }
    }

    private void UpdateRecordingDurationTimer(bool nowRecording)
    {
        if (nowRecording)
        {
            recordingStartUtc = DateTime.UtcNow;
            RecordingDurationText = TimeSpan.Zero.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

            if (recordingTimer is null)
            {
                recordingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1),
                };
                recordingTimer.Tick += OnRecordingTimerTick;
            }

            recordingTimer.Start();
        }
        else
        {
            recordingTimer?.Stop();
            RecordingDurationText = TimeSpan.Zero.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }
    }

    private void OnRecordingTimerTick(
        object? sender,
        EventArgs e)
    {
        var elapsed = DateTime.UtcNow - recordingStartUtc;
        RecordingDurationText = elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
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

            if (recordingTimer is not null)
            {
                recordingTimer.Stop();
                recordingTimer.Tick -= OnRecordingTimerTick;
                recordingTimer = null;
            }

            eventService.OnConnectionStateChanged -= OnHubConnectionStateChanged;
            eventService.OnRecordingStateChanged -= OnHubRecordingStateChanged;
            eventService.OnMotionDetected -= OnHubMotionDetected;

            if (Player is not null)
            {
                Player.StateChanged -= OnPlayerStateChanged;

                // Do NOT dispose the player — it is borrowed from the tile
                Player = null;
            }
        }

        disposed = true;
    }
}