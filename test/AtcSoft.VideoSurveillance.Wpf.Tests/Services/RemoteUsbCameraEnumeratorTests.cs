namespace AtcSoft.VideoSurveillance.Wpf.Services;

public class RemoteUsbCameraEnumeratorTests
{
    [Fact]
    public void EnumerateDevices_FirstCall_FetchesFromGateway()
    {
        var gateway = Substitute.For<IUsbCameraGateway>();
        gateway.ListUsbDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UsbDeviceDescriptor>?>(new[]
            {
                new UsbDeviceDescriptor("id-1", "Cam 1"),
            }));
        var enumerator = Build(gateway);

        var devices = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);

        devices.Should().HaveCount(1);
        devices[0].DeviceId.Should().Be("id-1");
        _ = gateway.Received(1).ListUsbDevicesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EnumerateDevices_RepeatedCallsWithinTtl_DoNotHitGateway()
    {
        var gateway = Substitute.For<IUsbCameraGateway>();
        gateway.ListUsbDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UsbDeviceDescriptor>?>(new[]
            {
                new UsbDeviceDescriptor("id-1", "Cam 1"),
            }));
        var enumerator = Build(gateway);

        _ = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);
        _ = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);
        _ = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);

        _ = gateway.Received(1).ListUsbDevicesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EnumerateDevices_AfterTtlElapsed_RefreshesFromGateway()
    {
        var gateway = Substitute.For<IUsbCameraGateway>();
        gateway.ListUsbDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UsbDeviceDescriptor>?>(new[]
            {
                new UsbDeviceDescriptor("id-1", "Cam 1"),
            }));
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var enumerator = new RemoteUsbCameraEnumerator(gateway, time, TimeSpan.FromSeconds(5));

        _ = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(6));
        _ = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);

        _ = gateway.Received(2).ListUsbDevicesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EnumerateDevices_GatewayReturnsNull_KeepsLastCachedList()
    {
        // 503 / transport failure → gateway returns null. We must not
        // wipe the previous response, so the dialog doesn't blink to
        // empty when the server briefly hiccups.
        var gateway = Substitute.For<IUsbCameraGateway>();
        var seed = new List<UsbDeviceDescriptor>
        {
            new("id-1", "Cam 1"),
        };
        gateway.ListUsbDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<UsbDeviceDescriptor>?>(seed),
                Task.FromResult<IReadOnlyList<UsbDeviceDescriptor>?>(null));
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var enumerator = new RemoteUsbCameraEnumerator(gateway, time, TimeSpan.FromSeconds(1));

        _ = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(2));
        var second = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);

        second.Should().HaveCount(1);
        second[0].DeviceId.Should().Be("id-1");
    }

    [Fact]
    public void FindByDeviceId_DelegatesToCachedEnumeration_AndIsCaseInsensitive()
    {
        var gateway = Substitute.For<IUsbCameraGateway>();
        gateway.ListUsbDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UsbDeviceDescriptor>?>(new[]
            {
                new UsbDeviceDescriptor(@"\\?\USB#VID_046D", "Logitech BRIO"),
            }));
        var enumerator = Build(gateway);

        enumerator.FindByDeviceId(@"\\?\usb#vid_046d").Should().NotBeNull();
    }

    [Fact]
    public void FindByDeviceId_NullOrEmpty_ReturnsNull()
    {
        var gateway = Substitute.For<IUsbCameraGateway>();
        var enumerator = Build(gateway);

        enumerator.FindByDeviceId(string.Empty).Should().BeNull();
        enumerator.FindByDeviceId(null!).Should().BeNull();
    }

    [Fact]
    public void FindByFriendlyName_MatchesIgnoreCase()
    {
        var gateway = Substitute.For<IUsbCameraGateway>();
        gateway.ListUsbDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UsbDeviceDescriptor>?>(new[]
            {
                new UsbDeviceDescriptor("id", "Logitech BRIO"),
            }));
        var enumerator = Build(gateway);

        enumerator.FindByFriendlyName("logitech brio").Should().NotBeNull();
        enumerator.FindByFriendlyName("Other Cam").Should().BeNull();
    }

    [Fact]
    public void Constructor_NullGateway_Throws()
    {
        var act = () => new RemoteUsbCameraEnumerator(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static RemoteUsbCameraEnumerator Build(IUsbCameraGateway gateway)
        => new(gateway, new FakeTimeProvider(DateTimeOffset.UtcNow), TimeSpan.FromSeconds(5));
}