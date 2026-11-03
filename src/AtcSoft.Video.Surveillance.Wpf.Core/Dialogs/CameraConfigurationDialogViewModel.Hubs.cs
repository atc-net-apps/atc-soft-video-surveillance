namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// A new camera from a camera hub: the hub's address and user, its cameras with a picture each,
/// and one picked to fill in the camera (its stream, its name, the hub as its origin).
/// </summary>
public partial class CameraConfigurationDialogViewModel
{
    private AtcSoft.Video.Surveillance.Services.Hubs.ICameraHubProvider? cameraHubProvider;
    private AtcSoft.Video.Surveillance.Services.Hubs.CameraHubListing? hubListing;

    // Which Connect is the latest: an older one's answer and pictures are dropped.
    private int hubConnection;

    [ObservableProperty(AfterChangedCallback = nameof(OnHubInputChanged))]
    private string hubAddress = string.Empty;

    [ObservableProperty]
    private string hubUserName = "admin";

    [ObservableProperty]
    private string hubPassword = string.Empty;

    [ObservableProperty(AfterChangedCallback = nameof(OnHubInputChanged))]
    private bool isHubBusy;

    [ObservableProperty]
    private string? hubStatus;

    [ObservableProperty]
    private string? hubWarning;

    /// <summary>
    /// Gets a value indicating whether a camera can be added from a hub: a new network camera, in
    /// an app that reaches hubs itself.
    /// </summary>
    public bool CanUseCameraHub
        => isNew && cameraHubProvider is not null && IsNetworkSource;

    /// <summary>
    /// Gets the hub's cameras, once listed.
    /// </summary>
    public ObservableCollection<HubCameraItem> HubCameras { get; } = [];

    /// <summary>
    /// Sets the hub provider; without one the hub part is not shown.
    /// </summary>
    public void UseCameraHubs(
        AtcSoft.Video.Surveillance.Services.Hubs.ICameraHubProvider? provider)
    {
        cameraHubProvider = provider;
        OnPropertyChanged(nameof(CanUseCameraHub));
    }

    private static void OnHubInputChanged()
        => CommandManager.InvalidateRequerySuggested();

    private bool CanConnectHub()
        => cameraHubProvider is not null && !IsHubBusy && !string.IsNullOrWhiteSpace(HubAddress);

    [RelayCommand("ConnectHub", CanExecute = nameof(CanConnectHub))]
    private async Task ConnectHubAsync()
    {
        if (cameraHubProvider is null)
        {
            return;
        }

        var connection = ++hubConnection;

        IsHubBusy = true;
        HubStatus = Translations.Connecting;
        HubWarning = null;
        HubCameras.Clear();
        hubListing = null;
        try
        {
            var listing = await cameraHubProvider.ListAsync(HubAddress, HubUserName, HubPassword, CancellationToken.None).ConfigureAwait(true);
            if (connection != hubConnection)
            {
                return;
            }

            hubListing = listing;
            HubStatus = StatusOf(listing);
            HubWarning = listing.OlderSignIn ? Translations.CameraHubOlderSignIn : null;
            foreach (var camera in listing.Cameras)
            {
                HubCameras.Add(new HubCameraItem(camera, ProblemOf(camera.Problem)));
            }
        }
        finally
        {
            IsHubBusy = false;
        }

        await LoadHubPicturesAsync(connection).ConfigureAwait(true);
    }

    // The hub cameras' pictures, one after another: a Pi with four cameras answers slowly.
    private async Task LoadHubPicturesAsync(int connection)
    {
        if (cameraHubProvider is null || hubListing is not { } listing)
        {
            return;
        }

        foreach (var item in new List<HubCameraItem>(HubCameras))
        {
            try
            {
                var picture = await cameraHubProvider
                    .GetSnapshotAsync(listing.Address, HubUserName, HubPassword, item.Camera.Id, CancellationToken.None)
                    .ConfigureAwait(true);
                if (connection != hubConnection)
                {
                    return;
                }

                if (picture is not null)
                {
                    item.Picture = Helpers.MotionZonePicture.Decode(picture);
                }
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException or InvalidOperationException)
            {
                // No picture; the camera is listed all the same.
            }
        }
    }

    [RelayCommand]
    private void UseHubCamera(HubCameraItem? item)
    {
        if (item is null || hubListing is null || !item.CanUse)
        {
            return;
        }

        // Filled in on a copy, then carried over through the form's own properties, so every field shows it.
        var filled = new AtcSoft.Video.Surveillance.Models.CameraConfiguration();
        filled.Overrides.Recording.TranscodeVideoCodec = Camera.Overrides?.Recording.TranscodeVideoCodec;
        if (!AtcSoft.Video.Surveillance.Services.Hubs.HubCameraSetup.ApplyTo(filled, hubListing, item.Camera))
        {
            return;
        }

        Camera.Connection.Source = filled.Connection.Source;
        Camera.Connection.Protocol = filled.Connection.Protocol;
        Camera.Connection.IpAddress = filled.Connection.IpAddress;
        Camera.Connection.Port = filled.Connection.Port;
        Camera.Connection.Path = filled.Connection.Path;
        Camera.Connection.Hub = filled.Connection.Hub;
        if (filled.Authentication.UserName is not null)
        {
            Camera.Authentication.UserName = filled.Authentication.UserName;
            Camera.Authentication.Password = filled.Authentication.Password;
        }

        Camera.Display.DisplayName = filled.Display.DisplayName;
        if (filled.Overrides.Recording.TranscodeVideoCodec is { } codec && Camera.Overrides is not null)
        {
            Camera.Overrides.Recording.TranscodeVideoCodec = codec;
        }

        OnPropertyChanged(nameof(SelectedProtocolKey));
        OnPropertyChanged(nameof(UseDefaultTranscodeVideoCodec));
        OnPropertyChanged(nameof(OverrideTranscodeVideoCodec));
        HubStatus = string.Format(CultureInfo.CurrentCulture, Translations.HubCameraUsed1, item.Name);
    }

    private static string StatusOf(
        AtcSoft.Video.Surveillance.Services.Hubs.CameraHubListing listing)
        => listing.Outcome switch
        {
            AtcSoft.Video.Surveillance.Services.Hubs.CameraHubOutcome.Listed => string.Format(CultureInfo.CurrentCulture, Translations.CameraHubListed1, listing.Cameras.Count),
            AtcSoft.Video.Surveillance.Services.Hubs.CameraHubOutcome.SignInRefused => Translations.CameraHubSignInRefused,
            AtcSoft.Video.Surveillance.Services.Hubs.CameraHubOutcome.NotThisKind => Translations.CameraHubNotMotionEye,
            _ => Translations.CameraHubUnreachable,
        };

    private static string? ProblemOf(
        AtcSoft.Video.Surveillance.Services.Hubs.HubCameraProblem problem)
        => problem switch
        {
            AtcSoft.Video.Surveillance.Services.Hubs.HubCameraProblem.Disabled => Translations.HubCameraDisabled,
            AtcSoft.Video.Surveillance.Services.Hubs.HubCameraProblem.StreamingOff => Translations.HubCameraStreamingOff,
            AtcSoft.Video.Surveillance.Services.Hubs.HubCameraProblem.OnAnotherHub => Translations.HubCameraOnAnotherHub,
            _ => null,
        };
}