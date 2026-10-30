namespace AtcSoft.CameraWall.Wpf.Services;

using IUsbCameraWatcher = AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher;

/// <summary>
/// Pins the seed-camera contract for the AddCamera / AddUsbCamera flow.
/// The dialog itself has its own ViewModel-level coverage; these tests
/// catch the layer between the ribbon command and the dialog where a
/// future refactor could silently drop the Source = Usb pre-selection.
/// </summary>
public class CameraWallManagerUsbTests
{
    [Fact]
    public void AddUsbCamera_PassesSeedCamera_WithSourceUsb_ToDialog()
    {
        var (manager, deps) = BuildManager();
        CameraConfiguration? capturedSeed = null;

        deps.Dialog
            .ShowCameraConfigurationDialog(
                Arg.Do<CameraConfiguration?>(c => capturedSeed = c),
                Arg.Any<bool>(),
                Arg.Any<IReadOnlyCollection<(string, string?)>>())
            .Returns((CameraConfiguration?)null);

        manager.AddUsbCamera();

        capturedSeed.Should().NotBeNull();
        capturedSeed!.Connection.Source.Should().Be(CameraSource.Usb);
        capturedSeed.Connection.Usb.Should().NotBeNull();
    }

    [Fact]
    public void AddCamera_PassesSeedCamera_WithSourceNetwork_ToDialog()
    {
        // Negative case — pin the network-source path too so that a
        // shared-helper refactor can't accidentally seed both flows
        // with the same source kind.
        var (manager, deps) = BuildManager();
        CameraConfiguration? capturedSeed = null;

        deps.Dialog
            .ShowCameraConfigurationDialog(
                Arg.Do<CameraConfiguration?>(c => capturedSeed = c),
                Arg.Any<bool>(),
                Arg.Any<IReadOnlyCollection<(string, string?)>>())
            .Returns((CameraConfiguration?)null);

        manager.AddCamera();

        capturedSeed.Should().NotBeNull();
        capturedSeed!.Connection.Source.Should().Be(CameraSource.Network);
        capturedSeed.Connection.Usb.Should().BeNull();
    }

    [Fact]
    public void UsbDeviceRemoved_StoredUsbCamera_StopsRecording()
    {
        // When the watcher reports a USB device gone, the manager
        // stops the in-flight recording so the blinking record dot
        // clears (otherwise it sits next to the new "Device unplugged"
        // indicator, which is misleading).
        const string deviceId = @"\\?\usb#vid_046d&pid_085e";
        var camera = BuildUsbCamera(deviceId);
        var (_, deps) = BuildManager(usbCameras: [camera]);

        RaiseDeviceRemoved(deps.UsbWatcher, deviceId);

        deps.Recording.Received(1).StopRecording(camera.Id);
    }

    [Fact]
    public void UsbDeviceRemoved_UnknownDeviceId_DoesNotStopRecording()
    {
        // The watcher fires for *every* USB-class device on the host —
        // including ones never enrolled as cameras. Stops must be
        // strictly scoped to enrolled cameras to avoid silently
        // tearing down unrelated recording sessions.
        var enrolled = BuildUsbCamera(@"\\?\usb#vid_046d&pid_085e");
        var (_, deps) = BuildManager(usbCameras: [enrolled]);

        RaiseDeviceRemoved(deps.UsbWatcher, @"\\?\usb#vid_dead&pid_beef");

        deps.Recording.DidNotReceive().StopRecording(Arg.Any<Guid>());
    }

    [Fact]
    public void UsbDeviceArrived_DoesNotStopRecording()
    {
        // Arrival is the inverse of removal — the operator just
        // plugged a camera back in. Must not interfere with whatever
        // recording state the tile transitions to on reconnect.
        const string deviceId = @"\\?\usb#vid_046d&pid_085e";
        var camera = BuildUsbCamera(deviceId);
        var (_, deps) = BuildManager(usbCameras: [camera]);

        RaiseDeviceArrived(deps.UsbWatcher, deviceId);

        deps.Recording.DidNotReceive().StopRecording(Arg.Any<Guid>());
    }

