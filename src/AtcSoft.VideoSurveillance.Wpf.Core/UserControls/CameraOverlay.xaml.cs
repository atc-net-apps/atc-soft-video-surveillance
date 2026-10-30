namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// Overlay control displaying camera information on a camera tile.
/// </summary>
/// <remarks>
/// CS0169 / CS0414 are suppressed via &lt;NoWarn&gt; in the .csproj —
/// [DependencyProperty] backing fields are read by the source generator.
/// </remarks>
public partial class CameraOverlay
{
    private DispatcherTimer? timeTimer;
    private Storyboard? recordingBlinkAnimation;

    [DependencyProperty]
    private string title = string.Empty;

    [DependencyProperty(PropertyChangedCallback = nameof(OnDescriptionChanged))]
    private string description = string.Empty;

    [DependencyProperty(
        DefaultValue = ConnectionState.Disconnected,
        PropertyChangedCallback = nameof(OnConnectionStateChanged))]
    private ConnectionState connectionState;

    [DependencyProperty]
    private bool hasDescription;

    [DependencyProperty]
    private Brush statusColor = Brushes.Gray;

    [DependencyProperty]
    private string statusText = string.Empty;

    [DependencyProperty(DefaultValue = true)]
    private bool showTitle;

    [DependencyProperty(DefaultValue = true)]
    private bool showDescription;

    [DependencyProperty(DefaultValue = true)]
    private bool showConnectionStatus;

    [DependencyProperty(DefaultValue = false, PropertyChangedCallback = nameof(OnShowTimeChanged))]
    private bool showTime;

    [DependencyProperty]
    private string currentTime = string.Empty;

    [DependencyProperty(DefaultValue = 0.6)]
    private double overlayOpacity;

    [DependencyProperty(DefaultValue = false, PropertyChangedCallback = nameof(OnIsRecordingChanged))]
    private bool isRecording;

    [DependencyProperty(PropertyChangedCallback = nameof(OnRecordingDurationChanged))]
    private TimeSpan recordingDuration;

    [DependencyProperty]
    private string recordingDurationText = string.Empty;

    [DependencyProperty(DefaultValue = false)]
    private bool isMotionDetected;

    [DependencyProperty(DefaultValue = false)]
    private bool enableRecordingOnMotion;

    /// <summary>
    /// True when the camera being rendered is a USB / webcam source.
    /// Drives the small "USB" badge in the corner of the overlay so
    /// operators can tell at a glance which tiles are local devices vs
    /// network cameras (especially useful in mixed deployments).
    /// </summary>
    [DependencyProperty(DefaultValue = false)]
    private bool isUsbSource;

    /// <summary>
    /// True when the underlying USB device has been physically unplugged
    /// (cable removed, device powered off). Surfaces a distinctive
    /// affordance over the existing status row so operators can tell
    /// "physical device gone" apart from "stream error" or "still
    /// reconnecting" — backoff doesn't help here; the host must wait
    /// for <see cref="AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher.DeviceArrived"/>.
    /// Modelled as an orthogonal bool (not a new <c>ConnectionState</c>
    /// value) because the overlay's <see cref="ConnectionState"/> DP is
    /// the <c>Atc.Network.ConnectionState</c> enum, which has no
    /// equivalent. Device-presence and stream-lifecycle are independent
    /// dimensions.
    /// </summary>
    [DependencyProperty(DefaultValue = false)]
    private bool isDeviceUnplugged;

    /// <summary>
    /// True when the underlying USB device opened successfully but every
    /// subsequent read failed — the canonical "another app holds the
    /// device's exclusive lock" pattern (Teams, browser camera test,
    /// OBS, etc.). Orthogonal to <see cref="ConnectionState"/> for the
    /// same reason as <see cref="IsDeviceUnplugged"/>: the operator
    /// sees both a red "ConnectionFailed" indicator *and* the amber
    /// "device in use" row at once, because both facts are true.
    /// Auto-reconnect keeps running so the tile resumes the moment the
    /// device is released.
    /// </summary>
    [DependencyProperty(DefaultValue = false)]
    private bool isDeviceBusy;

    // An app that uses this control from the package may never touch Translations, and
    // Atc.Wpf finds the texts only in assemblies already loaded: load them before the XAML.
    static CameraOverlay()
    {
        _ = Translations.ResourceManager;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraOverlay"/> class.
    /// </summary>
    public CameraOverlay()
    {
        InitializeComponent();
        StatusText = Translations.Disconnected;
        RecordingDurationText = TimeSpan.Zero.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
        => StopTimeTimer();

    private static void OnDescriptionChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraOverlay overlay)
        {
            overlay.HasDescription = !string.IsNullOrEmpty(e.NewValue as string);
        }
    }

    private static void OnConnectionStateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraOverlay overlay && e.NewValue is ConnectionState state)
        {
            overlay.UpdateConnectionVisuals(state);
        }
    }

    private void UpdateConnectionVisuals(ConnectionState state)
    {
        var (color, text) = state switch
        {
            ConnectionState.Connected => (Brushes.LimeGreen, Translations.Connected),
            ConnectionState.Connecting => (Brushes.Yellow, Translations.Connecting),
            ConnectionState.Reconnecting => (Brushes.Orange, Translations.Reconnecting),
            ConnectionState.ConnectionFailed => (Brushes.Red, Translations.Error),
            ConnectionState.Disconnected => (Brushes.Gray, Translations.Disconnected),
            _ => (Brushes.Gray, Translations.Unknown),
        };

        // Set dependency properties
        StatusColor = color;
        StatusText = text;

        // Also directly set UI elements in case bindings don't work in overlay window
        StatusIndicator.Fill = color;
        StatusTextBlock.Text = text;
    }

    private static void OnShowTimeChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not CameraOverlay overlay || e.NewValue is not bool showTime)
        {
            return;
        }

        if (showTime)
        {
            overlay.StartTimeTimer();
        }
        else
        {
            overlay.StopTimeTimer();
        }
    }

    private void StartTimeTimer()
    {
        if (timeTimer is not null)
        {
            return;
        }

        // Update time immediately
        UpdateCurrentTime();

        timeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        timeTimer.Tick += (_, _) => UpdateCurrentTime();
        timeTimer.Start();
    }

    private void StopTimeTimer()
    {
        timeTimer?.Stop();
        timeTimer = null;
    }

    private void UpdateCurrentTime()
    {
        CurrentTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private static void OnIsRecordingChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraOverlay overlay && e.NewValue is bool isRecording)
        {
            overlay.UpdateRecordingAnimation(isRecording);
        }
    }

    private static void OnRecordingDurationChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraOverlay overlay && e.NewValue is TimeSpan duration)
        {
            overlay.RecordingDurationText = duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }
    }

    private void UpdateRecordingAnimation(bool recording)
    {
        if (recording)
        {
            // Start the blink animation
            if (recordingBlinkAnimation is null && TryFindResource("RecordingBlinkAnimation") is Storyboard storyboard)
            {
                recordingBlinkAnimation = storyboard;
            }

            recordingBlinkAnimation?.Begin(RecordingDot, isControllable: true);
        }
        else
        {
            // Stop the blink animation
            recordingBlinkAnimation?.Stop(RecordingDot);
        }
    }
}