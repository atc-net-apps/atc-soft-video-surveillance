namespace AtcSoft.VideoSurveillance.Services;

public class UsbCameraLifecycleCoordinatorTests
{
    [Fact]
    public void IsUnplugged_DefaultsToFalse_ForUnknownCameraId()
    {
        var (coordinator, _, _) = Build();

        coordinator.IsUnplugged(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Start_SubscribesToWatcher_AndCallsStart()
    {
        var (coordinator, watcher, _) = Build();

        coordinator.Start();

        watcher.Received(1).Start();
    }

    [Fact]
    public void Start_IsIdempotent()
    {
        var (coordinator, watcher, _) = Build();

        coordinator.Start();
        coordinator.Start();
        coordinator.Start();

        watcher.Received(1).Start();
    }

    [Fact]
    public void Stop_BeforeStart_IsNoOp()
    {
        var (coordinator, watcher, _) = Build();

        coordinator.Stop();

        watcher.DidNotReceive().Stop();
    }

    [Fact]
    public void Stop_AfterStart_UnsubscribesFromWatcher()
    {
        var (coordinator, watcher, _) = Build();
        coordinator.Start();

        coordinator.Stop();

        watcher.Received(1).Stop();

        // After Stop, raising events on the watcher must not affect the coordinator's set.
        watcher.DeviceRemoved += Raise.EventWith(watcher, new UsbCameraEventArgs(SampleDescriptor("any")));
        coordinator.IsUnplugged(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void DeviceRemoved_KnownUsbCamera_MarksAsUnplugged_AndRaisesStateChanged()
    {
        var cameraId = Guid.NewGuid();
        var (coordinator, watcher, storage) = Build();
        storage.GetAllCameras().Returns(new List<CameraConfiguration>
        {
            UsbCamera(cameraId, deviceId: "abc"),
        });

        var captured = new List<UsbCameraLifecycleChangedEventArgs>();
        coordinator.StateChanged += (_, e) => captured.Add(e);
        coordinator.Start();

        watcher.DeviceRemoved += Raise.EventWith(
            watcher,
            new UsbCameraEventArgs(SampleDescriptor("abc")));

        coordinator.IsUnplugged(cameraId).Should().BeTrue();
        captured.Should().HaveCount(1);
        captured[0].CameraId.Should().Be(cameraId);
        captured[0].Phase.Should().Be(UsbCameraLifecyclePhase.Unplugged);
    }

    [Fact]
    public void DeviceRemoved_DuplicateEvent_DoesNotRaiseTwice()
    {
        var cameraId = Guid.NewGuid();
        var (coordinator, watcher, storage) = Build();
        storage.GetAllCameras().Returns(new List<CameraConfiguration>
        {
            UsbCamera(cameraId, deviceId: "abc"),
        });

        var raises = 0;
        coordinator.StateChanged += (_, _) => raises++;
        coordinator.Start();

        var args = new UsbCameraEventArgs(SampleDescriptor("abc"));
        watcher.DeviceRemoved += Raise.EventWith(watcher, args);
        watcher.DeviceRemoved += Raise.EventWith(watcher, args);

        raises.Should().Be(1);
    }

    [Fact]
    public void DeviceRemoved_UnknownDeviceId_NoOp()
    {
        var (coordinator, watcher, storage) = Build();
        storage.GetAllCameras().Returns(new List<CameraConfiguration>());

        var raises = 0;
        coordinator.StateChanged += (_, _) => raises++;
        coordinator.Start();

        watcher.DeviceRemoved += Raise.EventWith(
            watcher,
            new UsbCameraEventArgs(SampleDescriptor("nope")));

        raises.Should().Be(0);
    }

    [Fact]
    public void DeviceRemoved_DeviceIdMatchCaseInsensitively()
    {
        var cameraId = Guid.NewGuid();
        var (coordinator, watcher, storage) = Build();
        storage.GetAllCameras().Returns(new List<CameraConfiguration>
        {
            UsbCamera(cameraId, deviceId: @"\\?\USB#VID_046D"),
        });

        coordinator.Start();
        watcher.DeviceRemoved += Raise.EventWith(
            watcher,
            new UsbCameraEventArgs(SampleDescriptor(@"\\?\usb#vid_046d")));

        coordinator.IsUnplugged(cameraId).Should().BeTrue();
    }

    [Fact]
    public void DeviceRemoved_NetworkCamera_Ignored()
    {
        // A network camera with no UsbConnectionSettings must never
        // match a USB device-arrival event even if device IDs collide
        // accidentally.
        var cameraId = Guid.NewGuid();
        var (coordinator, watcher, storage) = Build();
        var network = new CameraConfiguration { Id = cameraId };
        network.Connection.Source = CameraSource.Network;
        network.Connection.IpAddress = "10.0.0.1";
        storage.GetAllCameras().Returns(new List<CameraConfiguration> { network });

        coordinator.Start();
        watcher.DeviceRemoved += Raise.EventWith(
            watcher,
            new UsbCameraEventArgs(SampleDescriptor("anything")));

        coordinator.IsUnplugged(cameraId).Should().BeFalse();
    }

    [Fact]
    public void DeviceArrived_AfterUnplug_ClearsState_AndRaisesReplugged()
    {
        var cameraId = Guid.NewGuid();
        var (coordinator, watcher, storage) = Build();
        storage.GetAllCameras().Returns(new List<CameraConfiguration>
        {
            UsbCamera(cameraId, deviceId: "abc"),
        });

        var captured = new List<UsbCameraLifecycleChangedEventArgs>();
        coordinator.StateChanged += (_, e) => captured.Add(e);
        coordinator.Start();

        watcher.DeviceRemoved += Raise.EventWith(
            watcher,
            new UsbCameraEventArgs(SampleDescriptor("abc")));
        watcher.DeviceArrived += Raise.EventWith(
            watcher,
            new UsbCameraEventArgs(SampleDescriptor("abc")));

        coordinator.IsUnplugged(cameraId).Should().BeFalse();
        captured.Should().HaveCount(2);
        captured[0].Phase.Should().Be(UsbCameraLifecyclePhase.Unplugged);
        captured[1].Phase.Should().Be(UsbCameraLifecyclePhase.Replugged);
    }

    [Fact]
    public void DeviceArrived_WithoutPriorUnplug_DoesNotRaise()
    {
        // Watcher implementations may emit DeviceArrived on first
        // enumeration just to populate state; we must not surface that
        // as a "Replugged" event because the camera was never marked
        // unplugged.
        var cameraId = Guid.NewGuid();
        var (coordinator, watcher, storage) = Build();
        storage.GetAllCameras().Returns(new List<CameraConfiguration>
        {
            UsbCamera(cameraId, deviceId: "abc"),
        });

        var raises = 0;
        coordinator.StateChanged += (_, _) => raises++;
        coordinator.Start();

        watcher.DeviceArrived += Raise.EventWith(
            watcher,
            new UsbCameraEventArgs(SampleDescriptor("abc")));

        raises.Should().Be(0);
        coordinator.IsUnplugged(cameraId).Should().BeFalse();
    }

    [Fact]
    public void ClearUnpluggedState_RemovesEntry_ForCameraDeletedWhileUnplugged()
    {
        // Reproduces the leak the §8 finding called out: a USB camera
        // unplugged → deleted → the coordinator's ConcurrentDictionary
        // would otherwise retain the Guid forever because the watcher
        // only clears on a matching DeviceArrived event, which never
        // resolves to the deleted camera's Guid.
        var cameraId = Guid.NewGuid();
        var (coordinator, watcher, storage) = Build();
        storage.GetAllCameras().Returns(new List<CameraConfiguration>
        {
            UsbCamera(cameraId, deviceId: "abc"),
        });

        coordinator.Start();
        watcher.DeviceRemoved += Raise.EventWith(
            watcher,
            new UsbCameraEventArgs(SampleDescriptor("abc")));
        coordinator.IsUnplugged(cameraId).Should().BeTrue();

        coordinator.ClearUnpluggedState(cameraId);

        coordinator.IsUnplugged(cameraId).Should().BeFalse();
    }

    [Fact]
    public void ClearUnpluggedState_UnknownCameraId_IsNoOp()
    {
        // DeleteCameraHandler calls this for every delete (network or
        // USB, present or absent) — must not throw.
        var (coordinator, _, _) = Build();

        var act = () => coordinator.ClearUnpluggedState(Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_StopsListening_AndIsIdempotent()
    {
        var (coordinator, watcher, _) = Build();
        coordinator.Start();

        coordinator.Dispose();
        coordinator.Dispose();

        watcher.Received(1).Stop();

        var act = () => coordinator.Start();
        act.Should().Throw<ObjectDisposedException>();
    }

    private static (UsbCameraLifecycleCoordinator Coordinator,
                    IUsbCameraWatcher Watcher,
                    ICameraStorageService Storage) Build()
    {
        var watcher = Substitute.For<IUsbCameraWatcher>();
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns(new List<CameraConfiguration>());
        var coordinator = new UsbCameraLifecycleCoordinator(watcher, storage);
        return (coordinator, watcher, storage);
    }

    private static CameraConfiguration UsbCamera(
        Guid id,
        string deviceId)
    {
        var camera = new CameraConfiguration { Id = id };
        camera.Connection.Source = CameraSource.Usb;
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = deviceId };
        return camera;
    }

    private static UsbDeviceDescriptor SampleDescriptor(string deviceId)
        => new(deviceId: deviceId, friendlyName: "Cam");
}