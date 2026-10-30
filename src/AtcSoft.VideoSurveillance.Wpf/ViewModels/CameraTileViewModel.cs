using ConnectionState = Atc.Network.ConnectionState;
using CoreCameraConnectionType = AtcSoft.VideoSurveillance.Enums.CameraConnectionType;

namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// Per-camera view model for a single tile in the live view grid.
/// Watches its camera's HLS stream on a lease it renews, and shows the
/// server's camera events.
/// </summary>
public sealed partial class CameraTileViewModel : ViewModelBase, IDisposable
{
    private const int MaxStreamRetries = 5;
    private static readonly TimeSpan StreamRetryDelay = TimeSpan.FromSeconds(5);

    private readonly IStreamLeaseGateway streams;
    private readonly SurveillanceEventService eventService;
    private readonly Lock leaseLock = new();
    private readonly string apiBaseAddress;
    private Guid? leaseId;
    private TimeSpan renewInterval = TimeSpan.FromSeconds(30);
    private bool disposed;

    [ObservableProperty]
    private Guid cameraId;

    [ObservableProperty]
    private string displayName = string.Empty;

    // False while the live view's filter or page leaves this camera out.
    // The tile is then not shown; its stream keeps running.
    [ObservableProperty]
    private bool isShownInGrid = true;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private ConnectionState connectionState = ConnectionState.Disconnected;

    /// <summary>
    /// Symbolic-link / device-id of the underlying USB device when this
    /// tile is bound to a USB camera. Empty for network cameras. Used
    /// by <see cref="LiveViewViewModel"/> to match
    /// <see cref="AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher"/>
    /// events back to the right tile.
    /// </summary>
    [ObservableProperty(DependentPropertyNames = [nameof(IsUsbSource)])]
    private string usbDeviceId = string.Empty;

    /// <summary>True when this tile represents a USB camera source.</summary>
    public bool IsUsbSource => !string.IsNullOrEmpty(UsbDeviceId);

    /// <summary>
    /// Effective connection type reported by the server (USB cameras are always USB).
    /// </summary>
    [ObservableProperty(DependentPropertyNames = [nameof(DisplayedConnectionType)])]
    private CoreCameraConnectionType connectionType;

    /// <summary>
    /// Whether the connection-type icon is enabled in the server settings.
    /// </summary>
    [ObservableProperty(DependentPropertyNames = [nameof(DisplayedConnectionType)])]
    private bool showConnectionTypeIcon;

    /// <summary>
    /// The clockwise rotation of the camera. The server streams the camera's
    /// own pixels, so the picture is turned here, in the client's player.
    /// </summary>
    [ObservableProperty(AfterChangedCallback = nameof(ApplyRotation))]
    private AtcSoft.VideoSurveillance.Enums.CameraRotation rotation;

    /// <summary>
    /// Gets the connection type shown by the badge icon, or
    /// <see cref="CoreCameraConnectionType.None"/> while the icon is disabled.
    /// </summary>
    public CoreCameraConnectionType DisplayedConnectionType
        => ShowConnectionTypeIcon ? ConnectionType : CoreCameraConnectionType.None;

    /// <summary>
    /// True when the underlying USB device is currently unplugged.
    /// Orthogonal to <see cref="ConnectionState"/>: a USB camera can be
    /// <c>ConnectionFailed</c> *and* unplugged simultaneously, and the
    /// overlay surfaces both signals. Cleared automatically when the
    /// watcher reports the device returned.
    /// </summary>
    [ObservableProperty(DependentPropertyNames = [nameof(PlaceholderText)])]
    private bool isDeviceUnplugged;

    /// <summary>
    /// Gets what the tile says while it has no picture. A USB camera that
    /// was pulled out says so: "no stream" would send the user looking at
    /// the server, and the cable is where to look.
    /// </summary>
    public string PlaceholderText
        => IsDeviceUnplugged
            ? Translations.UsbDeviceUnplugged
            : Translations.NoStream;

    private bool isRecording;
    private DispatcherTimer? recordingTimer;
    private DateTime recordingStartUtc;

    [ObservableProperty]
    private string recordingDurationText = "00:00:00";

    [ObservableProperty]
    private bool isMotionDetected;

    [ObservableProperty]
    private IReadOnlyList<AtcSoft.VideoSurveillance.Models.BoundingBox> currentBoundingBoxes = [];

    /// <summary>
    /// The displayed picture's width (turned with the rotation), so the boxes stay off the black bars.
    /// </summary>
    [ObservableProperty]
    private int motionVideoWidth;

    [ObservableProperty]
    private int motionVideoHeight;

    [ObservableProperty]
    private int analysisWidth = 320;

    [ObservableProperty]
    private int analysisHeight = 240;

    [ObservableProperty]
    private IVideoPlayer? player;

