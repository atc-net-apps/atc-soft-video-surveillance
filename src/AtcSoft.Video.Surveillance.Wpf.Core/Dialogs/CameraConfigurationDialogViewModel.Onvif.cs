namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// A network camera over ONVIF: find the cameras on the network and pick one for its address, or
/// read the streams of the camera at the address typed, which fills in the main stream and the
/// sub-stream. All within the dialog.
/// </summary>
public partial class CameraConfigurationDialogViewModel
{
    private AtcSoft.Video.Surveillance.Services.Onvif.IOnvifDiscovery? onvifDiscovery;
    private AtcSoft.Video.Surveillance.Services.Onvif.IOnvifMediaClient? onvifMediaClient;

    [ObservableProperty(AfterChangedCallback = nameof(OnOnvifBusyChanged))]
    private bool isOnvifBusy;

    [ObservableProperty]
    private string? onvifStatus;

    [ObservableProperty]
    private int onvifPort;

    /// <summary>
    /// Gets the devices the last search found.
    /// </summary>
    public ObservableCollection<OnvifDeviceItem> OnvifDevices { get; } = [];

    /// <summary>
    /// Gets a value indicating whether ONVIF can be used: a network camera, in an app that has it.
    /// </summary>
    public bool CanUseOnvif
        => onvifMediaClient is not null && IsNetworkSource;

    /// <summary>
    /// Gets a value indicating whether the network can be searched, besides reading by address.
    /// </summary>
    public bool CanFindOnvifCameras
        => onvifDiscovery is not null;

    /// <summary>
    /// Sets the ONVIF services; without a client the ONVIF part is not shown.
    /// </summary>
    /// <param name="discovery">Finds cameras on the network, or nothing.</param>
    /// <param name="mediaClient">Reads a camera's streams, or nothing.</param>
    public void UseOnvif(
        AtcSoft.Video.Surveillance.Services.Onvif.IOnvifDiscovery? discovery,
        AtcSoft.Video.Surveillance.Services.Onvif.IOnvifMediaClient? mediaClient)
    {
        onvifDiscovery = discovery;
        onvifMediaClient = mediaClient;
        OnPropertyChanged(nameof(CanUseOnvif));
        OnPropertyChanged(nameof(CanFindOnvifCameras));
    }

    private static void OnOnvifBusyChanged()
        => CommandManager.InvalidateRequerySuggested();

    private bool CanFindOnvif()
        => onvifDiscovery is not null && !IsOnvifBusy;

    [RelayCommand("FindOnvifCameras", CanExecute = nameof(CanFindOnvif))]
    private async Task FindOnvifCamerasAsync()
    {
        IsOnvifBusy = true;
        OnvifStatus = Translations.OnvifSearching;
        OnvifDevices.Clear();
        try
        {
            var found = await onvifDiscovery!.DiscoverAsync(cancellationToken: CancellationToken.None).ConfigureAwait(true);
            foreach (var device in found.Devices)
            {
                OnvifDevices.Add(new OnvifDeviceItem(device));
            }

            OnvifStatus = !found.CouldProbe
                ? Translations.OnvifNoMulticast
                : found.Devices.Count == 0
                    ? Translations.OnvifNoneFound
                    : string.Format(CultureInfo.CurrentCulture, Translations.OnvifFound1, found.Devices.Count);
        }
        finally
        {
            IsOnvifBusy = false;
        }
    }

    [RelayCommand]
    private void UseOnvifDevice(OnvifDeviceItem? item)
    {
        if (item is null)
        {
            return;
        }

        Camera.Connection.IpAddress = item.Device.Address;
        OnvifPort = item.Device.Port;
        if (!string.IsNullOrWhiteSpace(item.Device.Name) &&
            (string.IsNullOrWhiteSpace(Camera.Display.DisplayName) || string.Equals(Camera.Display.DisplayName, Translations.NewCamera, StringComparison.Ordinal)))
        {
            Camera.Display.DisplayName = item.Device.Name;
        }
    }

    private bool CanReadFromCamera()
        => onvifMediaClient is not null && !IsOnvifBusy && !string.IsNullOrWhiteSpace(Camera.Connection.IpAddress);

    [RelayCommand("ReadFromCamera", CanExecute = nameof(CanReadFromCamera))]
    private async Task ReadFromCameraAsync()
    {
        IsOnvifBusy = true;
        OnvifStatus = Translations.OnvifReading;
        try
        {
            var read = await onvifMediaClient!
                .ReadProfilesAsync(
                    Camera.Connection.IpAddress,
                    OnvifPort > 0 ? OnvifPort : null,
                    Camera.Authentication.UserName,
                    Camera.Authentication.Password,
                    CancellationToken.None)
                .ConfigureAwait(true);

            if (read.Choice is { } choice)
            {
                // Filled in on a copy, then carried over through the form's own properties, so every field shows it.
                var filled = new AtcSoft.Video.Surveillance.Models.Settings.ConnectionSettings { IpAddress = Camera.Connection.IpAddress };
                choice.ApplyTo(filled);
                Camera.Connection.Protocol = filled.Protocol;
                Camera.Connection.Port = filled.Port;
                Camera.Connection.Path = filled.Path;
                Camera.Connection.SubStreamPath = filled.SubStreamPath;
                SubStreamPortValue = filled.SubStreamPort ?? 0;
                OnPropertyChanged(nameof(SelectedProtocolKey));
            }

            OnvifStatus = StatusOf(read);
        }
        finally
        {
            IsOnvifBusy = false;
        }
    }

    private static string StatusOf(
        AtcSoft.Video.Surveillance.Services.Onvif.OnvifImportResult read)
        => read.Outcome switch
        {
            AtcSoft.Video.Surveillance.Services.Onvif.OnvifImportOutcome.Read => read.Choice?.Sub is null
                ? Translations.OnvifReadMainOnly
                : Translations.OnvifReadWithSubStream,
            AtcSoft.Video.Surveillance.Services.Onvif.OnvifImportOutcome.Unauthorized => Translations.OnvifUnauthorized,
            AtcSoft.Video.Surveillance.Services.Onvif.OnvifImportOutcome.Timeout => Translations.OnvifTimeout,
            AtcSoft.Video.Surveillance.Services.Onvif.OnvifImportOutcome.NoRtspProfile => Translations.OnvifNoRtspProfile,
            _ => Translations.OnvifNotOnvif,
        };
}