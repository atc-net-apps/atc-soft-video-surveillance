// ReSharper disable InvertIf
namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public partial class CameraConfigurationDialogViewModel : ViewModelDialogBase
{
    private readonly CameraConfiguration originalCamera;
    private readonly bool isNew;
    private readonly IReadOnlyCollection<(string IpAddress, string? Path)> existingEndpoints;
    private readonly IApplicationSettingsService settingsService;
    private readonly IVideoPlayerFactory videoPlayerFactory;
    private readonly AtcSoft.VideoSurveillance.Services.IUsbCameraEnumerator usbEnumerator;
    private readonly AtcSoft.VideoSurveillance.Services.ICameraConnectionTypeDetector? connectionTypeDetector;
    private readonly Func<CancellationToken, Task<byte[]?>>? livePicture;

    [ObservableProperty(AfterChangedCallback = nameof(OnIsTestingChanged))]
    private bool isTesting;

    private string? testResultInternal;

    public string TestResult => testResultInternal ?? Translations.NotTested;

    private void ClearTestResult()
    {
        if (testResultInternal is not null)
        {
            testResultInternal = null;
            OnPropertyChanged(nameof(TestResult));
        }
    }

    private void SetTestResult(string value)
    {
        if (testResultInternal != value)
        {
            testResultInternal = value;
            OnPropertyChanged(nameof(TestResult));
        }
    }

    [ObservableProperty]
    private string? ipAddressError;

    public CameraConfigurationDialogViewModel(
        CameraConfiguration camera,
        bool isNew,
        IReadOnlyCollection<(string IpAddress, string? Path)> existingEndpoints,
        IApplicationSettingsService settingsService,
        IVideoPlayerFactory videoPlayerFactory,
        AtcSoft.VideoSurveillance.Services.IUsbCameraEnumerator usbEnumerator,
        AtcSoft.VideoSurveillance.Services.ICameraConnectionTypeDetector? connectionTypeDetector = null,
        Func<CancellationToken, Task<byte[]?>>? livePicture = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(videoPlayerFactory);
        ArgumentNullException.ThrowIfNull(usbEnumerator);

        originalCamera = camera;
        this.isNew = isNew;
        this.existingEndpoints = existingEndpoints ?? [];
        this.settingsService = settingsService;
        this.videoPlayerFactory = videoPlayerFactory;

        // Required, not nullable-with-Null-default. The previous shape
        // let DialogService silently forget to pass the enumerator,
        // which left the dropdown empty in the standalone app even
        // though the Windows MF enumerator was registered. Force every
        // caller to think about which enumerator they want.
        this.usbEnumerator = usbEnumerator;
        this.connectionTypeDetector = connectionTypeDetector;
        this.livePicture = livePicture;

        UsbDevices = [];

        // Create a clone for editing - changes only apply when Save is clicked
        Camera = camera.Clone();
        zoneDraft = new MotionZoneDraft(Camera.MotionZones, Camera.Display.Rotation);

        Camera.PropertyChanged += OnCameraPropertyChanged;
        NetworkScanner.EntrySelected += OnNetworkScannerEntrySelected;

        // Pre-populate USB devices for cameras that already use the
        // USB source (edit mode). The collection stays empty for
        // network cameras and re-fills on demand when the operator
        // flips the source radio. Skipped when the local picker drives
        // the dialog: the Atc.Wpf.Hardware picker enumerates via WinRT
        // DeviceInformation in its own service, so calling our Media
        // Foundation enumerator here would just duplicate work — and on
        // .NET 10 the in-process MF probe is brittle (MF_E_ATTRIBUTENOTFOUND
        // out of MFEnumDeviceSources on STA→MTA hand-off).
        if (Camera.Connection.Source == CameraSource.Usb && !UseLocalUsbPicker)
        {
            RefreshUsbDevices();
        }
    }

    private static void OnIsTestingChanged()
        => CommandManager.InvalidateRequerySuggested();

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    public CameraConfiguration Camera { get; }

    public string DialogTitle
        => isNew
            ? Translations.AddCamera
            : Translations.EditCamera;

    /// <summary>
    /// Gets a value indicating whether this is a new camera being added.
    /// </summary>
    public bool IsNew => isNew;

    /// <summary>
    /// Gets a value indicating whether an existing camera is being edited.
    /// When editing, connection settings (IP, port, protocol) are read-only.
    /// </summary>
    public bool IsEditing => !isNew;

    /// <summary>
    /// Gets a value indicating whether connection settings can be modified.
    /// Connection settings are only editable for new cameras.
    /// </summary>
    public bool CanEditConnectionSettings => isNew;

    /// <summary>
    /// Gets a value indicating whether the Source radio (Network / USB)
    /// can be changed. Disabled on existing cameras because changing
    /// source = different camera; the dropdown should match the
    /// existing <see cref="CanEditConnectionSettings"/> rule.
    /// </summary>
    public bool CanEditSource => isNew;

    /// <summary>
    /// Gets a value indicating whether the USB device selection (device
    /// dropdown + refresh) can be changed. Disabled on existing cameras
    /// because the symbolic link is the camera's identity. Format / FPS /
    /// pixel-format / audio toggles remain editable — they don't change
    /// identity.
    /// </summary>
    public bool CanEditUsbDevice => isNew;

    /// <summary>
    /// Convenience predicate for XAML visibility triggers — the
    /// network-only branch (BasicConnectionSettings, Authentication,
    /// NetworkScanner) is shown when this is <see langword="true"/>.
    /// </summary>
    public bool IsNetworkSource
    {
        get => Camera.Connection.Source == CameraSource.Network;
        set
        {
            if (value && Camera.Connection.Source != CameraSource.Network)
            {
                SwitchToNetworkSource();
            }
        }
    }

    /// <summary>
    /// Convenience predicate for XAML visibility triggers — the USB
    /// branch (UsbDevicePart) is shown when this is
    /// <see langword="true"/>.
    /// </summary>
    public bool IsUsbSource
    {
        get => Camera.Connection.Source == CameraSource.Usb;
        set
        {
            if (value && Camera.Connection.Source != CameraSource.Usb)
            {
                SwitchToUsbSource();
            }
        }
    }

    private void SwitchToNetworkSource()
    {
        Camera.Connection.Source = CameraSource.Network;

        // Reset USB-specific fields so a half-saved USB config can't
        // leak into a network camera. Keep ip/port/protocol since the
        // user may still edit them; defaults already populate fresh
        // cameras.
        Camera.Connection.Usb = null;
        Camera.Connection.ConnectionType = CameraConnectionType.None;

        ClearTestResult();
        OnPropertyChanged(nameof(IsNetworkSource));
        OnPropertyChanged(nameof(IsUsbSource));
        OnPropertyChanged(nameof(SelectedConnectionTypeKey));
        CommandManager.InvalidateRequerySuggested();
    }

    private void SwitchToUsbSource()
    {
        Camera.Connection.Source = CameraSource.Usb;

        // Reset network-specific fields so a half-saved network config
        // can't leak into a USB camera (e.g. an IP address with a
        // dshow pipeline behind it makes no sense).
        Camera.Connection.IpAddress = string.Empty;
        Camera.Connection.Port = 0;
        Camera.Connection.Path = null;
        Camera.Authentication.UserName = string.Empty;
        Camera.Authentication.Password = string.Empty;
        Camera.Connection.Usb ??= new UsbConnectionSettings();
        Camera.Connection.ConnectionType = CameraConnectionType.Usb;

        ClearTestResult();
        IpAddressError = null;
        OnPropertyChanged(nameof(IsNetworkSource));
        OnPropertyChanged(nameof(IsUsbSource));
        OnPropertyChanged(nameof(SelectedProtocolKey));
        OnPropertyChanged(nameof(SelectedConnectionTypeKey));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Gets the connection types offered for a network camera (USB is implied
    /// by the source and never picked by hand).
    /// </summary>
    public IDictionary<string, string> ConnectionTypeItems { get; } = DropDownItemsFactory.ConnectionTypeItems;

    /// <summary>
    /// Gets or sets the connection type as a dropdown key. For network cameras
    /// only None / Lan / Wifi are accepted.
    /// </summary>
    public string SelectedConnectionTypeKey
    {
        get => Camera.Connection.ConnectionType.ToString();
        set
        {
            if (Enum.TryParse<CameraConnectionType>(value, out var connectionType) &&
                connectionType != CameraConnectionType.Usb &&
                Camera.Connection.ConnectionType != connectionType)
            {
                Camera.Connection.ConnectionType = connectionType;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a connection-type detector is available
    /// (the Detect button is hidden otherwise).
    /// </summary>
    public bool IsConnectionTypeDetectionAvailable
        => connectionTypeDetector is not null;

    /// <summary>
    /// Gets or sets the inline result of the last connection-type detection.
    /// </summary>
    [ObservableProperty]
    private string connectionTypeDetectionStatus = string.Empty;

    [ObservableProperty]
    private bool isDetectingConnectionType;

    /// <summary>
    /// Explicit detection: the user asked, so the detected value may
    /// overwrite the current one. The user can still change it afterwards.
    /// </summary>
    [RelayCommand("DetectConnectionType", CanExecute = nameof(CanDetectConnectionType))]
    private Task DetectConnectionTypeAsync()
        => DetectAndApplyConnectionTypeAsync(isExplicit: true);

    private bool CanDetectConnectionType()
        => connectionTypeDetector is not null &&
           !IsDetectingConnectionType &&
           Camera.Connection.Source == CameraSource.Network &&
           !string.IsNullOrWhiteSpace(Camera.Connection.IpAddress);

    private async Task DetectAndApplyConnectionTypeAsync(bool isExplicit)
    {
        if (!CanDetectConnectionType())
        {
            return;
        }

        IsDetectingConnectionType = true;
        ConnectionTypeDetectionStatus = Translations.ConnectionTypeDetecting;

        try
        {
            var detected = await connectionTypeDetector!
                .DetectAsync(
                    Camera.Connection.IpAddress,
                    Camera.Authentication.UserName,
                    Camera.Authentication.Password)
                .ConfigureAwait(true);

            SelectedConnectionTypeKey = CameraConnectionTypeHelper
                .ApplyDetected(Camera.Connection.ConnectionType, detected, isExplicit)
                .ToString();

            ConnectionTypeDetectionStatus = detected == CameraConnectionType.None
                ? (isExplicit ? Translations.ConnectionTypeDetectFailed : string.Empty)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Translations.ConnectionTypeDetectedFormat1,
                    ConnectionTypeIconHelper.GetDisplayName(detected));
        }
        finally
        {
            IsDetectingConnectionType = false;
        }
    }

    public IDictionary<string, string> ProtocolItems { get; } = Enum<CameraProtocol>.ToDictionaryWithStringKey();

    public string SelectedProtocolKey
    {
        get => Camera.Connection.Protocol.ToString();
        set
        {
            if (Enum.TryParse<CameraProtocol>(value, out var protocol))
            {
                Camera.Connection.Protocol = protocol;
                RaisePropertyChanged();
            }
        }
    }

    public IDictionary<string, string> OverlayPositionItems
        => DropDownItemsFactory.OverlayPositionItems;

    public string SelectedOverlayPositionKey
    {
        get => Camera.Display.OverlayPosition.ToString();
        set
        {
            if (Enum.TryParse<OverlayPosition>(value, out var position))
            {
                Camera.Display.OverlayPosition = position;
                RaisePropertyChanged();
            }
        }
    }

    public IDictionary<string, string> RotationItems
        => DropDownItemsFactory.RotationItems;

    public string SelectedRotationKey
    {
        get => Camera.Display.Rotation.ToString();
        set
        {
            if (Enum.TryParse<CameraRotation>(value, out var rotation))
            {
                Camera.Display.Rotation = rotation;
                RaisePropertyChanged();
                TurnZones(rotation);
            }
        }
    }

    public IDictionary<string, string> RtspTransportItems
        => DropDownItemsFactory.RtspTransportItems;

    public IDictionary<string, string> VideoQualityItems
        => DropDownItemsFactory.VideoQualityItems;

    public IDictionary<string, string> RecordingFormatItems
        => DropDownItemsFactory.RecordingFormatItems;

    public IDictionary<string, string> TranscodeVideoCodecItems
        => AtcSoft.VideoSurveillance.Factories.DropDownItemsFactory.TranscodeVideoCodecItems;

    public IDictionary<string, string> OpacityItems
        => DropDownItemsFactory.OverlayOpacityItems;

    public string SelectedRtspTransportKey
    {
        get => Camera.Stream.RtspTransport;
        set
        {
            Camera.Stream.RtspTransport = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultShowOverlayTitle
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayTitle is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.CameraDisplay.ShowOverlayTitle = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.ShowOverlayTitle = settingsService.CameraDisplay.ShowOverlayTitle;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideShowOverlayTitle));
        }
    }

    public bool OverrideShowOverlayTitle
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayTitle ?? settingsService.CameraDisplay.ShowOverlayTitle;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.CameraDisplay.ShowOverlayTitle = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultShowOverlayDescription
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayDescription is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.CameraDisplay.ShowOverlayDescription = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.ShowOverlayDescription = settingsService.CameraDisplay.ShowOverlayDescription;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideShowOverlayDescription));
        }
    }

    public bool OverrideShowOverlayDescription
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayDescription ?? settingsService.CameraDisplay.ShowOverlayDescription;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.CameraDisplay.ShowOverlayDescription = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultShowOverlayTime
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayTime is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.CameraDisplay.ShowOverlayTime = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.ShowOverlayTime = settingsService.CameraDisplay.ShowOverlayTime;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideShowOverlayTime));
        }
    }

    public bool OverrideShowOverlayTime
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayTime ?? settingsService.CameraDisplay.ShowOverlayTime;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.CameraDisplay.ShowOverlayTime = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultShowOverlayConnectionStatus
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayConnectionStatus is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.CameraDisplay.ShowOverlayConnectionStatus = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.ShowOverlayConnectionStatus = settingsService.CameraDisplay.ShowOverlayConnectionStatus;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideShowOverlayConnectionStatus));
        }
    }

    public bool OverrideShowOverlayConnectionStatus
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayConnectionStatus ?? settingsService.CameraDisplay.ShowOverlayConnectionStatus;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.CameraDisplay.ShowOverlayConnectionStatus = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultShowOverlayQuickActions
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayQuickActions is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.CameraDisplay.ShowOverlayQuickActions = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.ShowOverlayQuickActions = settingsService.CameraDisplay.ShowOverlayQuickActions;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideShowOverlayQuickActions));
        }
    }

    public bool OverrideShowOverlayQuickActions
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayQuickActions ?? settingsService.CameraDisplay.ShowOverlayQuickActions;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.CameraDisplay.ShowOverlayQuickActions = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultOverlayOpacity
    {
        get => Camera.Overrides?.CameraDisplay.OverlayOpacity is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.CameraDisplay.OverlayOpacity = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.OverlayOpacity = settingsService.CameraDisplay.OverlayOpacity;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideOverlayOpacity));
        }
    }

    public string OverrideOverlayOpacity
    {
        get
        {
            var opacity = Camera.Overrides?.CameraDisplay.OverlayOpacity ?? settingsService.CameraDisplay.OverlayOpacity;
            return opacity.ToString("F1", CultureInfo.InvariantCulture);
        }

        set
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.OverlayOpacity = opacity;
                RaisePropertyChanged();
            }
        }
    }

    public bool UseDefaultOverlayPosition
    {
        get => Camera.Overrides?.CameraDisplay.OverlayPosition is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.CameraDisplay.OverlayPosition = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.OverlayPosition = settingsService.CameraDisplay.OverlayPosition;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideOverlayPositionKey));
            RaisePropertyChanged(nameof(ConnectionTypeIconPlacementItems));
        }
    }

    public string OverrideOverlayPositionKey
    {
        get => (Camera.Overrides?.CameraDisplay.OverlayPosition ?? settingsService.CameraDisplay.OverlayPosition).ToString();
        set
        {
            if (Enum.TryParse<OverlayPosition>(value, out var position))
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.OverlayPosition = position;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ConnectionTypeIconPlacementItems));
            }
        }
    }

    public bool UseDefaultShowOverlayConnectionTypeIcon
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayConnectionTypeIcon is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.CameraDisplay.ShowOverlayConnectionTypeIcon = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.ShowOverlayConnectionTypeIcon = settingsService.CameraDisplay.ShowOverlayConnectionTypeIcon;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideShowOverlayConnectionTypeIcon));
        }
    }

    public bool OverrideShowOverlayConnectionTypeIcon
    {
        get => Camera.Overrides?.CameraDisplay.ShowOverlayConnectionTypeIcon ?? settingsService.CameraDisplay.ShowOverlayConnectionTypeIcon;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.CameraDisplay.ShowOverlayConnectionTypeIcon = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultConnectionTypeIconPlacement
    {
        get => Camera.Overrides?.CameraDisplay.ConnectionTypeIconPlacement is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.CameraDisplay.ConnectionTypeIconPlacement = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.ConnectionTypeIconPlacement = settingsService.CameraDisplay.ConnectionTypeIconPlacement;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideConnectionTypeIconPlacementKey));
        }
    }

    public string OverrideConnectionTypeIconPlacementKey
    {
        get => (Camera.Overrides?.CameraDisplay.ConnectionTypeIconPlacement ?? settingsService.CameraDisplay.ConnectionTypeIconPlacement).ToString();
        set
        {
            if (Enum.TryParse<ConnectionTypeIconPlacement>(value, out var placement))
            {
                EnsureOverrides();
                Camera.Overrides!.CameraDisplay.ConnectionTypeIconPlacement = placement;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the icon placements, labelled with the concrete corner they resolve
    /// to for this camera's effective overlay position.
    /// </summary>
    public IDictionary<string, string> ConnectionTypeIconPlacementItems
        => DropDownItemsFactory.GetConnectionTypeIconPlacementItems(
            Camera.Overrides?.CameraDisplay.OverlayPosition ?? Camera.Display.OverlayPosition);

    public bool UseDefaultConnectionTimeout
    {
        get => Camera.Overrides?.Connection.ConnectionTimeoutSeconds is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Connection.ConnectionTimeoutSeconds = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Connection.ConnectionTimeoutSeconds = settingsService.Connection.ConnectionTimeoutSeconds;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideConnectionTimeout));
        }
    }

    public int OverrideConnectionTimeout
    {
        get => Camera.Overrides?.Connection.ConnectionTimeoutSeconds ?? settingsService.Connection.ConnectionTimeoutSeconds;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Connection.ConnectionTimeoutSeconds = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultReconnectDelay
    {
        get => Camera.Overrides?.Connection.ReconnectDelaySeconds is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Connection.ReconnectDelaySeconds = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Connection.ReconnectDelaySeconds = settingsService.Connection.ReconnectDelaySeconds;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideReconnectDelay));
        }
    }

    public int OverrideReconnectDelay
    {
        get => Camera.Overrides?.Connection.ReconnectDelaySeconds ?? settingsService.Connection.ReconnectDelaySeconds;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Connection.ReconnectDelaySeconds = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultAutoReconnect
    {
        get => Camera.Overrides?.Connection.AutoReconnectOnFailure is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Connection.AutoReconnectOnFailure = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Connection.AutoReconnectOnFailure = settingsService.Connection.AutoReconnectOnFailure;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideAutoReconnect));
        }
    }

    public bool OverrideAutoReconnect
    {
        get => Camera.Overrides?.Connection.AutoReconnectOnFailure ?? settingsService.Connection.AutoReconnectOnFailure;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Connection.AutoReconnectOnFailure = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultVideoQuality
    {
        get => Camera.Overrides?.Performance.VideoQuality is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Performance.VideoQuality = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Performance.VideoQuality = settingsService.Performance.VideoQuality;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideVideoQuality));
        }
    }

    public string OverrideVideoQuality
    {
        get => Camera.Overrides?.Performance.VideoQuality ?? settingsService.Performance.VideoQuality;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Performance.VideoQuality = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultHardwareAcceleration
    {
        get => Camera.Overrides?.Performance.HardwareAcceleration is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Performance.HardwareAcceleration = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Performance.HardwareAcceleration = settingsService.Performance.HardwareAcceleration;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideHardwareAcceleration));
        }
    }

    public bool OverrideHardwareAcceleration
    {
        get => Camera.Overrides?.Performance.HardwareAcceleration ?? settingsService.Performance.HardwareAcceleration;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Performance.HardwareAcceleration = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultRecordingPath
    {
        get => Camera.Overrides?.Recording.RecordingPath is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Recording.RecordingPath = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Recording.RecordingPath = settingsService.Recording.RecordingPath ?? string.Empty;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideRecordingPath));
        }
    }

    public DirectoryInfo? OverrideRecordingPath
    {
        get
        {
            var path = Camera.Overrides?.Recording.RecordingPath ?? settingsService.Recording.RecordingPath;
            return string.IsNullOrEmpty(path) ? null : new DirectoryInfo(path);
        }

        set
        {
            EnsureOverrides();
            Camera.Overrides!.Recording.RecordingPath = value?.FullName;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultRecordingFormat
    {
        get => Camera.Overrides?.Recording.RecordingFormat is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Recording.RecordingFormat = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Recording.RecordingFormat = settingsService.Recording.RecordingFormat;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideRecordingFormat));
        }
    }

    public string OverrideRecordingFormat
    {
        get => Camera.Overrides?.Recording.RecordingFormat ?? settingsService.Recording.RecordingFormat;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Recording.RecordingFormat = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultTranscodeVideoCodec
    {
        get => Camera.Overrides?.Recording.TranscodeVideoCodec is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Recording.TranscodeVideoCodec = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Recording.TranscodeVideoCodec = settingsService.Recording.TranscodeVideoCodec;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideTranscodeVideoCodec));
        }
    }

    public string OverrideTranscodeVideoCodec
    {
        get => (Camera.Overrides?.Recording.TranscodeVideoCodec ?? settingsService.Recording.TranscodeVideoCodec).ToString();
        set
        {
            // The combo box hands over a key; anything else is not a choice.
            if (!TranscodeVideoCodecItems.ContainsKey(value ?? string.Empty) ||
                !Enum.TryParse<VideoTranscodeCodec>(value, out var codec))
            {
                return;
            }

            EnsureOverrides();
            Camera.Overrides!.Recording.TranscodeVideoCodec = codec;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultRecordingOnMotion
    {
        get => Camera.Overrides?.Recording.EnableRecordingOnMotion is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Recording.EnableRecordingOnMotion = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Recording.EnableRecordingOnMotion = settingsService.Recording.EnableRecordingOnMotion;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideRecordingOnMotion));
        }
    }

    public bool OverrideRecordingOnMotion
    {
        get => Camera.Overrides?.Recording.EnableRecordingOnMotion ?? settingsService.Recording.EnableRecordingOnMotion;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Recording.EnableRecordingOnMotion = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultMotionSensitivity
    {
        get => Camera.Overrides?.MotionDetection.Sensitivity is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.Sensitivity = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.Sensitivity = settingsService.MotionDetection.Sensitivity;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideMotionSensitivity));
        }
    }

    public int OverrideMotionSensitivity
    {
        get => Camera.Overrides?.MotionDetection.Sensitivity ?? settingsService.MotionDetection.Sensitivity;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.Sensitivity = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultPostMotionDuration
    {
        get => Camera.Overrides?.MotionDetection.PostMotionDurationSeconds is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.PostMotionDurationSeconds = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.PostMotionDurationSeconds = settingsService.MotionDetection.PostMotionDurationSeconds;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverridePostMotionDuration));
        }
    }

    public int OverridePostMotionDuration
    {
        get => Camera.Overrides?.MotionDetection.PostMotionDurationSeconds ?? settingsService.MotionDetection.PostMotionDurationSeconds;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.PostMotionDurationSeconds = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultRecordingOnConnect
    {
        get => Camera.Overrides?.Recording.EnableRecordingOnConnect is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Recording.EnableRecordingOnConnect = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Recording.EnableRecordingOnConnect = settingsService.Recording.EnableRecordingOnConnect;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideRecordingOnConnect));
        }
    }

    public bool OverrideRecordingOnConnect
    {
        get => Camera.Overrides?.Recording.EnableRecordingOnConnect ?? settingsService.Recording.EnableRecordingOnConnect;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Recording.EnableRecordingOnConnect = value;
            RaisePropertyChanged();
        }
    }

    // Motion Detection Overrides
    public bool UseDefaultSmallestObjectPercent
    {
        get => Camera.Overrides?.MotionDetection.SmallestObjectPercent is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.SmallestObjectPercent = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.SmallestObjectPercent = settingsService.MotionDetection.SmallestObjectPercent;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideSmallestObjectPercent));
            RaisePropertyChanged(nameof(CameraSmallestObjectPercent));
        }
    }

    public decimal OverrideSmallestObjectPercent
    {
        get => (decimal)CameraSmallestObjectPercent;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.SmallestObjectPercent = (double)value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(CameraSmallestObjectPercent));
        }
    }

    /// <summary>
    /// Gets the camera's smallest object in percent: its own, or the app's. A watch zone without its own takes it.
    /// </summary>
    public double CameraSmallestObjectPercent
        => Camera.Overrides?.MotionDetection.SmallestObjectPercent ?? settingsService.MotionDetection.SmallestObjectPercent;

    public bool UseDefaultStoppedObjectSeconds
    {
        get => Camera.Overrides?.MotionDetection.StoppedObjectSeconds is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.StoppedObjectSeconds = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.StoppedObjectSeconds = settingsService.MotionDetection.StoppedObjectSeconds;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideStoppedObjectSeconds));
        }
    }

    public int OverrideStoppedObjectSeconds
    {
        get => Camera.Overrides?.MotionDetection.StoppedObjectSeconds ?? settingsService.MotionDetection.StoppedObjectSeconds;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.StoppedObjectSeconds = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultIgnoreLightingChanges
    {
        get => Camera.Overrides?.MotionDetection.IgnoreLightingChanges is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.IgnoreLightingChanges = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.IgnoreLightingChanges = settingsService.MotionDetection.IgnoreLightingChanges;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideIgnoreLightingChanges));
        }
    }

    public bool OverrideIgnoreLightingChanges
    {
        get => Camera.Overrides?.MotionDetection.IgnoreLightingChanges ?? settingsService.MotionDetection.IgnoreLightingChanges;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.IgnoreLightingChanges = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultAnalysisFrameRate
    {
        get => Camera.Overrides?.MotionDetection.AnalysisFrameRate is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.AnalysisFrameRate = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.AnalysisFrameRate = settingsService.MotionDetection.AnalysisFrameRate;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideAnalysisFrameRate));
        }
    }

    public int OverrideAnalysisFrameRate
    {
        get => Camera.Overrides?.MotionDetection.AnalysisFrameRate ?? settingsService.MotionDetection.AnalysisFrameRate;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.AnalysisFrameRate = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultCooldownSeconds
    {
        get => Camera.Overrides?.MotionDetection.CooldownSeconds is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.CooldownSeconds = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.CooldownSeconds = settingsService.MotionDetection.CooldownSeconds;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideCooldownSeconds));
        }
    }

    public int OverrideCooldownSeconds
    {
        get => Camera.Overrides?.MotionDetection.CooldownSeconds ?? settingsService.MotionDetection.CooldownSeconds;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.CooldownSeconds = value;
            RaisePropertyChanged();
        }
    }

    // Bounding Box Overrides
    public bool UseDefaultShowBoundingBoxInGrid
    {
        get => Camera.Overrides?.MotionDetection.BoundingBox.ShowInGrid is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.BoundingBox.ShowInGrid = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.BoundingBox.ShowInGrid = settingsService.MotionDetection.BoundingBox.ShowInGrid;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideShowBoundingBoxInGrid));
        }
    }

    public bool OverrideShowBoundingBoxInGrid
    {
        get => Camera.Overrides?.MotionDetection.BoundingBox.ShowInGrid ?? settingsService.MotionDetection.BoundingBox.ShowInGrid;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.BoundingBox.ShowInGrid = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultShowBoundingBoxInFullScreen
    {
        get => Camera.Overrides?.MotionDetection.BoundingBox.ShowInFullScreen is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.BoundingBox.ShowInFullScreen = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.BoundingBox.ShowInFullScreen = settingsService.MotionDetection.BoundingBox.ShowInFullScreen;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideShowBoundingBoxInFullScreen));
        }
    }

    public bool OverrideShowBoundingBoxInFullScreen
    {
        get => Camera.Overrides?.MotionDetection.BoundingBox.ShowInFullScreen ?? settingsService.MotionDetection.BoundingBox.ShowInFullScreen;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.BoundingBox.ShowInFullScreen = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultBoundingBoxColor
    {
        get => Camera.Overrides?.MotionDetection.BoundingBox.Color is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.BoundingBox.Color = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.BoundingBox.Color = settingsService.MotionDetection.BoundingBox.Color;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideBoundingBoxColor));
        }
    }

    public string OverrideBoundingBoxColor
    {
        get => Camera.Overrides?.MotionDetection.BoundingBox.Color ?? settingsService.MotionDetection.BoundingBox.Color;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.BoundingBox.Color = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultBoundingBoxThickness
    {
        get => Camera.Overrides?.MotionDetection.BoundingBox.Thickness is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.BoundingBox.Thickness = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.BoundingBox.Thickness = settingsService.MotionDetection.BoundingBox.Thickness;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideBoundingBoxThickness));
        }
    }

    public string OverrideBoundingBoxThickness
    {
        get => (Camera.Overrides?.MotionDetection.BoundingBox.Thickness ?? settingsService.MotionDetection.BoundingBox.Thickness).ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var thickness))
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.BoundingBox.Thickness = thickness;
                RaisePropertyChanged();
            }
        }
    }

    public IDictionary<string, string> BoundingBoxThicknessItems
        => DropDownItemsFactory.BoundingBoxThicknessItems;

    public IDictionary<string, string> AnalysisResolutionItems
        => DropDownItemsFactory.MotionAnalysisResolutionItemsWith(OverrideAnalysisResolution);

    public IDictionary<string, string> ThumbnailTileCountItems
        => DropDownItemsFactory.ThumbnailTileCountItems;

    public IDictionary<string, string> TimelapseIntervalItems
        => DropDownItemsFactory.TimelapseIntervalItems;

    public bool UseDefaultThumbnailTileCount
    {
        get => Camera.Overrides?.Recording.ThumbnailTileCount is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Recording.ThumbnailTileCount = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Recording.ThumbnailTileCount = settingsService.Recording.ThumbnailTileCount;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideThumbnailTileCount));
        }
    }

    public string OverrideThumbnailTileCount
    {
        get => (Camera.Overrides?.Recording.ThumbnailTileCount ?? settingsService.Recording.ThumbnailTileCount).ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tileCount))
            {
                EnsureOverrides();
                Camera.Overrides!.Recording.ThumbnailTileCount = tileCount;
                RaisePropertyChanged();
            }
        }
    }

    public bool UseDefaultEnableTimelapse
    {
        get => Camera.Overrides?.Recording.EnableTimelapse is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Recording.EnableTimelapse = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Recording.EnableTimelapse = settingsService.Recording.EnableTimelapse;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideEnableTimelapse));
        }
    }

    public bool OverrideEnableTimelapse
    {
        get => Camera.Overrides?.Recording.EnableTimelapse ?? settingsService.Recording.EnableTimelapse;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Recording.EnableTimelapse = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultTimelapseInterval
    {
        get => Camera.Overrides?.Recording.TimelapseInterval is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.Recording.TimelapseInterval = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.Recording.TimelapseInterval = settingsService.Recording.TimelapseInterval;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideTimelapseInterval));
        }
    }

    public string OverrideTimelapseInterval
    {
        get => Camera.Overrides?.Recording.TimelapseInterval ?? settingsService.Recording.TimelapseInterval;
        set
        {
            EnsureOverrides();
            Camera.Overrides!.Recording.TimelapseInterval = value;
            RaisePropertyChanged();
        }
    }

    public bool UseDefaultAnalysisResolution
    {
        get => Camera.Overrides?.MotionDetection.AnalysisWidth is null;
        set
        {
            if (value)
            {
                if (Camera.Overrides is not null)
                {
                    Camera.Overrides.MotionDetection.AnalysisWidth = null;
                    Camera.Overrides.MotionDetection.AnalysisHeight = null;
                }
            }
            else
            {
                EnsureOverrides();
                Camera.Overrides!.MotionDetection.AnalysisWidth = DropDownItemsFactory.ParseAnalysisResolution(
                    DropDownItemsFactory.FormatAnalysisResolution(settingsService.MotionDetection.AnalysisWidth));
                Camera.Overrides!.MotionDetection.AnalysisHeight = null;
            }

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(OverrideAnalysisResolution));
        }
    }

    public string OverrideAnalysisResolution
    {
        get => DropDownItemsFactory.FormatAnalysisResolution(
            Camera.Overrides?.MotionDetection.AnalysisWidth ?? settingsService.MotionDetection.AnalysisWidth);

        set
        {
            EnsureOverrides();
            Camera.Overrides!.MotionDetection.AnalysisWidth = DropDownItemsFactory.ParseAnalysisResolution(value);
            Camera.Overrides!.MotionDetection.AnalysisHeight = null;
            RaisePropertyChanged();
        }
    }

    private void EnsureOverrides()
    {
        Camera.Overrides ??= new CameraOverrides();
    }

    public NetworkScannerViewModel NetworkScanner { get; } = CreateNetworkScanner();

    private static NetworkScannerViewModel CreateNetworkScanner()
    {
        var scanner = new NetworkScannerViewModel
        {
            // Common camera ports: RTSP, HTTP, HTTP alt, RTSP alt
            PortsNumbers = [554, 80, 8080, 8554],
        };

        var localIpAddress = IPv4AddressHelper.GetLocalAddress();
        if (localIpAddress is not null)
        {
            // Use /24 subnet (255.255.255.0) which is typical for most local networks
            var (startIp, endIp) = IPv4AddressHelper.GetFirstAndLastAddressInRange(localIpAddress, cidrLength: 24);
            scanner.StartIpAddress = startIp.ToString();
            scanner.EndIpAddress = endIp.ToString();
        }

        return scanner;
    }

    private void OnNetworkScannerEntrySelected(
        object? sender,
        NetworkHostSelectedEventArgs e)
    {
        if (e.SelectedHost is null)
        {
            return;
        }

        // Populate IP address
        Camera.Connection.IpAddress = e.SelectedHost.IpAddress.ToString();

        // Set display name from hostname if available and display name is default
        if (!string.IsNullOrWhiteSpace(e.SelectedHost.Hostname) &&
            (string.IsNullOrWhiteSpace(Camera.Display.DisplayName) || Camera.Display.DisplayName == Translations.NewCamera))
        {
            Camera.Display.DisplayName = e.SelectedHost.Hostname;
        }

        // Auto-detect protocol and port from open ports
        var openPorts = e.SelectedHost.OpenPortNumbers.ToList();
        if (openPorts.Contains(554))
        {
            Camera.Connection.Protocol = CameraProtocol.Rtsp;
            Camera.Connection.Port = 554;
        }
        else if (openPorts.Contains(8554))
        {
            Camera.Connection.Protocol = CameraProtocol.Rtsp;
            Camera.Connection.Port = 8554;
        }
        else if (openPorts.Contains(80))
        {
            Camera.Connection.Protocol = CameraProtocol.Http;
            Camera.Connection.Port = 80;
        }
        else if (openPorts.Contains(8080))
        {
            Camera.Connection.Protocol = CameraProtocol.Http;
            Camera.Connection.Port = 8080;
        }
        else if (openPorts.Count > 0)
        {
            // Use first open port
            Camera.Connection.Port = openPorts[0];
        }

        // Notify property changes for combo boxes
        OnPropertyChanged(nameof(SelectedProtocolKey));
    }

    private void OnCameraPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        // Reset test result when connection or authentication properties change
        if (e.PropertyName is nameof(Camera.Connection) or nameof(Camera.Authentication))
        {
            ClearTestResult();
        }

        // Validate IP address + path uniqueness when connection changes
        if (e.PropertyName == nameof(Camera.Connection))
        {
            ValidateCameraEndpoint();
        }

        // Always invalidate commands when any property changes (for Save button CanExecute)
        CommandManager.InvalidateRequerySuggested();
    }

    private void ValidateCameraEndpoint()
    {
        if (string.IsNullOrWhiteSpace(Camera.Connection.IpAddress))
        {
            IpAddressError = null;
            return;
        }

        IpAddressError = !IsCameraEndpointUnique() ? Translations.IpAddressAlreadyExists : null;
    }

    private bool IsCameraEndpointUnique()
    {
        // USB cameras don't pass through the existing-endpoint
        // collision check — duplicate USB camera entries are
        // operator-meaningful (same device, two recording configs)
        // and OS-level single-open enforcement applies regardless.
        if (Camera.Connection.Source == CameraSource.Usb)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(Camera.Connection.IpAddress))
        {
            return true;
        }

        var currentPath = Camera.Connection.Path;

        return !existingEndpoints.Any(e =>
            string.Equals(e.IpAddress, Camera.Connection.IpAddress, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                string.IsNullOrEmpty(e.Path) ? null : e.Path,
                string.IsNullOrEmpty(currentPath) ? null : currentPath,
                StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnection()
    {
        IsTesting = true;
        ClearTestResult();
        var success = false;

        try
        {
            // BuildSourceLocator handles both Network (rtsp://) and
            // USB (dshow:video=...) paths transparently; the player
            // reads the locator's InputFormat / RawDeviceSpec to pick
            // the right FFmpeg open path.
            var locator = AtcSoft.VideoSurveillance.Helpers.CameraUriHelper.BuildSourceLocator(Camera.Core);
            success = await TestStreamWithPlayerAsync(locator).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetTestResult(string.Format(CultureInfo.CurrentCulture, Translations.FailedWithStatus1, ex.Message));
        }
        finally
        {
            IsTesting = false;
        }

        // Implicit detection: the stream opened with these credentials, so an
        // ONVIF probe is likely to authenticate too. Only fills a value that
        // is still "not set". Runs after the test so it doesn't hold IsTesting.
        if (success &&
            Camera.Connection.ConnectionType == CameraConnectionType.None &&
            !string.IsNullOrEmpty(Camera.Authentication.UserName))
        {
            await DetectAndApplyConnectionTypeAsync(isExplicit: false).ConfigureAwait(true);
        }
    }

    private async Task<bool> TestStreamWithPlayerAsync(
        AtcSoft.VideoSurveillance.Helpers.SourceLocator locator)
    {
        using var player = videoPlayerFactory.Create();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tcs = new TaskCompletionSource<bool>();

        player.StateChanged += OnPlayerStateChanged;

        try
        {
            player.Open(locator.Uri, CreateStreamOptions(locator));
            return await WaitForConnectionResultAsync(tcs, cts.Token).ConfigureAwait(false);
        }
        finally
        {
            player.StateChanged -= OnPlayerStateChanged;
            player.Close();
        }

        void OnPlayerStateChanged(
            object? sender,
            PlayerStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case PlayerState.Playing:
                    tcs.TrySetResult(true);
                    break;
                case PlayerState.Error:
                    tcs.TrySetResult(false);
                    break;
            }
        }
    }

    private StreamOptions CreateStreamOptions(
        AtcSoft.VideoSurveillance.Helpers.SourceLocator locator)
        => new()
        {
            Source = Camera.Display.DisplayName,
            InputFormat = locator.InputFormat switch
            {
                "dshow" => InputFormatKind.Dshow,
                "v4l2" => InputFormatKind.V4l2,
                "avfoundation" => InputFormatKind.AVFoundation,
                _ => InputFormatKind.Auto,
            },
            RawDeviceSpec = locator.RawDeviceSpec,
            VideoSize = locator.VideoSize,
            FrameRate = locator.FrameRate,
            PixelFormat = locator.PixelFormat,
        };

    private async Task<bool> WaitForConnectionResultAsync(
        TaskCompletionSource<bool> tcs,
        CancellationToken cancellationToken)
    {
        try
        {
            var success = await tcs.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            SetTestResult(success ? Translations.ConnectionSuccessful : Translations.StreamOpenFailed);
            return success;
        }
        catch (OperationCanceledException)
        {
            SetTestResult(Translations.ConnectionTimedOut);
            return false;
        }
    }

    private bool CanTestConnection()
        => !IsTesting && HasMinimumSourceIdentity();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        // Copy edited values back to the original camera
        originalCamera.CopyFrom(Camera);

        CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
    }

    private bool HasMinimumSourceIdentity()
        => Camera.Connection.Source switch
        {
            CameraSource.Usb => Camera.Connection.Usb is not null
                && (!string.IsNullOrWhiteSpace(Camera.Connection.Usb.DeviceId)
                    || !string.IsNullOrWhiteSpace(Camera.Connection.Usb.FriendlyName)),
            _ => !string.IsNullOrWhiteSpace(Camera.Connection.IpAddress),
        };

    private bool CanSave()
        => !string.IsNullOrWhiteSpace(Camera.Display.DisplayName) &&
           HasMinimumSourceIdentity() &&
           HasValidValues() &&
           IsCameraEndpointUnique() &&
           HasChanges();

    /// <summary>
    /// Checks the validation attributes on the models, so a value the form
    /// flags as invalid (a port out of range, a name that is too long) cannot
    /// be saved. The address and port rules only apply to network cameras.
    /// </summary>
    private bool HasValidValues()
        => IsValid(Camera.Display) &&
           (Camera.Connection.Source == CameraSource.Usb || IsValid(Camera.Connection));

    private static bool IsValid(object model)
        => Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            validationResults: null,
            validateAllProperties: true);

    private bool HasChanges()
    {
        // For new cameras, always consider as having changes if fields are filled
        if (isNew)
        {
            return true;
        }

        // For existing cameras, compare with original to detect changes
        return !Camera.ValueEquals(originalCamera);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: false));
    }

    // -- USB device picker ---------------------------------------

    /// <summary>
    /// Snapshot of devices visible to the host's enumerator. Refreshed
    /// on demand via <see cref="RefreshUsbDevicesCommand"/> and on
    /// initial Source=Usb load. The list is intentionally an
    /// <see cref="ObservableCollection{T}"/> so the XAML combo binds
    /// directly without an intermediate copy.
    /// </summary>
    public ObservableCollection<AtcSoft.VideoSurveillance.Models.UsbDeviceDescriptor> UsbDevices { get; }

    /// <summary>
    /// Currently-selected device. Setting this writes back to
    /// <see cref="CameraConfiguration.Connection"/>.<c>Usb</c> so the
    /// camera carries the right symbolic-link identity at Save time.
    /// </summary>
    public AtcSoft.VideoSurveillance.Models.UsbDeviceDescriptor? SelectedUsbDevice
    {
        get
        {
            var deviceId = Camera.Connection.Usb?.DeviceId;
            if (string.IsNullOrEmpty(deviceId))
            {
                return null;
            }

            foreach (var d in UsbDevices)
            {
                if (string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return d;
                }
            }

            return null;
        }

        set
        {
            EnsureUsbConnectionSettings();

            Camera.Connection.Usb!.DeviceId = value?.DeviceId ?? string.Empty;
            Camera.Connection.Usb.FriendlyName = value?.FriendlyName ?? string.Empty;

            // Reset the format triple when the device changes — the
            // previous selection's format may be unsupported by the
            // newly-picked device.
            Camera.Connection.Usb.Format = null;

            OnPropertyChanged();
            OnPropertyChanged(nameof(UsbWidth));
            OnPropertyChanged(nameof(UsbHeight));
            OnPropertyChanged(nameof(UsbFrameRate));
            OnPropertyChanged(nameof(SelectedUsbFrameRate));
            OnPropertyChanged(nameof(UsbPixelFormat));
            OnPropertyChanged(nameof(UsbResolutionItems));
            OnPropertyChanged(nameof(SelectedUsbResolution));
            OnPropertyChanged(nameof(UsbFrameRateItems));
            OnPropertyChanged(nameof(UsbPixelFormatItems));

            // Pick the highest-resolution capability triple by default
            // so the operator sees a usable selection immediately rather
            // than three empty combos.
            TryApplyDefaultUsbFormat();

            ClearTestResult();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private Atc.Wpf.Hardware.Models.UsbCameraInfo? selectedUsbCameraField;

    /// <summary>
    /// Local-picker counterpart to <see cref="SelectedUsbDevice"/>.
    /// Bound TwoWay to <c>atc:LabelUsbCameraPicker.Value</c> so the
    /// <see cref="Atc.Wpf.Hardware.Pickers.UsbCameraPicker"/>'s internal
    /// <c>UsbCameraService</c> drives selection. Only consulted when
    /// <see cref="UseLocalUsbPicker"/> is <see langword="true"/>; the
    /// API-client app keeps the existing <see cref="SelectedUsbDevice"/>
    /// path against the gateway-backed enumerator.
    /// </summary>
    public Atc.Wpf.Hardware.Models.UsbCameraInfo? SelectedUsbCamera
    {
        get => selectedUsbCameraField;

        set
        {
            if (ReferenceEquals(selectedUsbCameraField, value))
            {
                return;
            }

            // Detach the previous camera's PropertyChanged hook so we
            // stop reacting to its SupportedFormats updates after a
            // different device is picked.
            if (selectedUsbCameraField is not null)
            {
                selectedUsbCameraField.PropertyChanged -= OnSelectedUsbCameraPropertyChanged;
            }

            selectedUsbCameraField = value;

            if (selectedUsbCameraField is not null)
            {
                selectedUsbCameraField.PropertyChanged += OnSelectedUsbCameraPropertyChanged;
            }

            // Capture the previously-bound device id BEFORE we mutate
            // Camera.Connection.Usb. This drives the format-reset
            // decision below: in edit-mode the code-behind rebinds the
            // picker to the *same* device the camera was loaded with,
            // and we must keep the saved Width/Height/FrameRate/PixelFormat
            // intact rather than wiping them as if the user had picked
            // a different device.
            var previousDeviceId = Camera.Connection.Usb?.DeviceId;

            EnsureUsbConnectionSettings();

            var newDeviceId = value?.DeviceId ?? string.Empty;
            Camera.Connection.Usb!.DeviceId = newDeviceId;
            Camera.Connection.Usb.FriendlyName = value?.FriendlyName ?? string.Empty;

            // Reset the format triple only when the device actually
            // changes — the previous selection's format may be
            // unsupported by the newly-picked device. Initial edit-mode
            // rebind hits this setter with the same device id and must
            // not clobber the saved choice.
            if (!string.Equals(previousDeviceId, newDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                Camera.Connection.Usb.Format = null;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(UsbWidth));
            OnPropertyChanged(nameof(UsbHeight));
            OnPropertyChanged(nameof(UsbFrameRate));
            OnPropertyChanged(nameof(SelectedUsbFrameRate));
            OnPropertyChanged(nameof(UsbPixelFormat));
            OnPropertyChanged(nameof(UsbResolutionItems));
            OnPropertyChanged(nameof(SelectedUsbResolution));
            OnPropertyChanged(nameof(UsbFrameRateItems));
            OnPropertyChanged(nameof(UsbPixelFormatItems));

            // For the synchronous descriptor path, capabilities are
            // immediately available — pre-fill defaults so the combos
            // aren't empty. The local-picker path runs again from
            // OnSelectedUsbCameraPropertyChanged once SupportedFormats
            // arrives lazily.
            TryApplyDefaultUsbFormat();

            ClearTestResult();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void OnSelectedUsbCameraPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        // SupportedFormats is the only field the live preview populates
        // lazily; everything else is set in the constructor and never
        // changes for the same UsbCameraInfo instance.
        if (e.PropertyName != nameof(Atc.Wpf.Hardware.Models.UsbCameraInfo.SupportedFormats))
        {
            return;
        }

        OnPropertyChanged(nameof(UsbResolutionItems));
        OnPropertyChanged(nameof(UsbFrameRateItems));
        OnPropertyChanged(nameof(UsbPixelFormatItems));
        OnPropertyChanged(nameof(SelectedUsbResolution));

        // SupportedFormats arrived — populate any missing format fields
        // so the cascading combos render with defaults.
        TryApplyDefaultUsbFormat();
    }

    /// <summary>
    /// <see langword="true"/> when the dialog is hosted by an app that
    /// enumerates USB cameras on the local Windows host (the standalone
    /// CameraWall app). Drives whether the camera-source view shows the
    /// new <c>atc:LabelUsbCameraPicker</c> with live preview, or keeps
    /// the descriptor-driven dropdown used by the API-client.
    /// </summary>
    public bool UseLocalUsbPicker => usbEnumerator.IsLocalEnumerator;

    public int UsbWidth
    {
        get => Camera.Connection.Usb?.Format?.Width ?? 0;
        set
        {
            EnsureUsbFormat();
            Camera.Connection.Usb!.Format!.Width = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedUsbResolution));
            OnPropertyChanged(nameof(UsbFrameRateItems));
            OnPropertyChanged(nameof(UsbPixelFormatItems));
        }
    }

    public int UsbHeight
    {
        get => Camera.Connection.Usb?.Format?.Height ?? 0;
        set
        {
            EnsureUsbFormat();
            Camera.Connection.Usb!.Format!.Height = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedUsbResolution));
            OnPropertyChanged(nameof(UsbFrameRateItems));
            OnPropertyChanged(nameof(UsbPixelFormatItems));
        }
    }

    public double UsbFrameRate
    {
        get => Camera.Connection.Usb?.Format?.FrameRate ?? 0;
        set
        {
            EnsureUsbFormat();
            Camera.Connection.Usb!.Format!.FrameRate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedUsbFrameRate));
            OnPropertyChanged(nameof(UsbPixelFormatItems));

            // Cascade: pixel format may be invalid at the new frame
            // rate. Pick the first valid format so the combo never
            // shows a stale value.
            TryApplyDefaultUsbFormat();
        }
    }

    /// <summary>
    /// The frame-rate combo's selection. Picking another camera replaces the
    /// frame-rate items, and the combo then pushes <see langword="null"/> back:
    /// into a <see langword="double"/> that is a conversion error on the combo,
    /// so the combo binds here and a <see langword="null"/> is ignored. The
    /// defaults for the new camera follow from <see cref="TryApplyDefaultUsbFormat"/>.
    /// </summary>
    public double? SelectedUsbFrameRate
    {
        get => Camera.Connection.Usb?.Format?.FrameRate is > 0 and var rate ? rate : null;
        set
        {
            if (value is { } rate)
            {
                UsbFrameRate = rate;
            }
        }
    }

    public string UsbPixelFormat
    {
        get => Camera.Connection.Usb?.Format?.PixelFormat ?? string.Empty;
        set
        {
            EnsureUsbFormat();
            Camera.Connection.Usb!.Format!.PixelFormat = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool UsbCaptureAudio
    {
        get => Camera.Connection.Usb?.PreferAudio ?? false;
        set
        {
            EnsureUsbConnectionSettings();
            Camera.Connection.Usb!.PreferAudio = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// DirectShow friendly name of the companion audio capture device
    /// (e.g. <c>Microphone (Logitech BRIO)</c>). Free-form because the
    /// dshow demuxer accepts whatever shows up in
    /// <c>ffmpeg -list_devices true -f dshow -i dummy</c>. Only consulted
    /// at stream-open time when <see cref="UsbCaptureAudio"/> is true —
    /// the empty / set-while-disabled state is safe.
    /// </summary>
    public string UsbAudioDeviceName
    {
        get => Camera.Connection.Usb?.AudioDeviceName ?? string.Empty;
        set
        {
            EnsureUsbConnectionSettings();
            Camera.Connection.Usb!.AudioDeviceName = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    private Atc.Wpf.Hardware.Models.AudioDeviceInfo? selectedAudioInputField;

    /// <summary>
    /// Companion to <see cref="UsbAudioDeviceName"/>. Bound TwoWay to
    /// the local <c>atc:LabelAudioInputPicker</c> so the dialog round-
    /// trips an <see cref="Atc.Wpf.Hardware.Models.AudioDeviceInfo"/>
    /// instead of forcing the operator to type a DirectShow friendly
    /// name. Writes the picked device's <c>FriendlyName</c> back to
    /// <see cref="UsbAudioDeviceName"/> so the dshow URL builder keeps
    /// working unchanged. Edit-mode rebind by friendly name happens in
    /// the part's code-behind once the picker's <c>Devices</c>
    /// collection populates.
    /// </summary>
    public Atc.Wpf.Hardware.Models.AudioDeviceInfo? SelectedAudioInput
    {
        get => selectedAudioInputField;

        set
        {
            if (ReferenceEquals(selectedAudioInputField, value))
            {
                return;
            }

            selectedAudioInputField = value;

            EnsureUsbConnectionSettings();
            Camera.Connection.Usb!.AudioDeviceName = value?.FriendlyName ?? string.Empty;

            OnPropertyChanged();
            OnPropertyChanged(nameof(UsbAudioDeviceName));
        }
    }

    /// <summary>
    /// Distinct <c>"Width × Height"</c> strings derived from the
    /// selected device's capability list. The dialog binds the
    /// resolution combo to this so operators only see triples the
    /// device actually advertises. Sources from
    /// <see cref="SelectedUsbCamera"/>'s lazy
    /// <see cref="Atc.Wpf.Hardware.Models.UsbCameraInfo.SupportedFormats"/>
    /// when the local picker is in use, otherwise the descriptor's
    /// synchronous capability list.
    /// </summary>
    public IReadOnlyList<string> UsbResolutionItems
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<string>();
            foreach (var c in EnumerateSelectedDeviceCapabilities())
            {
                var label = FormatResolution(c.Width, c.Height);
                if (seen.Add(label))
                {
                    items.Add(label);
                }
            }

            // Largest first — common UX convention for capture pickers.
            items.Sort((a, b) =>
            {
                var aDim = ParseResolution(a);
                var bDim = ParseResolution(b);
                return (bDim.W * bDim.H).CompareTo(aDim.W * aDim.H);
            });

            return items;
        }
    }

    public string SelectedUsbResolution
    {
        get
        {
            if (UsbWidth > 0 && UsbHeight > 0)
            {
                return FormatResolution(UsbWidth, UsbHeight);
            }

            return string.Empty;
        }

        set
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var (w, h) = ParseResolution(value);
            if (w == 0 || h == 0)
            {
                return;
            }

            EnsureUsbFormat();
            Camera.Connection.Usb!.Format!.Width = w;
            Camera.Connection.Usb.Format.Height = h;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsbWidth));
            OnPropertyChanged(nameof(UsbHeight));
            OnPropertyChanged(nameof(UsbFrameRateItems));
            OnPropertyChanged(nameof(UsbPixelFormatItems));

            // Cascade: the previously-selected frame rate / pixel format
            // may not be supported at the new resolution. Replace them
            // with the highest valid FPS and first valid pixel format
            // so the operator never sees an inconsistent triple.
            TryApplyDefaultUsbFormat();
        }
    }

    /// <summary>
    /// Distinct frame-rate values for the selected device + currently
    /// selected resolution. Cascades from <see cref="SelectedUsbResolution"/>
    /// so operators only see frame rates the device supports at that
    /// size.
    /// </summary>
    public IReadOnlyList<double> UsbFrameRateItems
    {
        get
        {
            if (UsbWidth == 0 || UsbHeight == 0)
            {
                return [];
            }

            var seen = new HashSet<double>();
            var items = new List<double>();
            foreach (var c in EnumerateSelectedDeviceCapabilities())
            {
                if (c.Width == UsbWidth && c.Height == UsbHeight && c.FrameRate > 0 && seen.Add(c.FrameRate))
                {
                    items.Add(c.FrameRate);
                }
            }

            items.Sort((a, b) => b.CompareTo(a));
            return items;
        }
    }

    /// <summary>
    /// Distinct pixel-format strings for the selected device +
    /// resolution + frame rate.
    /// </summary>
    public IReadOnlyList<string> UsbPixelFormatItems
    {
        get
        {
            if (UsbWidth == 0 || UsbHeight == 0)
            {
                return [];
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<string>();
            foreach (var c in EnumerateSelectedDeviceCapabilities())
            {
                if (c.Width == UsbWidth
                    && c.Height == UsbHeight
                    && (UsbFrameRate < 0.0001 || Math.Abs(c.FrameRate - UsbFrameRate) < 0.05)
                    && !string.IsNullOrEmpty(c.PixelFormat)
                    && seen.Add(c.PixelFormat))
                {
                    items.Add(c.PixelFormat);
                }
            }

            items.Sort(StringComparer.Ordinal);
            return items;
        }
    }

    /// <summary>
    /// Yields capability triples from whichever picker is driving the
    /// dialog. The local picker (<see cref="SelectedUsbCamera"/>)
    /// publishes formats lazily after live preview opens — until then
    /// this enumerable is empty. The remote/descriptor path
    /// (<see cref="SelectedUsbDevice"/>) ships a synchronous snapshot.
    /// </summary>
    /// <summary>
    /// Fills in missing or stale entries of
    /// <see cref="UsbConnectionSettings.Format"/> from the selected
    /// device's capability list:
    /// <list type="bullet">
    /// <item>Resolution: highest pixel-count if not set.</item>
    /// <item>Frame rate: highest valid FPS at the current resolution if
    ///     the current FPS is missing or unsupported.</item>
    /// <item>Pixel format: first valid pixel format at the current
    ///     resolution + FPS if the current value is missing or
    ///     unsupported.</item>
    /// </list>
    /// Idempotent — re-running with a fully-valid format is a no-op,
    /// so edit-mode rebinds keep saved selections intact.
    /// </summary>
    private void TryApplyDefaultUsbFormat()
    {
        if (Camera.Connection.Usb is null)
        {
            return;
        }

        var caps = new List<(int Width, int Height, double FrameRate, string PixelFormat)>();
        foreach (var c in EnumerateSelectedDeviceCapabilities())
        {
            caps.Add(c);
        }

        if (caps.Count == 0)
        {
            return;
        }

        // 1. Resolution — only apply when no resolution is set yet.
        if (UsbWidth <= 0 || UsbHeight <= 0)
        {
            var best = caps[0];
            for (var i = 1; i < caps.Count; i++)
            {
                if (caps[i].Width * caps[i].Height > best.Width * best.Height)
                {
                    best = caps[i];
                }
            }

            EnsureUsbFormat();
            Camera.Connection.Usb!.Format!.Width = best.Width;
            Camera.Connection.Usb.Format.Height = best.Height;
            OnPropertyChanged(nameof(UsbWidth));
            OnPropertyChanged(nameof(UsbHeight));
            OnPropertyChanged(nameof(SelectedUsbResolution));
            OnPropertyChanged(nameof(UsbFrameRateItems));
            OnPropertyChanged(nameof(UsbPixelFormatItems));
        }

        // 2. Frame rate — restrict to triples at the current resolution
        //    and replace when the saved value isn't in that set.
        double bestFps = 0;
        var fpsAvailable = false;
        var fpsCurrentMatches = false;
        for (var i = 0; i < caps.Count; i++)
        {
            var c = caps[i];
            if (c.Width != UsbWidth || c.Height != UsbHeight || c.FrameRate <= 0)
            {
                continue;
            }

            fpsAvailable = true;
            if (c.FrameRate > bestFps)
            {
                bestFps = c.FrameRate;
            }

            if (UsbFrameRate > 0.0001 && Math.Abs(c.FrameRate - UsbFrameRate) < 0.05)
            {
                fpsCurrentMatches = true;
            }
        }

        if (fpsAvailable && !fpsCurrentMatches)
        {
            EnsureUsbFormat();
            Camera.Connection.Usb!.Format!.FrameRate = bestFps;
            OnPropertyChanged(nameof(UsbFrameRate));
            OnPropertyChanged(nameof(SelectedUsbFrameRate));
            OnPropertyChanged(nameof(UsbPixelFormatItems));
        }

        // 3. Pixel format — first valid format at the current
        //    resolution + frame rate. Skip when nothing is advertised
        //    so we don't write an empty string back.
        string? firstPixelFormat = null;
        var pfCurrentMatches = false;
        for (var i = 0; i < caps.Count; i++)
        {
            var c = caps[i];
            if (c.Width != UsbWidth || c.Height != UsbHeight)
            {
                continue;
            }

            if (UsbFrameRate > 0.0001 && Math.Abs(c.FrameRate - UsbFrameRate) >= 0.05)
            {
                continue;
            }

            if (string.IsNullOrEmpty(c.PixelFormat))
            {
                continue;
            }

            firstPixelFormat ??= c.PixelFormat;
            if (string.Equals(c.PixelFormat, UsbPixelFormat, StringComparison.Ordinal))
            {
                pfCurrentMatches = true;
            }
        }

        if (firstPixelFormat is not null && !pfCurrentMatches)
        {
            EnsureUsbFormat();
            Camera.Connection.Usb!.Format!.PixelFormat = firstPixelFormat;
            OnPropertyChanged(nameof(UsbPixelFormat));
        }
    }

    private IEnumerable<(int Width, int Height, double FrameRate, string PixelFormat)> EnumerateSelectedDeviceCapabilities()
    {
        if (SelectedUsbCamera?.SupportedFormats is { } supported)
        {
            foreach (var f in supported)
            {
                // Translate the WinRT MediaEncodingSubtypes string
                // ("NV12", "YUY2", "MJPG", …) to the FFmpeg pixel-format
                // spelling the dshow demuxer expects ("nv12", "yuyv422",
                // "mjpeg"). Without this the picker would store "NV12"
                // and FFmpeg would reject pixel_format=NV12 with
                // "Unable to parse option value as pixel format".
                var pf = AtcSoft.VideoSurveillance.Helpers.MediaSubtypeMapper.MapToFFmpeg(f.Subtype);
                yield return ((int)f.Width, (int)f.Height, f.FrameRate, pf);
            }

            yield break;
        }

        if (SelectedUsbDevice is { } device)
        {
            foreach (var c in device.Capabilities)
            {
                yield return (c.Width, c.Height, c.FrameRate, c.PixelFormat ?? string.Empty);
            }
        }
    }

    private static string FormatResolution(
        int width,
        int height)
        => string.Create(CultureInfo.InvariantCulture, $"{width} × {height}");

    private static (int W, int H) ParseResolution(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return (0, 0);
        }

        var parts = label.Split('×', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return (0, 0);
        }

        if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
        {
            return (w, h);
        }

        return (0, 0);
    }

    [RelayCommand]
    private void RefreshUsbDevices()
    {
        var devices = usbEnumerator.EnumerateDevices();

        UsbDevices.Clear();
        foreach (var d in devices)
        {
            UsbDevices.Add(d);
        }

        // Re-broadcast SelectedUsbDevice — the dropdown stays correct
        // when a refresh re-finds the previously-selected device, and
        // bind-clears when it doesn't.
        OnPropertyChanged(nameof(SelectedUsbDevice));
    }

    private void EnsureUsbConnectionSettings()
    {
        Camera.Connection.Usb ??= new UsbConnectionSettings();
    }

    private void EnsureUsbFormat()
    {
        EnsureUsbConnectionSettings();
        Camera.Connection.Usb!.Format ??= new AtcSoft.VideoSurveillance.Models.UsbStreamFormat();
    }
}