    [Fact]
    public void UsbDeviceRemoved_NetworkCamera_WithMatchingDeviceId_Ignored()
    {
        // Defence-in-depth: even if a network camera somehow carried
        // a USB device-id string in its config (data drift), the
        // manager must not touch it — the resolver is keyed on
        // CameraSource.Usb.
        const string deviceId = @"\\?\usb#vid_046d&pid_085e";
        var bogus = new CameraConfiguration { Id = Guid.NewGuid() };
        bogus.Connection.Source = CameraSource.Network;
        bogus.Connection.Usb = new UsbConnectionSettings { DeviceId = deviceId };
        var (_, deps) = BuildManager(usbCameras: [bogus]);

        RaiseDeviceRemoved(deps.UsbWatcher, deviceId);

        deps.Recording.DidNotReceive().StopRecording(Arg.Any<Guid>());
    }

    [Fact]
    public void AddUsbCamera_DialogCancelled_DoesNotPersist()
    {
        // Cancel returns null from the dialog — make sure the manager
        // doesn't accidentally write a half-baked seed to storage.
        var (manager, deps) = BuildManager();

        deps.Dialog
            .ShowCameraConfigurationDialog(
                Arg.Any<CameraConfiguration?>(),
                Arg.Any<bool>(),
                Arg.Any<IReadOnlyCollection<(string, string?)>>())
            .Returns((CameraConfiguration?)null);

        manager.AddUsbCamera();

        deps.Storage.DidNotReceive().AddOrUpdateCamera(Arg.Any<CameraConfiguration>());
    }

    private static (CameraWallManager Manager, Deps Deps) BuildManager(
        IReadOnlyList<CameraConfiguration>? usbCameras = null)
    {
        var deps = new Deps
        {
            Storage = Substitute.For<ICameraStorageService>(),
            Dialog = Substitute.For<IDialogService>(),
            Settings = Substitute.For<IApplicationSettingsService>(),
            Recording = Substitute.For<IRecordingService>(),
            MotionDetection = Substitute.For<AtcSoft.VideoSurveillance.Services.IMotionDetectionService>(),
            Timelapse = Substitute.For<ITimelapseService>(),
            GitHub = Substitute.For<AtcSoft.VideoSurveillance.Services.IGitHubReleaseService>(),
            Toast = Substitute.For<IToastNotificationService>(),
            PlayerFactory = Substitute.For<IVideoPlayerFactory>(),
            UsbWatcher = Substitute.For<IUsbCameraWatcher>(),
        };

        deps.Storage.GetAllLayouts().Returns([]);
        deps.Storage.GetAllCameras().Returns(usbCameras?.ToList() ?? []);

        var manager = new CameraWallManager(
            NullLogger<CameraWallManager>.Instance,
            deps.Storage,
            deps.Dialog,
            deps.Settings,
            deps.Recording,
            deps.MotionDetection,
            deps.Timelapse,
            deps.GitHub,
            deps.Toast,
            deps.PlayerFactory,
            deps.UsbWatcher);
        return (manager, deps);
    }

    private static CameraConfiguration BuildUsbCamera(string deviceId)
    {
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };
        camera.Connection.Source = CameraSource.Usb;
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = deviceId };
        return camera;
    }

    private static void RaiseDeviceArrived(
        IUsbCameraWatcher watcher,
        string deviceId)
    {
        var descriptor = new UsbDeviceDescriptor(deviceId, friendlyName: "Test Cam");
        watcher.DeviceArrived += Raise.EventWith<UsbCameraEventArgs>(
            watcher,
            new UsbCameraEventArgs(descriptor));
    }

    private static void RaiseDeviceRemoved(
        IUsbCameraWatcher watcher,
        string deviceId)
    {
        var descriptor = new UsbDeviceDescriptor(deviceId, friendlyName: "Test Cam");
        watcher.DeviceRemoved += Raise.EventWith<UsbCameraEventArgs>(
            watcher,
            new UsbCameraEventArgs(descriptor));
    }

    private sealed class Deps
    {
        public ICameraStorageService Storage { get; set; } = null!;

        public IDialogService Dialog { get; set; } = null!;

        public IApplicationSettingsService Settings { get; set; } = null!;

        public IRecordingService Recording { get; set; } = null!;

        public AtcSoft.VideoSurveillance.Services.IMotionDetectionService MotionDetection { get; set; } = null!;

        public ITimelapseService Timelapse { get; set; } = null!;

        public AtcSoft.VideoSurveillance.Services.IGitHubReleaseService GitHub { get; set; } = null!;

        public IToastNotificationService Toast { get; set; } = null!;

        public IVideoPlayerFactory PlayerFactory { get; set; } = null!;

        public IUsbCameraWatcher UsbWatcher { get; set; } = null!;
    }
}