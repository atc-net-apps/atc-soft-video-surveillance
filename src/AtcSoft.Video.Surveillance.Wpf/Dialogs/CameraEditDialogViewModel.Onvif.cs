namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

/// <summary>
/// A camera over ONVIF, through the server: the cameras on the server's networks, one picked for
/// its address, and the streams of the camera at the address typed, which fill in the main stream
/// and the sub-stream. The camera's password is the one typed here: the client never has a stored one.
/// </summary>
public partial class CameraEditDialogViewModel
{
    [ObservableProperty(AfterChangedCallback = nameof(OnOnvifBusyChanged))]
    private bool isOnvifBusy;

    [ObservableProperty]
    private string? onvifStatus;

    [ObservableProperty]
    private int onvifPort;

    /// <summary>
    /// Gets or sets the server's ONVIF operations; without them the ONVIF part is not shown.
    /// </summary>
    public IOnvifGateway? Onvif { get; set; }

    /// <summary>
    /// Gets a value indicating whether the ONVIF part is shown.
    /// </summary>
    public bool CanUseOnvif => Onvif is not null;

    /// <summary>
    /// Gets the devices the last search found.
    /// </summary>
    public ObservableCollection<OnvifDiscoveredDevice> OnvifDevices { get; } = [];

    private static void OnOnvifBusyChanged()
        => CommandManager.InvalidateRequerySuggested();

    private bool CanFindOnvif()
        => Onvif is not null && !IsOnvifBusy;

    [RelayCommand("FindOnvifCameras", CanExecute = nameof(CanFindOnvif))]
    private async Task FindOnvifCamerasAsync()
    {
        IsOnvifBusy = true;
        OnvifStatus = Translations.OnvifSearching;
        OnvifDevices.Clear();
        try
        {
            var found = await Onvif!.DiscoverOnvifCamerasAsync().ConfigureAwait(true);
            foreach (var device in found.Devices)
            {
                OnvifDevices.Add(device);
            }

            OnvifStatus = !found.CouldProbe
                ? Translations.OnvifNoMulticast
                : found.Devices.Count == 0
                    ? Translations.OnvifNoneFound
                    : string.Format(CultureInfo.CurrentCulture, Translations.OnvifFound1, found.Devices.Count);
        }
        catch (HttpRequestException ex)
        {
            OnvifStatus = ex.Message;
        }
        finally
        {
            IsOnvifBusy = false;
        }
    }

    [RelayCommand]
    private void UseOnvifDevice(OnvifDiscoveredDevice? device)
    {
        if (device is null)
        {
            return;
        }

        IpAddress = device.Address;
        OnvifPort = device.Port;
        if (!string.IsNullOrWhiteSpace(device.Name) && string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayName = device.Name;
        }
    }

    private bool CanReadFromCamera()
        => Onvif is not null && !IsOnvifBusy && !string.IsNullOrWhiteSpace(IpAddress);

    [RelayCommand("ReadFromCamera", CanExecute = nameof(CanReadFromCamera))]
    private async Task ReadFromCameraAsync()
    {
        IsOnvifBusy = true;
        OnvifStatus = Translations.OnvifReading;
        try
        {
            var read = await Onvif!
                .ReadOnvifProfilesAsync(new OnvifProfilesRequest(Address: IpAddress.Trim())
                {
                    Port = OnvifPort > 0 ? OnvifPort : null,
                    UserName = string.IsNullOrEmpty(Username) ? null : Username,
                    Password = string.IsNullOrEmpty(Password) ? null : Password,
                })
                .ConfigureAwait(true);

            if (read.Outcome.Value == OnvifProfilesResultOutcome.Names.Read && read.Path is { } mainPath)
            {
                SelectedProtocol = CameraProtocol.Rtsp;
                Port = read.Port ?? 554;
                Path = mainPath;
                SubStreamPath = read.SubStreamPath ?? string.Empty;
                SubStreamPort = read.SubStreamPort ?? 0;
            }

            OnvifStatus = read.Outcome.Value switch
            {
                OnvifProfilesResultOutcome.Names.Read => read.SubStreamPath is null ? Translations.OnvifReadMainOnly : Translations.OnvifReadWithSubStream,
                OnvifProfilesResultOutcome.Names.Unauthorized => Translations.OnvifUnauthorized,
                OnvifProfilesResultOutcome.Names.Timeout => Translations.OnvifTimeout,
                OnvifProfilesResultOutcome.Names.NoRtspProfile => Translations.OnvifNoRtspProfile,
                _ => Translations.OnvifNotOnvif,
            };
        }
        catch (HttpRequestException ex)
        {
            OnvifStatus = ex.Message;
        }
        finally
        {
            IsOnvifBusy = false;
        }
    }
}