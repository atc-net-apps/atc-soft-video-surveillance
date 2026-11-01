namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// Subscribes to the server's camera events (GET /events) and dispatches toast notifications
/// based on user preferences. Maintains a notification history log.
/// </summary>
public sealed class NotificationCoordinator : IDisposable
{
    private const int MaxHistoryEntries = 100;
    private static readonly TimeSpan MotionRateLimitInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ToastExpiration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NameRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly SurveillanceEventService eventService;
    private readonly IToastNotificationService toastNotificationService;
    private readonly TimeProvider time;
    private readonly ConcurrentDictionary<Guid, string> cameraNameCache = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> lastMotionNotification = new();
    private readonly Queue<DateTimeOffset> toastTimes = new();
    private DateTimeOffset lastNameRefresh = DateTimeOffset.MinValue;

    private bool isStarted;
    private bool isServerLost;
    private bool wasConnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationCoordinator"/> class.
    /// </summary>
    public NotificationCoordinator(
        SurveillanceEventService eventService,
        IToastNotificationService toastNotificationService,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(eventService);
        ArgumentNullException.ThrowIfNull(toastNotificationService);

        this.eventService = eventService;
        this.toastNotificationService = toastNotificationService;
        this.time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the most toasts shown in <see cref="ToastWindow"/>; the rest of a
    /// burst goes into the history only.
    /// </summary>
    public static int MaxToastsPerWindow => 3;

    /// <summary>
    /// Gets the time over which <see cref="MaxToastsPerWindow"/> counts.
    /// </summary>
    public static TimeSpan ToastWindow { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how the camera names are read again (a camera added or renamed).
    /// </summary>
    public Func<Task<Camera[]?>>? LoadCameras { get; set; }

    /// <summary>
    /// Gets the notification history collection (UI-bindable).
    /// </summary>
    public ObservableCollection<NotificationEntry> History { get; } = [];

    /// <summary>
    /// Gets or sets the notification preferences.
    /// </summary>
    public NotificationPreferences Preferences { get; set; } = new();

    /// <summary>
    /// Pre-populates the camera name cache from an API result.
    /// </summary>
    public void UpdateCameraNameCache(IEnumerable<Camera>? cameras)
    {
        if (cameras is null)
        {
            return;
        }

        foreach (var camera in cameras)
        {
            cameraNameCache[camera.Id] = camera.DisplayName;
        }
    }

    /// <summary>
    /// Subscribes to the camera events and the server connection. Call after <c>mainWindow.Show()</c> to ensure the visual tree is ready.
    /// </summary>
    public void Start()
    {
        if (isStarted)
        {
            return;
        }

        isStarted = true;

        eventService.OnConnectionStateChanged += OnConnectionStateChanged;
        eventService.OnRecordingStateChanged += OnRecordingStateChanged;
        eventService.OnMotionDetected += OnMotionDetected;
        eventService.ConnectedChanged += OnServerConnectionChanged;
        eventService.OnMissedEvents += OnMissedEvents;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!isStarted)
        {
            return;
        }

        eventService.OnConnectionStateChanged -= OnConnectionStateChanged;
        eventService.OnRecordingStateChanged -= OnRecordingStateChanged;
        eventService.OnMotionDetected -= OnMotionDetected;
        eventService.ConnectedChanged -= OnServerConnectionChanged;
        eventService.OnMissedEvents -= OnMissedEvents;

        isStarted = false;
    }

    // The server could not be reached, or answers again; a stream that drops
    // and is opened again at once says nothing.
    private void OnServerConnectionChanged()
    {
        // A server that was not there at the start was never lost.
        var connected = eventService.IsConnected;
        var hadBeenConnected = wasConnected;
        wasConnected |= connected;
        if (!connected && hadBeenConnected && !isServerLost)
        {
            isServerLost = true;
            ShowNotification(
                Translations.ServerConnectionLost,
                Translations.ServerConnectionLostMessage,
                NotificationEventType.CameraDisconnected);
        }
        else if (connected && isServerLost)
        {
            isServerLost = false;
            ShowNotification(
                Translations.ServerConnected,
                Translations.ServerConnectionRestoredMessage,
                NotificationEventType.CameraReconnected);
        }
    }

    private void OnConnectionStateChanged(ConnectionStateChangedEvent e)
    {
        var cameraName = GetCameraName(e.CameraId);

        if (e.NewState == CameraConnectionState.Disconnected &&
            Preferences.NotifyOnDisconnect)
        {
            ShowNotification(
                cameraName,
                Translations.CameraDisconnectedNotification,
                NotificationEventType.CameraDisconnected);
        }
        else if (e.NewState == CameraConnectionState.Connected &&
                 Preferences.NotifyOnReconnect)
        {
            ShowNotification(
                cameraName,
                Translations.CameraConnectedNotification,
                NotificationEventType.CameraReconnected);
        }
    }

    private void OnRecordingStateChanged(RecordingStateChangedEvent e)
    {
        var cameraName = GetCameraName(e.CameraId);

        var isNowRecording = HubStates.IsRecording(e.NewState);
        var wasRecording = HubStates.IsRecording(e.OldState);

        if (isNowRecording && Preferences.NotifyOnRecordingStarted)
        {
            ShowNotification(
                cameraName,
                Translations.RecordingStartedNotification,
                NotificationEventType.RecordingStarted);
        }
        else if (!isNowRecording && wasRecording && Preferences.NotifyOnRecordingStopped)
        {
            ShowNotification(
                cameraName,
                Translations.RecordingStoppedNotification,
                NotificationEventType.RecordingStopped);
        }
    }

    private void OnMotionDetected(MotionDetectedEvent e)
    {
        if (!e.IsMotionActive || !Preferences.NotifyOnMotionDetected)
        {
            return;
        }

        // Rate-limit: max 1 per camera per 10 seconds
        var now = DateTimeOffset.UtcNow;
        if (lastMotionNotification.TryGetValue(e.CameraId, out var lastTime) &&
            now - lastTime < MotionRateLimitInterval)
        {
            return;
        }

        lastMotionNotification[e.CameraId] = now;

        var cameraName = GetCameraName(e.CameraId);
        ShowNotification(
            cameraName,
            string.Format(CultureInfo.CurrentCulture, Translations.MotionDetectedPercent1, e.ChangePercentage),
            NotificationEventType.MotionDetected);
    }

    private void ShowNotification(
        string title,
        string message,
        NotificationEventType eventType)
    {
        // Every notification goes into the history; a burst (thirty cameras
        // flapping after a server restart) shows only its first few as toasts.
        var showToast = TakeToast();

        // Not waited for: the event reader goes on while the UI thread shows it.
        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            // Add to history
            var entry = new NotificationEntry(DateTimeOffset.Now, title, message, eventType);
            History.Insert(0, entry);

            while (History.Count > MaxHistoryEntries)
            {
                History.RemoveAt(History.Count - 1);
            }

            if (!showToast)
            {
                return;
            }

            // Show toast
            toastNotificationService.ShowInformation(
                title,
                message,
                useDesktop: true,
                expirationTime: ToastExpiration);

            // Play sound if enabled
            if (Preferences.PlaySound)
            {
                SystemSounds.Exclamation.Play();
            }
        });
    }

    /// <summary>
    /// Takes one of the toasts the window allows; false when the burst has used them up.
    /// </summary>
    internal bool TakeToast()
    {
        lock (toastTimes)
        {
            var now = time.GetUtcNow();
            while (toastTimes.Count > 0 && now - toastTimes.Peek() >= ToastWindow)
            {
                toastTimes.Dequeue();
            }

            if (toastTimes.Count >= MaxToastsPerWindow)
            {
                return false;
            }

            toastTimes.Enqueue(now);
            return true;
        }
    }

    private string GetCameraName(Guid cameraId)
    {
        if (cameraNameCache.TryGetValue(cameraId, out var name))
        {
            return name;
        }

        // A camera added since the names were read: they are read again for the next time.
        RefreshCameraNames();
        return string.Format(CultureInfo.CurrentCulture, Translations.CameraShortId1, cameraId.ToString()[..8]);
    }

    private void OnMissedEvents()
        => RefreshCameraNames();

    // Reads the names again, at most once a minute (cameras renamed or added).
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A name that cannot be read is shown by its id; it must not stop the notifications.")]
    private void RefreshCameraNames()
    {
        if (LoadCameras is not { } load)
        {
            return;
        }

        var now = time.GetUtcNow();
        lock (toastTimes)
        {
            if (now - lastNameRefresh < NameRefreshInterval)
            {
                return;
            }

            lastNameRefresh = now;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                UpdateCameraNameCache(await load().ConfigureAwait(false));
            }
            catch (Exception)
            {
                // The server is away: the names are read the next time.
            }
        });
    }
}