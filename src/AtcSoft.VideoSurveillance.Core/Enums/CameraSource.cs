namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Identifies the *kind* of source feeding a camera. Distinct from
/// <see cref="CameraProtocol"/>, which only covers wire protocols for
/// network cameras. <see cref="CameraSource"/> selects between mutually
/// exclusive configuration shapes (network endpoint vs. local USB device).
/// </summary>
public enum CameraSource
{
    /// <summary>
    /// IP / network camera — uses
    /// <see cref="Models.Settings.ConnectionSettings.IpAddress"/>,
    /// <see cref="Models.Settings.ConnectionSettings.Port"/> and
    /// <see cref="Models.Settings.ConnectionSettings.Protocol"/>.
    /// </summary>
    Network = 0,

    /// <summary>
    /// USB / DirectShow / UVC webcam — uses
    /// <see cref="Models.Settings.ConnectionSettings.Usb"/> for device
    /// identity and capture-format selection.
    /// </summary>
    Usb = 1,
}