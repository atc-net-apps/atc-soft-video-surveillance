namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// View model for the camera edit dialog.
/// </summary>
public partial class CameraEditDialogViewModel : ViewModelBase
{
    private readonly Guid? cameraId;
    private readonly Func<Guid, Task<CameraConnectionType?>>? detectConnectionType;

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    [ObservableProperty(AfterChangedCallback = nameof(OnFormChanged))]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty(AfterChangedCallback = nameof(OnFormChanged))]
    private string ipAddress = string.Empty;

    [ObservableProperty]
    private int port = 554;

    [ObservableProperty(DependentPropertyNames = [nameof(SelectedProtocolKey)])]
    private CameraProtocol selectedProtocol = CameraProtocol.Rtsp;

    [ObservableProperty]
    private string path = string.Empty;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty(DependentPropertyNames = [nameof(SelectedConnectionTypeKey)])]
    private CameraConnectionType selectedConnectionType = CameraConnectionType.None;

    [ObservableProperty(DependentPropertyNames = [nameof(SelectedRotationKey)], AfterChangedCallback = nameof(OnSelectedRotationChanged))]
    private CameraRotation selectedRotation = CameraRotation.None;

    [ObservableProperty(DependentPropertyNames = [nameof(SelectedOverlayPositionKey)])]
    private CameraOverlayPosition selectedOverlayPosition = CameraOverlayPosition.TopLeft;

    [ObservableProperty]
    private bool streamUseLowLatencyMode;

    [ObservableProperty]
    private int streamMaxLatencyMs;

    [ObservableProperty(DependentPropertyNames = [nameof(SelectedRtspTransportKey)])]
    private CameraStreamRtspTransport selectedRtspTransport = CameraStreamRtspTransport.Tcp;

    [ObservableProperty]
    private int streamBufferDurationMs;

    /// <summary>
    /// Gets whether this is an edit operation.
    /// </summary>
    public bool IsEdit { get; }

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public string DialogTitle
        => IsEdit ? Translations.EditCameraTitle : Translations.AddCamera;

    /// <summary>
    /// Gets the protocol items for the combobox.
    /// </summary>
    /// <remarks>
    /// The keys of every drop-down are the API's wire values (<c>rtsp</c>, <c>topLeft</c>):
    /// a LabelComboBox's SelectedKey is a string, bound to the <c>Selected...Key</c> properties.
    /// </remarks>
    public IDictionary<string, string> ProtocolItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [CameraProtocol.Rtsp.ToString()] = "RTSP",
        [CameraProtocol.Http.ToString()] = "HTTP",
        [CameraProtocol.Https.ToString()] = "HTTPS",
    };

    /// <summary>
    /// Gets the connection types offered for a network camera.
    /// </summary>
    public IDictionary<string, string> ConnectionTypeItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [CameraConnectionType.None.ToString()] = Translations.ConnectionTypeNone,
        [CameraConnectionType.Lan.ToString()] = Translations.ConnectionTypeLan,
        [CameraConnectionType.Wifi.ToString()] = Translations.ConnectionTypeWifi,
    };

    /// <summary>
    /// Gets the clockwise rotations offered for the camera picture.
    /// </summary>
    public IDictionary<string, string> RotationItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [CameraRotation.None.ToString()] = Translations.RotationNone,
        [CameraRotation.Rotate90.ToString()] = Translations.RotationRotate90Degrees,
        [CameraRotation.Rotate180.ToString()] = Translations.RotationRotate180Degrees,
        [CameraRotation.Rotate270.ToString()] = Translations.RotationRotate270Degrees,
    };

    /// <summary>
    /// Gets the overlay position items for the combobox.
    /// </summary>
    public IDictionary<string, string> OverlayPositionItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [CameraOverlayPosition.TopLeft.ToString()] = Translations.OverlayPositionTopLeft,
        [CameraOverlayPosition.TopRight.ToString()] = Translations.OverlayPositionTopRight,
        [CameraOverlayPosition.BottomLeft.ToString()] = Translations.OverlayPositionBottomLeft,
        [CameraOverlayPosition.BottomRight.ToString()] = Translations.OverlayPositionBottomRight,
    };

    /// <summary>
    /// Gets the RTSP transport items for the combobox.
    /// </summary>
    public IDictionary<string, string> RtspTransportItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [CameraStreamRtspTransport.Tcp.ToString()] = "TCP",
        [CameraStreamRtspTransport.Udp.ToString()] = "UDP",
    };

    /// <summary>
    /// Gets or sets the key of <see cref="SelectedProtocol"/> in <see cref="ProtocolItems"/> (its wire value).
    /// </summary>
    public string SelectedProtocolKey
    {
        get => SelectedProtocol.ToString();
        set => SelectedProtocol = KnownValue.FromName(CameraProtocol.KnownValues, value, SelectedProtocol);
    }

    /// <summary>
    /// Gets or sets the key of <see cref="SelectedConnectionType"/> in <see cref="ConnectionTypeItems"/> (its wire value).
    /// </summary>
    public string SelectedConnectionTypeKey
    {
        get => SelectedConnectionType.ToString();
        set => SelectedConnectionType = KnownValue.FromName(CameraConnectionType.KnownValues, value, SelectedConnectionType);
    }

    /// <summary>
    /// Gets or sets the key of <see cref="SelectedRotation"/> in <see cref="RotationItems"/> (its wire value).
    /// </summary>
    public string SelectedRotationKey
    {
        get => SelectedRotation.ToString();
        set => SelectedRotation = KnownValue.FromName(CameraRotation.KnownValues, value, SelectedRotation);
    }

    /// <summary>
    /// Gets or sets the key of <see cref="SelectedOverlayPosition"/> in <see cref="OverlayPositionItems"/> (its wire value).
    /// </summary>
    public string SelectedOverlayPositionKey
    {
        get => SelectedOverlayPosition.ToString();
        set => SelectedOverlayPosition = KnownValue.FromName(CameraOverlayPosition.KnownValues, value, SelectedOverlayPosition);
    }

    /// <summary>
    /// Gets or sets the key of <see cref="SelectedRtspTransport"/> in <see cref="RtspTransportItems"/> (its wire value).
    /// </summary>
    public string SelectedRtspTransportKey
    {
        get => SelectedRtspTransport.ToString();
        set => SelectedRtspTransport = KnownValue.FromName(CameraStreamRtspTransport.KnownValues, value, SelectedRtspTransport);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraEditDialogViewModel"/> class for adding a new camera.
    /// </summary>
    public CameraEditDialogViewModel()
    {
        IsEdit = false;
        zoneDraft = new MotionZoneDraft([], AtcSoft.VideoSurveillance.Enums.CameraRotation.None);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraEditDialogViewModel"/> class for editing an existing camera.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="detectConnectionType">Asks the server for the camera's connection type.</param>
    /// <param name="takeSnapshot">Asks the server for a snapshot to draw the motion zones on.</param>
    public CameraEditDialogViewModel(
        Camera camera,
        Func<Guid, Task<CameraConnectionType?>>? detectConnectionType = null,
        Func<Guid, CancellationToken, Task<byte[]?>>? takeSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(camera);

        this.detectConnectionType = detectConnectionType;
        this.takeSnapshot = takeSnapshot;
        storedRotation = camera.Rotation.ToCoreRotation();
        zoneDraft = new MotionZoneDraft((camera.MotionZones ?? []).Select(z => z.ToCoreZone()), storedRotation);
        zonePictureStatus = takeSnapshot is null ? null : Translations.MotionZonesNoPicture;

        IsEdit = true;
        cameraId = camera.Id;
        displayName = camera.DisplayName;
        description = camera.Description ?? string.Empty;
        ipAddress = camera.IpAddress ?? string.Empty;
        port = camera.Port;
        selectedProtocol = camera.Protocol;
        path = camera.Path ?? string.Empty;
        username = camera.Username ?? string.Empty;
        selectedOverlayPosition = camera.OverlayPosition;
        selectedConnectionType = camera.ConnectionType;
        selectedRotation = camera.Rotation;
        streamUseLowLatencyMode = camera.StreamUseLowLatencyMode;
        streamMaxLatencyMs = camera.StreamMaxLatencyMs;
        selectedRtspTransport = camera.StreamRtspTransport;
        streamBufferDurationMs = camera.StreamBufferDurationMs;
    }

    /// <summary>
    /// Builds a <see cref="CreateCameraRequest"/> from the form fields.
    /// </summary>
    public CreateCameraRequest BuildCreateRequest()
        => new(
            DisplayName: DisplayName,
            Description: Description,
            Source: CameraSource.Network,
            ConnectionType: SelectedConnectionType,
            Rotation: SelectedRotation,
            IpAddress: IpAddress,
            Protocol: SelectedProtocol,
            Path: Path,
            Username: Username,
            Password: string.IsNullOrEmpty(Password) ? null! : Password,
            OverlayPosition: SelectedOverlayPosition,
            StreamUseLowLatencyMode: StreamUseLowLatencyMode,
            StreamMaxLatencyMs: StreamMaxLatencyMs,
            StreamRtspTransport: SelectedRtspTransport,
            StreamBufferDurationMs: StreamBufferDurationMs,
            Port: Port,
            MotionZones: ZonesToSend);

    /// <summary>
    /// Builds an <see cref="UpdateCameraRequest"/> from the form fields.
    /// </summary>
    /// <remarks>
    /// The dialog edits a network camera's fields. What it does not edit (the
    /// source, the USB settings, a password that was not typed again) is left
    /// out, so the server keeps it.
    /// </remarks>
    public UpdateCameraRequest BuildUpdateRequest()
        => new(
            DisplayName: DisplayName,
            Description: Description,
            ConnectionType: SelectedConnectionType,
            Rotation: SelectedRotation,
            IpAddress: IpAddress,
            Port: Port,
            Protocol: SelectedProtocol,
            Path: Path,
            Username: Username,
            Password: string.IsNullOrEmpty(Password) ? null! : Password,
            OverlayPosition: SelectedOverlayPosition,
            StreamUseLowLatencyMode: StreamUseLowLatencyMode,
            StreamMaxLatencyMs: StreamMaxLatencyMs,
            StreamRtspTransport: SelectedRtspTransport,
            StreamBufferDurationMs: StreamBufferDurationMs,
            MotionZones: ZonesToSend);

    /// <summary>
    /// Gets the camera ID for update operations.
    /// </summary>
    public Guid? CameraId => cameraId;

    /// <summary>
    /// Gets a value indicating whether server-side detection is available
    /// (edit mode only: the server probes with the stored credentials).
    /// </summary>
    public bool IsConnectionTypeDetectionAvailable
        => detectConnectionType is not null;

    [ObservableProperty]
    private string connectionTypeDetectionStatus = string.Empty;

    [ObservableProperty]
    private bool isDetectingConnectionType;

    /// <summary>
    /// Explicit detection: applies a detected LAN / Wi-Fi value to the picker;
    /// the user can still change it before saving.
    /// </summary>
    [RelayCommand("DetectConnectionType", CanExecute = nameof(CanDetectConnectionType))]
    private async Task DetectConnectionTypeAsync()
    {
        IsDetectingConnectionType = true;
        ConnectionTypeDetectionStatus = Translations.ConnectionTypeDetecting;

        try
        {
            var detected = await detectConnectionType!(cameraId!.Value).ConfigureAwait(true);
            if (detected?.Value is CameraConnectionType.Names.Lan or CameraConnectionType.Names.Wifi)
            {
                SelectedConnectionType = detected.Value;
                ConnectionTypeDetectionStatus = string.Format(CultureInfo.CurrentCulture, Translations.ConnectionTypeDetectedFormat1, ConnectionTypeItems[detected.Value.ToString()]);
            }
            else
            {
                ConnectionTypeDetectionStatus = Translations.ConnectionTypeDetectFailed;
            }
        }
        finally
        {
            IsDetectingConnectionType = false;
        }
    }

    private bool CanDetectConnectionType()
        => detectConnectionType is not null && cameraId is not null && !IsDetectingConnectionType;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: false));

    private bool CanSave()
        => !string.IsNullOrWhiteSpace(DisplayName) &&
           !string.IsNullOrWhiteSpace(IpAddress);

    private static void OnFormChanged()
        => CommandManager.InvalidateRequerySuggested();
}