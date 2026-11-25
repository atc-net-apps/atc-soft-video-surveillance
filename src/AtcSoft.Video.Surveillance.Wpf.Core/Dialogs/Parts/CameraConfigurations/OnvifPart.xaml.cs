namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs.Parts.CameraConfigurations;

/// <summary>
/// The Find camera tab's ONVIF part: find the cameras on the network; a camera picked fills in
/// the address on the Camera tab, where <see cref="OnvifReadPart"/> reads its streams.
/// </summary>
public partial class OnvifPart
{
    public OnvifPart()
        => InitializeComponent();
}