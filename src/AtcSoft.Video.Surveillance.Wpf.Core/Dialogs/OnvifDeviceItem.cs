namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// An ONVIF device found on the network, as the dialog lists it.
/// </summary>
/// <param name="Device">The device.</param>
public sealed record OnvifDeviceItem(AtcSoft.Video.Surveillance.Services.Onvif.OnvifDevice Device)
{
    /// <summary>
    /// Gets what the list shows: its name and model, and its address.
    /// </summary>
    public string Text
        => string.Join(
            " · ",
            new[] { Device.Name, Device.Hardware, Device.Address }.Where(s => !string.IsNullOrWhiteSpace(s)));
}