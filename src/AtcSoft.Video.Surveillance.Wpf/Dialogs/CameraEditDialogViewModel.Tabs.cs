namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

/// <summary>
/// The dialog's tabs: Camera, Find camera (an ONVIF search through the server), Stream, Motion
/// zones. A camera picked on the Find camera tab brings the Camera tab back, where its address now is.
/// </summary>
public partial class CameraEditDialogViewModel
{
    /// <summary>
    /// The place of the Camera tab.
    /// </summary>
    public const int CameraTabIndex = 0;

    /// <summary>
    /// The place of the Find camera tab.
    /// </summary>
    public const int FindTabIndex = 1;

    [ObservableProperty]
    private int selectedTabIndex;
}