namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// Test seam over <see cref="SurveillanceEventService"/>'s USB-lifecycle
/// events. Lets <see cref="RemoteUsbCameraWatcher"/> be unit tested
/// without a live event stream.
/// </summary>
public interface IUsbLifecycleChannel
{
    event Action<UsbCameraLifecycleEvent>? UsbCameraLifecycleChanged;
}