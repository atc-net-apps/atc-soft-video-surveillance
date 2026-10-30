namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// GET /devices/usb is kept in the output cache for a few seconds
/// (x-cache-* in the specification). This drops it as soon as a USB camera
/// is plugged in or pulled out, so a client that reads the list right after
/// gets the new one, and the cache only spares the repeated enumerations.
/// <br/><br/>
/// Event-driven like <see cref="SurveillanceEventBroadcaster"/>: subscription
/// in <see cref="StartAsync"/>, tear-down in <see cref="StopAsync"/>, and a
/// no-op <see cref="DoWorkAsync"/> parked on a long delay.
/// </summary>
public sealed partial class UsbDeviceCacheEvictor : BackgroundServiceBase<UsbDeviceCacheEvictor>
{
    /// <summary>
    /// The output cache tag of the USB device list, as the specification
    /// declares it (<c>x-cache-tags</c> of listUsbDevices).
    /// </summary>
    public const string Tag = OutputCacheTags.UsbDevices;

    private readonly IUsbCameraWatcher watcher;
    private readonly IOutputCacheStore store;

    public UsbDeviceCacheEvictor(
        ILogger<UsbDeviceCacheEvictor> logger,
        UsbDeviceCacheEvictorOptions options,
        IUsbCameraWatcher watcher,
        IOutputCacheStore store)
        : base(logger, options)
    {
        this.watcher = watcher;
        this.store = store;
    }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        watcher.DeviceArrived += OnDeviceChanged;
        watcher.DeviceRemoved += OnDeviceChanged;

        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        watcher.DeviceArrived -= OnDeviceChanged;
        watcher.DeviceRemoved -= OnDeviceChanged;

        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
        => Task.CompletedTask; // Event-driven: the work runs in the handler wired up in StartAsync.

    // Fire-and-forget through a Task, so a failure stays in it and is logged
    // instead of escaping onto the watcher's thread.
    private void OnDeviceChanged(
        object? sender,
        UsbCameraEventArgs e)
        => _ = EvictAsync();

    private async Task EvictAsync()
    {
        try
        {
            await store.EvictByTagAsync(Tag, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogEvictFailed(ex);
        }
    }
}