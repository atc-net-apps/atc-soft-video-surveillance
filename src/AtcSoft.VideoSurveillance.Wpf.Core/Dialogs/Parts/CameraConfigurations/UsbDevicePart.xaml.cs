namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs.Parts.CameraConfigurations;

/// <summary>
/// USB device picker shown when <c>IsUsbSource</c> is true. Bound to
/// the USB-related properties on
/// <see cref="CameraConfigurationDialogViewModel"/> — device dropdown,
/// resolution / frame-rate / pixel-format inputs, audio toggle, and
/// the Refresh command.
/// </summary>
public partial class UsbDevicePart
{
    public UsbDevicePart()
    {
        InitializeComponent();
    }
}