    private bool isStreaming;
    private DispatcherTimer? heartbeatTimer;

    // Auto-recover: when the HLS player drops (e.g. server reaped a stale
    // session, transient network blip, FFmpeg restart), retry StartStream
    // up to MaxStreamRetries times instead of leaving the tile stuck on
    // Disconnected. userStopInProgress suppresses the retry loop when the
    // stop was initiated by us (StopStreamAsync / Dispose).
    private bool userStopInProgress;
    private DispatcherTimer? retryTimer;
    private int retryAttempt;

    private void ApplyRotation()
        => Player?.SetRotation((VideoRotation)(int)Rotation);

    public CameraTileViewModel(
        IVideoPlayerFactory videoPlayerFactory,
        IStreamLeaseGateway streams,
        SurveillanceEventService eventService,
        string apiBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(videoPlayerFactory);
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(eventService);

        this.streams = streams;
        this.eventService = eventService;
        this.apiBaseAddress = apiBaseAddress;

        Player = videoPlayerFactory.Create();
        Player.StateChanged += OnPlayerStateChanged;
        ApplyRotation();

        eventService.OnConnectionStateChanged += OnHubConnectionStateChanged;
        eventService.OnRecordingStateChanged += OnHubRecordingStateChanged;
        eventService.OnMotionDetected += OnHubMotionDetected;
    }

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

    public bool IsStreaming
    {
        get => isStreaming;
        set
        {
            if (isStreaming == value)
            {
                return;
            }

            isStreaming = value;
            RaisePropertyChanged(nameof(IsStreaming));
            UpdateHeartbeatTimer(value);
        }
    }

    /// <summary>
    /// Takes a lease on the camera's stream and plays it.
    /// </summary>
    /// <exception cref="HttpRequestException">The server refused, with its reason, or could not be reached.</exception>
    public async Task StartStreamAsync()
    {
        var lease = await streams
            .StartStreamAsync(CameraId)
            .ConfigureAwait(false);

        Guid? previous;
        lock (leaseLock)
        {
            previous = leaseId;
            leaseId = lease.LeaseId;
            if (lease.RenewSeconds > 0)
            {
                renewInterval = TimeSpan.FromSeconds(lease.RenewSeconds);
            }
        }

        // Started twice: one lease is enough.
        if (previous is { } old && old != lease.LeaseId)
        {
            await streams.StopStreamAsync(CameraId, old).ConfigureAwait(false);
        }

        Play(lease.PlaylistUrl);
    }

    public Task StopStreamAsync()
    {
        userStopInProgress = true;
        CancelStreamRetry();
        Player?.Close();

        _ = Application.Current?.Dispatcher.InvokeAsync(() => IsStreaming = false);

        return TakeLease() is { } lease
            ? streams.StopStreamAsync(CameraId, lease)
            : Task.CompletedTask;
    }

    /// <summary>
    /// Stops every tile's stream and gives the leases back, all at once (when
    /// the app closes and has little time).
    /// </summary>
    public static Task StopAllAsync(IEnumerable<CameraTileViewModel> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        return Task.WhenAll(tiles.Select(tile => tile.StopStreamAsync()));
    }

    /// <summary>
    /// Renews the stream lease while the tile watches; one the server no
    /// longer has (it restarted, or the lease ran out) starts the stream again.
    /// </summary>
    internal async Task RenewLeaseAsync()
    {
        Guid? current;
        lock (leaseLock)
        {
            current = leaseId;
        }

        if (current is not { } lease || disposed)
        {
            return;
        }

        var renewal = await streams
            .RenewStreamLeaseAsync(CameraId, lease)
            .ConfigureAwait(false);

        if (renewal != StreamLeaseRenewal.Gone || !TakeLease(lease))
        {
            return;
        }

        try
        {
            await StartStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(ScheduleStreamRetry);
        }
    }

    private Guid? TakeLease(Guid? only = null)
    {
        lock (leaseLock)
        {
            var taken = leaseId;
            if (taken is null || (only is { } expected && taken != expected))
            {
                return null;
            }

            leaseId = null;
            return taken;
        }
    }

    private bool TakeLease(Guid lease)
        => TakeLease(only: lease) is not null;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        eventService.OnConnectionStateChanged -= OnHubConnectionStateChanged;
        eventService.OnRecordingStateChanged -= OnHubRecordingStateChanged;
        eventService.OnMotionDetected -= OnHubMotionDetected;

        // A tile that goes gives its lease back; the server stops a stream
        // nobody else watches.
        if (TakeLease() is { } lease)
        {
            _ = streams.StopStreamAsync(CameraId, lease);
        }

        if (recordingTimer is not null)
        {
            recordingTimer.Stop();
            recordingTimer.Tick -= OnRecordingTimerTick;
            recordingTimer = null;
        }

