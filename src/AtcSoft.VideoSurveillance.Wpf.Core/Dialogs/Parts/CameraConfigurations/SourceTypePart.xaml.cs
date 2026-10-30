namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs.Parts.CameraConfigurations;

/// <summary>
/// Source-type radio (Network / USB) shown at the top of the camera
/// configuration dialog. Bound to <c>IsNetworkSource</c> /
/// <c>IsUsbSource</c> on <see cref="CameraConfigurationDialogViewModel"/>.
/// </summary>
public partial class SourceTypePart
{
    public SourceTypePart()
    {
        InitializeComponent();
    }
}