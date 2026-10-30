namespace AtcSoft.VideoSurveillance.Api.Services;

using UsbCameraEventArgs = AtcSoft.VideoSurveillance.Events.UsbCameraEventArgs;
using UsbDeviceDescriptor = AtcSoft.VideoSurveillance.Models.UsbDeviceDescriptor;

/// <summary>
/// A camera plugged in or pulled out drops the cached USB device list, so the
/// next read enumerates again instead of answering with the old list.
/// </summary>
public sealed class UsbDeviceCacheEvictorTests
{
    private readonly IUsbCameraWatcher watcher = Substitute.For<IUsbCameraWatcher>();
    private readonly IOutputCacheStore store = Substitute.For<IOutputCacheStore>();

    [Fact]
    public async Task A_Camera_Plugged_In_Drops_The_Cached_List()
    {
        using var sut = await StartAsync();

        watcher.DeviceArrived += Raise.EventWith(Device());

        await store.Received(1).EvictByTagAsync(UsbDeviceCacheEvictor.Tag, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Camera_Pulled_Out_Drops_The_Cached_List()
    {
        using var sut = await StartAsync();

        watcher.DeviceRemoved += Raise.EventWith(Device());

        await store.Received(1).EvictByTagAsync(UsbDeviceCacheEvictor.Tag, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task After_It_Stops_It_Leaves_The_Cache_Alone()
    {
        using var sut = await StartAsync();
        await sut.StopAsync(TestContext.Current.CancellationToken);

        watcher.DeviceArrived += Raise.EventWith(Device());

        await store.DidNotReceive().EvictByTagAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void The_Tag_Is_The_One_The_Specification_Gives_The_Usb_Devices()
        => UsbDeviceCacheEvictor.Tag.Should().Be(OutputCacheTags.UsbDevices);

    private static UsbCameraEventArgs Device()
        => new(new UsbDeviceDescriptor("usb#vid_046d", "Logitech C920", isPresent: true));

    private async Task<UsbDeviceCacheEvictor> StartAsync()
    {
        var sut = new UsbDeviceCacheEvictor(
            NullLogger<UsbDeviceCacheEvictor>.Instance,
            new UsbDeviceCacheEvictorOptions(),
            watcher,
            store);
        await sut.StartAsync(TestContext.Current.CancellationToken);
        return sut;
    }
}