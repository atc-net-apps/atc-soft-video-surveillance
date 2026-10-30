using CoreCameraConfiguration = AtcSoft.VideoSurveillance.Models.CameraConfiguration;
using CoreCameraSource = AtcSoft.VideoSurveillance.Enums.CameraSource;

namespace AtcSoft.VideoSurveillance.Api.Domain.Mapping;

/// <summary>
/// What a camera needs before it can be stored: a network camera an address, a USB camera a
/// device (its id, or its name, which the capture opens it by). The spec cannot say it, because
/// it depends on the source. Without it a camera was stored and failed only when opened.
/// </summary>
public static class CameraSourceCheck
{
    /// <summary>
    /// Gets why the camera cannot be stored, or <see langword="null"/> when it can.
    /// </summary>
    public static string? Problem(CoreCameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var connection = camera.Connection;
        if (connection.Source == CoreCameraSource.Usb)
        {
            return string.IsNullOrWhiteSpace(connection.Usb?.DeviceId) && string.IsNullOrWhiteSpace(connection.Usb?.FriendlyName)
                ? "A USB camera needs a device: usbDeviceId or usbFriendlyName."
                : null;
        }

        return string.IsNullOrWhiteSpace(connection.IpAddress)
            ? "A network camera needs an address: ipAddress."
            : null;
    }
}