namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Subscribes to the server's camera events (GET /events) and dispatches toast notifications
/// based on user preferences. Maintains a notification history log.
/// </summary>
public sealed class NotificationCoordinator : IDisposable
{
    private const int MaxHistoryEntries = 100;
    private static readonly TimeSpan MotionRateLimitInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ToastExpiration = TimeSpan.FromSeconds(5);

    private readonly SurveillanceEventService eventService;
    private readonly IToastNotificationService toastNotificationService;
    private readonly ConcurrentDictionary<Guid, string> cameraNameCache = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> lastMotionNotification = new();

    private bool isStarted;
    private bool isServerLost;
    private bool wasConnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationCoordinator"/> class.
    /// </summary>
    public NotificationCoordinator(
        SurveillanceEventService eventService,
        IToastNotificationService toastNotificationService)
    {
        ArgumentNullException.ThrowIfNull(eventService);
        ArgumentNullException.ThrowIfNull(toastNotificationService);

        this.eventService = eventService;
        this.toastNotificationService = toastNotificationService;
    }

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
        Application.Current?.Dispatcher.Invoke(() =>
        {
            // Add to history
            var entry = new NotificationEntry(DateTimeOffset.Now, title, message, eventType);
            History.Insert(0, entry);

            while (History.Count > MaxHistoryEntries)
            {
                History.RemoveAt(History.Count - 1);
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

    private string GetCameraName(Guid cameraId)
        => cameraNameCache.TryGetValue(cameraId, out var name)
            ? name
            : string.Format(CultureInfo.CurrentCulture, Translations.CameraShortId1, cameraId.ToString()[..8]);
}