namespace AtcSoft.VideoSurveillance.Wpf.Services;

public class RemoteUsbCameraWatcherTests
{
    [Fact]
    public void Start_SubscribesToHubChannel()
    {
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);

        watcher.Start();

        channel.SubscriberCount.Should().Be(1);
    }

    [Fact]
    public void Start_IsIdempotent()
    {
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);

        watcher.Start();
        watcher.Start();
        watcher.Start();

        channel.SubscriberCount.Should().Be(1);
    }

    [Fact]
    public void Stop_BeforeStart_IsNoOp()
    {
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);

        watcher.Stop();

        channel.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void Stop_AfterStart_Unsubscribes()
    {
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);
        watcher.Start();

        watcher.Stop();

        channel.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void RepluggedEvent_RaisesDeviceArrived_WithDescriptor()
    {
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);
        watcher.Start();

        var captured = new List<UsbCameraEventArgs>();
        watcher.DeviceArrived += (_, e) => captured.Add(e);

        channel.Raise(new UsbCameraLifecycleEvent(
            CameraId: Guid.NewGuid(),
            Phase: new UsbCameraLifecyclePhase("Replugged"),
            DeviceId: @"\\?\usb#vid_046d&pid_085e",
            FriendlyName: "Logitech BRIO",
            Timestamp: DateTimeOffset.UtcNow));

        captured.Should().HaveCount(1);
        captured[0].Device.DeviceId.Should().Be(@"\\?\usb#vid_046d&pid_085e");
        captured[0].Device.FriendlyName.Should().Be("Logitech BRIO");
        captured[0].Device.IsPresent.Should().BeTrue();
    }

    [Fact]
    public void UnpluggedEvent_RaisesDeviceRemoved_WithDescriptor()
    {
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);
        watcher.Start();

        var captured = new List<UsbCameraEventArgs>();
        watcher.DeviceRemoved += (_, e) => captured.Add(e);

        channel.Raise(new UsbCameraLifecycleEvent(
            CameraId: Guid.NewGuid(),
            Phase: new UsbCameraLifecyclePhase("Unplugged"),
            DeviceId: "abc",
            FriendlyName: "Cam",
            Timestamp: DateTimeOffset.UtcNow));

        captured.Should().HaveCount(1);
        captured[0].Device.IsPresent.Should().BeFalse();
    }

    [Fact]
    public void PhaseMatching_IsCaseInsensitive()
    {
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);
        watcher.Start();

        var arrived = 0;
        var removed = 0;
        watcher.DeviceArrived += (_, _) => arrived++;
        watcher.DeviceRemoved += (_, _) => removed++;

        channel.Raise(SamplePayload("REPLUGGED"));
        channel.Raise(SamplePayload("unplugged"));

        arrived.Should().Be(1);
        removed.Should().Be(1);
    }

    [Fact]
    public void UnknownPhase_IsIgnored()
    {
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);
        watcher.Start();

        var raises = 0;
        watcher.DeviceArrived += (_, _) => raises++;
        watcher.DeviceRemoved += (_, _) => raises++;

        channel.Raise(SamplePayload("SomeFutureState"));

        raises.Should().Be(0);
    }

    [Fact]
    public void EventsBeforeStart_AreNotForwarded()
    {
        // Watcher.Start hasn't been called yet — the channel is firing
        // but no subscription exists. This guards against subtle leaks
        // where consumers receive events for a watcher they haven't yet
        // turned on.
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);

        var raises = 0;
        watcher.DeviceArrived += (_, _) => raises++;
        watcher.DeviceRemoved += (_, _) => raises++;

        channel.Raise(SamplePayload("Unplugged"));

        raises.Should().Be(0);
    }

    [Fact]
    public void Dispose_StopsForwarding()
    {
        var channel = new FakeChannel();
        var watcher = new RemoteUsbCameraWatcher(channel);
        watcher.Start();

        var raises = 0;
        watcher.DeviceRemoved += (_, _) => raises++;

        watcher.Dispose();
        channel.Raise(SamplePayload("Unplugged"));

        raises.Should().Be(0);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var channel = new FakeChannel();
        var watcher = new RemoteUsbCameraWatcher(channel);
        watcher.Start();

        watcher.Dispose();
        watcher.Dispose();

        var act = () => watcher.Start();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EmptyDeviceId_GeneratesSyntheticId_RatherThanThrow()
    {
        // UsbDeviceDescriptor's constructor enforces a non-empty
        // device-id; the watcher synthesizes one when the SignalR
        // payload arrives without it (e.g. malformed broadcast). This
        // keeps a degenerate event from crashing the entire WPF
        // dispatch loop.
        var channel = new FakeChannel();
        using var watcher = new RemoteUsbCameraWatcher(channel);
        watcher.Start();

        var captured = new List<UsbCameraEventArgs>();
        watcher.DeviceArrived += (_, e) => captured.Add(e);

        channel.Raise(new UsbCameraLifecycleEvent(
            CameraId: Guid.NewGuid(),
            Phase: new UsbCameraLifecyclePhase("Replugged"),
            DeviceId: string.Empty,
            FriendlyName: "Cam",
            Timestamp: DateTimeOffset.UtcNow));

        captured.Should().HaveCount(1);
        captured[0].Device.DeviceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_NullChannel_Throws()
    {
        var act = () => new RemoteUsbCameraWatcher(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static UsbCameraLifecycleEvent SamplePayload(string phase)
        => new(
            CameraId: Guid.NewGuid(),
            Phase: new UsbCameraLifecyclePhase(phase),
            DeviceId: "device",
            FriendlyName: "Cam",
            Timestamp: DateTimeOffset.UtcNow);

    private sealed class FakeChannel : IUsbLifecycleChannel
    {
        public event Action<UsbCameraLifecycleEvent>? UsbCameraLifecycleChanged;

        public int SubscriberCount
            => UsbCameraLifecycleChanged?.GetInvocationList().Length ?? 0;

        public void Raise(UsbCameraLifecycleEvent e)
            => UsbCameraLifecycleChanged?.Invoke(e);
    }
}