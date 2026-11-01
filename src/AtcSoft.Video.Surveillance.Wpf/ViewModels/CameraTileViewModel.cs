using ConnectionState = Atc.Network.ConnectionState;
using CoreCameraConnectionType = AtcSoft.Video.Surveillance.Enums.CameraConnectionType;

namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

/// <summary>
/// Per-camera view model for a single tile in the live view grid.
/// Watches its camera's HLS stream on a lease it renews, and shows the
/// server's camera events.
/// </summary>
public sealed partial class CameraTileViewModel : ViewModelBase, IDisposable
{
    // The waits before each new start of a stream that failed, the last one
    // repeated for as long as it fails: a wall left alone for weeks recovers
    // from an outage of any length.
    private static readonly TimeSpan[] StreamRetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(40),
        TimeSpan.FromSeconds(60),
    ];

    private readonly IStreamLeaseGateway streams;
    private readonly SurveillanceEventService eventService;
    private readonly TimeProvider time;
    private readonly ILogger logger;
    private readonly Lock leaseLock = new();
    private readonly Lock playerLock = new();
    private readonly Lock retryLock = new();
    private readonly string apiBaseAddress;
    private Guid? leaseId;
    private TimeSpan renewInterval = TimeSpan.FromSeconds(30);
    private bool disposed;

    [ObservableProperty]
    private Guid cameraId;

    [ObservableProperty]
    private string displayName = string.Empty;

    // False while the live view's filter or page leaves this camera out. The
    // tile is then not shown, and its stream rests (no decoder, no lease)
    // unless a full screen window shows it; it starts again when shown.
    [ObservableProperty(AfterChangedCallback = nameof(OnVisibilityChanged))]
    private bool isShownInGrid = true;

    private bool isShownFullScreen;
    private DateTimeOffset lastMotionShown = DateTimeOffset.MinValue;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private ConnectionState connectionState = ConnectionState.Disconnected;

    /// <summary>
    /// Symbolic-link / device-id of the underlying USB device when this
    /// tile is bound to a USB camera. Empty for network cameras. Used
    /// by <see cref="LiveViewViewModel"/> to match
    /// <see cref="AtcSoft.Video.Surveillance.Services.IUsbCameraWatcher"/>
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
    private AtcSoft.Video.Surveillance.Enums.CameraRotation rotation;

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
    private IReadOnlyList<AtcSoft.Video.Surveillance.Models.BoundingBox> currentBoundingBoxes = [];

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
    private ITimer? heartbeatTimer;

    // Auto-recover: when the HLS player fails (the server ended the stream,
    // a read timed out, the lease ran out), the stream is started again on a
    // new lease instead of leaving the tile on "Connection failed".
    // stoppedByUser suppresses it after StopStreamAsync.
    private bool stoppedByUser;
    private ITimer? retryTimer;
    private int retryGeneration;
    private int retryAttempt;

    private void ApplyRotation()
        => Player?.SetRotation((VideoRotation)(int)Rotation);

    public CameraTileViewModel(
        IVideoPlayerFactory videoPlayerFactory,
        IStreamLeaseGateway streams,
        SurveillanceEventService eventService,
        string apiBaseAddress,
        TimeProvider? time = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(videoPlayerFactory);
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(eventService);

        this.streams = streams;
        this.eventService = eventService;
        this.apiBaseAddress = apiBaseAddress;
        this.time = time ?? TimeProvider.System;
        this.logger = logger ?? NullLogger.Instance;

        Player = videoPlayerFactory.Create();
        Player.StateChanged += OnPlayerStateChanged;
        ApplyRotation();

        eventService.OnConnectionStateChanged += OnHubConnectionStateChanged;
        eventService.OnRecordingStateChanged += OnHubRecordingStateChanged;
        eventService.OnMotionDetected += OnHubMotionDetected;
        eventService.ConnectedChanged += OnEventServiceConnectedChanged;
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
    /// Gets a value indicating whether a new start of the stream is waiting
    /// (after the player failed, or the server could not start it).
    /// </summary>
    internal bool IsRetryScheduled
    {
        get
        {
            lock (retryLock)
            {
                return retryTimer is not null;
            }
        }
    }

    /// <summary>
    /// Takes a lease on the camera's stream and plays it.
    /// </summary>
    /// <exception cref="HttpRequestException">The server refused, with its reason, or could not be reached.</exception>
    public async Task StartStreamAsync()
    {
        stoppedByUser = false;

        var lease = await streams
            .StartStreamAsync(CameraId)
            .ConfigureAwait(false);

        // The tile went (or was left out of the grid) while the server started
        // the stream: the lease goes back.
        if (disposed || IsResting)
        {
            await streams.StopStreamAsync(CameraId, lease.LeaseId).ConfigureAwait(false);
            return;
        }

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
        stoppedByUser = true;
        CancelStreamRetry();
        lock (playerLock)
        {
            Player?.Close();
        }

        OnUi(() => IsStreaming = false);

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
    /// Makes sure the stream runs, after the live view was loaded again: a
    /// lease the server no longer has (it restarted) starts the stream again,
    /// and a tile waiting to start again does so now. Never throws.
    /// </summary>
    public async Task EnsureStreamAsync()
    {
        if (disposed || stoppedByUser || IsResting)
        {
            return;
        }

        bool hasLease;
        lock (leaseLock)
        {
            hasLease = leaseId is not null;
        }

        if (hasLease && !IsRetryScheduled)
        {
            await RenewLeaseAsync().ConfigureAwait(false);
            return;
        }

        CancelStreamRetry();
        await StartStreamOrRetryLaterAsync().ConfigureAwait(false);
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
        catch (Exception ex) when (IsStartFailure(ex))
        {
            LogStreamStartFailed(ex, CameraId);
            ScheduleStreamRetry();
        }
    }

    // What a start of the stream fails with: the server refused or could not be
    // reached, the call timed out, or the resilience pipeline turned it away.
    private static bool IsStartFailure(Exception ex)
        => ex is HttpRequestException or InvalidOperationException or OperationCanceledException or TimeoutException
            or Polly.ExecutionRejectedException;

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
        CancelStreamRetry();

        eventService.OnConnectionStateChanged -= OnHubConnectionStateChanged;
        eventService.OnRecordingStateChanged -= OnHubRecordingStateChanged;
        eventService.OnMotionDetected -= OnHubMotionDetected;
        eventService.ConnectedChanged -= OnEventServiceConnectedChanged;

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

        heartbeatTimer?.Dispose();
        heartbeatTimer = null;

        lock (playerLock)
        {
            if (Player is not null)
            {
                Player.StateChanged -= OnPlayerStateChanged;
                Player.Dispose();
                Player = null;
            }
        }
    }

    // Runs on the UI thread, or at once where there is none (tests).
    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            action();
            return;
        }

        _ = dispatcher.InvokeAsync(action);
    }

    private void Play(string playlistUrl)
    {
        if (disposed)
        {
            return;
        }

        var fullUrl = $"{apiBaseAddress.TrimEnd('/')}{playlistUrl}";
        var uri = new Uri(fullUrl);
        var options = new StreamOptions
        {
            UseLowLatencyMode = true,
            HardwareAcceleration = true,
        };

        // Off the UI thread: opening waits for the last stream's reader to end.
        _ = Task.Run(() => Open(uri, options));
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Whatever the player throws, the tile tries again later; it must not end the app.")]
    private void Open(
        Uri uri,
        StreamOptions options)
    {
        lock (playerLock)
        {
            var current = Player;
            if (disposed || stoppedByUser || IsResting || current is null)
            {
                return;
            }

            try
            {
                // The player opens only from Stopped. One that failed (the stream
                // ended, a read timed out, the lease ran out) or still plays an
                // older lease is closed first; its Stopped starts nothing.
                if (current.State != PlayerState.Stopped)
                {
                    current.Close();
                }

                current.Open(uri, options);
            }
            catch (Exception ex)
            {
                LogPlayerOpenFailed(ex, CameraId);
                ScheduleStreamRetry();
                return;
            }
        }

        OnUi(() =>
        {
            if (!disposed && !stoppedByUser)
            {
                IsStreaming = true;
            }
        });
    }

    private void OnPlayerStateChanged(
        object? sender,
        PlayerStateChangedEventArgs e)
    {
        var newState = e.NewState;

        // Only the player's own failure starts the stream again. Stopped comes
        // from Close alone: the user stopped, or the tile is opening again.
        if (newState == PlayerState.Error)
        {
            LogPlayerFailed(CameraId, e.ErrorMessage);
            OnUi(() => IsStreaming = false);
            ScheduleStreamRetry();
        }
        else if (newState == PlayerState.Playing)
        {
            lock (retryLock)
            {
                retryAttempt = 0;
            }
        }

        OnUi(() =>
        {
            ConnectionState = newState switch
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
        heartbeatTimer?.Dispose();
        heartbeatTimer = nowStreaming && !disposed
            ? time.CreateTimer(OnHeartbeatDue, state: null, renewInterval, renewInterval)
            : null;
    }

    private void OnHeartbeatDue(object? state)
        => _ = RenewLeaseAsync();

    private void ScheduleStreamRetry()
    {
        lock (retryLock)
        {
            if (disposed || stoppedByUser || IsResting)
            {
                return;
            }

            var delay = StreamRetryDelayFor(retryAttempt);
            retryAttempt++;
            retryGeneration++;
            retryTimer?.Dispose();
            retryTimer = time.CreateTimer(OnRetryDue, retryGeneration, delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Gets the wait before the new start after <paramref name="failures"/> failed ones.
    /// </summary>
    internal static TimeSpan StreamRetryDelayFor(int failures)
        => StreamRetryDelays[Math.Clamp(failures, 0, StreamRetryDelays.Length - 1)];

    // The server is back (the event stream opened again): a tile waiting to
    // start its stream again does so now, not after its back-off.
    private void OnEventServiceConnectedChanged()
    {
        if (!eventService.IsConnected)
        {
            return;
        }

        lock (retryLock)
        {
            if (disposed || stoppedByUser || retryTimer is null)
            {
                return;
            }

            retryGeneration++;
            retryTimer.Dispose();
            retryTimer = null;
        }

        _ = StartStreamOrRetryLaterAsync();
    }

    private void CancelStreamRetry()
    {
        lock (retryLock)
        {
            retryGeneration++;
            retryTimer?.Dispose();
            retryTimer = null;
            retryAttempt = 0;
        }
    }

    private void OnRetryDue(object? state)
    {
        lock (retryLock)
        {
            // A retry cancelled or replaced meanwhile does not run.
            if (state is not int generation || generation != retryGeneration || retryTimer is null)
            {
                return;
            }

            retryTimer.Dispose();
            retryTimer = null;
        }

        _ = StartStreamOrRetryLaterAsync();
    }

    /// <summary>
    /// Starts the stream; one that cannot start now (the server refused it,
    /// timed out or could not be reached) is tried again later. Never throws.
    /// </summary>
    public async Task StartStreamOrRetryLaterAsync()
    {
        if (disposed || IsResting)
        {
            return;
        }

        try
        {
            await StartStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStartFailure(ex))
        {
            // The server refused or could not be reached: try again later,
            // since the player does not change state without a stream.
            LogStreamStartFailed(ex, CameraId);
            ScheduleStreamRetry();
        }
    }

    /// <summary>
    /// Gets the shortest time between two motion events a tile shows: the
    /// server sends one for every picture it analyses. The end of motion
    /// always passes, so the boxes never stay on a still picture.
    /// </summary>
    internal static TimeSpan MotionInterval { get; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets a value indicating whether a full screen window shows
    /// this camera: its stream then plays even while the grid leaves it out.
    /// </summary>
    internal bool IsShownFullScreen
    {
        get => isShownFullScreen;
        set
        {
            isShownFullScreen = value;
            OnVisibilityChanged();
        }
    }

    // Left out of the grid and not shown full screen: the stream rests.
    private bool IsResting
        => !IsShownInGrid && !isShownFullScreen;

    private void OnVisibilityChanged()
    {
        if (disposed || stoppedByUser)
        {
            return;
        }

        if (IsResting)
        {
            Rest();
        }
        else if (!IsStreaming && !IsRetryScheduled)
        {
            _ = StartStreamOrRetryLaterAsync();
        }
    }

    // Closes the player and gives the lease back, without counting as the
    // user's stop: the stream starts again when the tile is shown.
    private void Rest()
    {
        CancelStreamRetry();
        lock (playerLock)
        {
            Player?.Close();
        }

        IsStreaming = false;
        CurrentBoundingBoxes = [];
        IsMotionDetected = false;
        if (TakeLease() is { } lease)
        {
            _ = streams.StopStreamAsync(CameraId, lease);
        }
    }

    /// <summary>
    /// Gets a value indicating whether a motion event is shown; it is then counted.
    /// </summary>
    internal bool TakeMotion(MotionDetectedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.CameraId != CameraId || IsResting)
        {
            return false;
        }

        var now = time.GetUtcNow();
        if (!e.IsMotionActive)
        {
            lastMotionShown = DateTimeOffset.MinValue;
            return true;
        }

        if (now - lastMotionShown < MotionInterval)
        {
            return false;
        }

        lastMotionShown = now;
        return true;
    }

    private void OnHubMotionDetected(MotionDetectedEvent e)
    {
        if (!TakeMotion(e))
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
                (MotionVideoWidth, MotionVideoHeight) = AtcSoft.Video.Surveillance.Helpers.MotionBoxMapper.GetDisplaySize(stream.Width, stream.Height, Rotation);
            }

            if (e.IsMotionActive && e.BoundingBoxes.Count > 0)
            {
                // With its track id, so the overlay smooths each box against its own track.
                CurrentBoundingBoxes = e.BoundingBoxes
                    .Select(b => new AtcSoft.Video.Surveillance.Models.BoundingBox { X = b.X, Y = b.Y, Width = b.Width, Height = b.Height, TrackId = b.TrackId })
                    .ToList();
            }
            else
            {
                CurrentBoundingBoxes = [];
            }
        });
    }
}