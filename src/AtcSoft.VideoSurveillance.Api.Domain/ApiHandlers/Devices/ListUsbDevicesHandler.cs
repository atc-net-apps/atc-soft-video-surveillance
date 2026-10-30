namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Devices;

using CoreUsbDeviceDescriptor = AtcSoft.VideoSurveillance.Models.UsbDeviceDescriptor;

/// <summary>
/// Handler business logic for the ListUsbDevices operation. Returns
/// the USB cameras visible to the server host. On hosts without an
/// enumerator implementation (e.g. Linux before V4L2 ships) responds
/// 503 instead of an empty list so clients can distinguish "none
/// attached" from "platform not supported".
/// </summary>
public sealed class ListUsbDevicesHandler(
    IUsbCameraEnumerator enumerator) : IListUsbDevicesHandler
{
    public Task<ListUsbDevicesResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // The Null enumerator is the cross-platform default — surface
        // a 503 when it's the bound implementation so non-Windows hosts
        // don't pretend to support USB enumeration.
        if (enumerator is NullUsbCameraEnumerator)
        {
            return Task.FromResult(ListUsbDevicesResult.ServiceUnavailable(
                "USB camera enumeration is not supported on this server host."));
        }

        var devices = enumerator.EnumerateDevices(cancellationToken)
            .Select(ToApiDescriptor)
            .ToList();

        return Task.FromResult(ListUsbDevicesResult.Ok(devices));
    }

    private static UsbDeviceDescriptor ToApiDescriptor(
        CoreUsbDeviceDescriptor core)
    {
        var apiCaps = new List<UsbStreamFormat>(core.Capabilities.Count);
        foreach (var c in core.Capabilities)
        {
            apiCaps.Add(new UsbStreamFormat(
                Width: c.Width,
                Height: c.Height,
                FrameRate: c.FrameRate,
                PixelFormat: c.PixelFormat));
        }

        return new UsbDeviceDescriptor(
            DeviceId: core.DeviceId,
            FriendlyName: core.FriendlyName,
            VendorId: core.VendorId ?? string.Empty,
            ProductId: core.ProductId ?? string.Empty,
            IsPresent: core.IsPresent,
            Capabilities: apiCaps);
    }
}