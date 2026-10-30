namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Describes the physical link to a camera, for display purposes. Distinct
/// from <see cref="CameraSource"/>, which selects how the stream is opened.
/// Use <c>CameraConnectionTypeHelper.GetEffective</c> to read
/// the value that should be shown, since USB cameras are always
/// <see cref="Usb"/> regardless of the stored value.
/// </summary>
public enum CameraConnectionType
{
    /// <summary>
    /// Unknown / not set (default). No connection-type icon is shown.
    /// </summary>
    None = 0,

    /// <summary>
    /// Wired Ethernet / PoE network camera.
    /// </summary>
    Lan = 1,

    /// <summary>
    /// Wireless (IEEE 802.11) network camera.
    /// </summary>
    Wifi = 2,

    /// <summary>
    /// Local USB / UVC camera.
    /// </summary>
    Usb = 3,
}