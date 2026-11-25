namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// The dialog's tabs: Camera, Find camera, Stream, Overrides, Motion zones. A camera picked with a
/// button on the Find tab (an ONVIF search, a camera hub) brings the Camera tab back, where its
/// address now is; one picked in the network scanner's list fills in the address and says so. A
/// tab that goes (a USB camera has no Find or Stream tab) is never left selected.
/// </summary>
public partial class CameraConfigurationDialogViewModel
{
    /// <summary>
    /// The place of the Camera tab.
    /// </summary>
    public const int CameraTabIndex = 0;

    /// <summary>
    /// The place of the Find camera tab.
    /// </summary>
    public const int FindTabIndex = 1;

    /// <summary>
    /// The place of the Stream tab.
    /// </summary>
    public const int StreamTabIndex = 2;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private string? scannerPickStatus;

    /// <summary>
    /// Gets a value indicating whether the Find camera tab has anything to find with: the network
    /// scanner and the camera hub for a new network camera, an ONVIF search for any network camera.
    /// </summary>
    public bool ShowFindTab
        => IsNetworkSource && (isNew || CanUseCameraHub || (CanUseOnvif && CanFindOnvifCameras));

    /// <summary>
    /// Gets a value indicating whether the Stream tab is shown: its settings are a network stream's.
    /// </summary>
    public bool ShowStreamTab
        => IsNetworkSource;

    // The Find tab may come or go (the source changed, ONVIF or the hubs came): a hidden tab is
    // not left selected.
    private void OnTabsChanged()
    {
        OnPropertyChanged(nameof(ShowFindTab));
        OnPropertyChanged(nameof(ShowStreamTab));
        if ((SelectedTabIndex == FindTabIndex && !ShowFindTab) ||
            (SelectedTabIndex == StreamTabIndex && !ShowStreamTab))
        {
            SelectedTabIndex = CameraTabIndex;
        }
    }

    // A camera picked on the Find tab: its address is on the Camera tab.
    private void ShowCameraTab()
        => SelectedTabIndex = CameraTabIndex;
}