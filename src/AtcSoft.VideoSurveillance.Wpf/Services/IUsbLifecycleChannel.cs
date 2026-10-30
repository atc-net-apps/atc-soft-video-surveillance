namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Test seam over <see cref="SurveillanceEventService"/>'s USB-lifecycle
/// events. Lets <see cref="RemoteUsbCameraWatcher"/> be unit tested
/// without a live event stream.
/// </summary>
public interface IUsbLifecycleChannel
{
    event Action<UsbCameraLifecycleEvent>? UsbCameraLifecycleChanged;
}

/// <summary>
/// Adapter that forwards the SurveillanceEventService event onto the
/// <see cref="IUsbLifecycleChannel"/> contract. The event service
/// itself stays free of new interfaces — adapters keep production DI
/// readable while still letting tests substitute a fake channel.
/// </summary>
public sealed class SurveillanceEventLifecycleChannel : IUsbLifecycleChannel
{
    public SurveillanceEventLifecycleChannel(
        SurveillanceEventService eventService)
    {
        ArgumentNullException.ThrowIfNull(eventService);

        // The event service outlives this adapter (DI singleton); keeping
        // the subscription for the process lifetime is intentional —
        // there's nowhere meaningful to unhook it in a singleton-driven
        // container, and unsubscribing would just race with shutdown.
        eventService.OnUsbCameraLifecycleChanged += Forward;
    }

    public event Action<UsbCameraLifecycleEvent>? UsbCameraLifecycleChanged;

    private void Forward(UsbCameraLifecycleEvent e)
        => UsbCameraLifecycleChanged?.Invoke(e);
}