        if (heartbeatTimer is not null)
        {
            heartbeatTimer.Stop();
            heartbeatTimer.Tick -= OnHeartbeatTimerTick;
            heartbeatTimer = null;
        }

        if (retryTimer is not null)
        {
            retryTimer.Stop();
            retryTimer.Tick -= OnRetryTimerTick;
            retryTimer = null;
        }

        if (Player is not null)
        {
            Player.StateChanged -= OnPlayerStateChanged;
            Player.Dispose();
            Player = null;
        }
    }

    private void Play(string playlistUrl)
    {
        var fullUrl = $"{apiBaseAddress.TrimEnd('/')}{playlistUrl}";
        var uri = new Uri(fullUrl);
        var options = new StreamOptions
        {
            UseLowLatencyMode = true,
            HardwareAcceleration = true,
        };

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

        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            retryAttempt = 0;
            IsStreaming = true;
        });
    }

    private void OnPlayerStateChanged(
        object? sender,
        PlayerStateChangedEventArgs e)
    {
        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ConnectionState = e.NewState switch
            {
                PlayerState.Playing => ConnectionState.Connected,
                PlayerState.Opening => ConnectionState.Connecting,
                PlayerState.Stopped => ConnectionState.Disconnected,
                PlayerState.Error => ConnectionState.ConnectionFailed,
                _ => ConnectionState.Disconnected,
            };

            if (e.NewState is PlayerState.Stopped or PlayerState.Error)
            {
                var wasStreaming = IsStreaming;
                IsStreaming = false;

                if (wasStreaming && !userStopInProgress && !disposed)
                {
                    ScheduleStreamRetry();
                }

                userStopInProgress = false;
            }
        });
    }

    private void OnHubConnectionStateChanged(ConnectionStateChangedEvent e)
    {
        if (e.CameraId != CameraId)
        {
            return;
        }

        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ConnectionState = HubStates.ToConnectionState(e.NewState);
        });
    }

    private void OnHubRecordingStateChanged(RecordingStateChangedEvent e)
    {
        if (e.CameraId != CameraId)
        {
            return;
        }

        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsRecording = HubStates.IsRecording(e.NewState);
        });
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

    // The server ends a stream lease that is not renewed for two renewal
    // intervals. The HLS playlist and segment requests do not renew it, so
    // without this timer the stream is stopped under us and the tile flips
    // to Disconnected. One missed renewal (a Wi-Fi blip, a GC pause) is safe.
    private void UpdateHeartbeatTimer(bool nowStreaming)
    {
        if (nowStreaming)
        {
            if (heartbeatTimer is null)
            {
                heartbeatTimer = new DispatcherTimer
                {
                    Interval = renewInterval,
                };
                heartbeatTimer.Tick += OnHeartbeatTimerTick;
            }

            heartbeatTimer.Start();
        }
        else
        {
            heartbeatTimer?.Stop();
        }
    }

    private void OnHeartbeatTimerTick(
        object? sender,
        EventArgs e)
        => _ = RenewLeaseAsync();

    private void ScheduleStreamRetry()
    {
        if (retryAttempt >= MaxStreamRetries)
        {
            return;
        }

        retryAttempt++;

        if (retryTimer is null)
        {
            retryTimer = new DispatcherTimer
            {
                Interval = StreamRetryDelay,
            };
            retryTimer.Tick += OnRetryTimerTick;
        }

        retryTimer.Stop();
        retryTimer.Start();
    }

    private void CancelStreamRetry()
    {
        retryTimer?.Stop();
        retryAttempt = 0;
    }

    private void OnRetryTimerTick(
        object? sender,
        EventArgs e)
    {
        retryTimer?.Stop();
        _ = TryStartStreamForRetryAsync();
    }

    private async Task TryStartStreamForRetryAsync()
    {
        if (disposed || userStopInProgress)
        {
            return;
        }

        try
        {
            await StartStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            // The server refused or is down: schedule another attempt directly,
            // since the player won't transition state without our doing.
            _ = Application.Current?.Dispatcher.InvokeAsync(ScheduleStreamRetry);
        }
    }

    private void OnHubMotionDetected(MotionDetectedEvent e)
    {
        if (e.CameraId != CameraId)
        {
            return;
        }

        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsMotionDetected = e.IsMotionActive;
            AnalysisWidth = e.AnalysisWidth;
            AnalysisHeight = e.AnalysisHeight;

            if (player?.StreamInfo is { Width: > 0, Height: > 0 } stream)
            {
                (MotionVideoWidth, MotionVideoHeight) = AtcSoft.VideoSurveillance.Helpers.MotionBoxMapper.GetDisplaySize(stream.Width, stream.Height, Rotation);
            }

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
}