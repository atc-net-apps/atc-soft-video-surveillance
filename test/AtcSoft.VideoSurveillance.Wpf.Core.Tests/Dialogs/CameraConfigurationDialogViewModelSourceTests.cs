namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using CameraConfiguration = AtcSoft.VideoSurveillance.Wpf.Core.Models.CameraConfiguration;
using IWpfCoreSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;

public class CameraConfigurationDialogViewModelSourceTests
{
    [Fact]
    public void IsNetworkSource_True_When_Connection_Source_Is_Network()
    {
        var vm = BuildVm(BuildNetworkCamera());

        vm.IsNetworkSource.Should().BeTrue();
        vm.IsUsbSource.Should().BeFalse();
    }

    [Fact]
    public void IsUsbSource_True_When_Connection_Source_Is_Usb()
    {
        var vm = BuildVm(BuildUsbCamera());

        vm.IsUsbSource.Should().BeTrue();
        vm.IsNetworkSource.Should().BeFalse();
    }

    [Fact]
    public void Switching_From_Network_To_Usb_Resets_IpAddress_Port_Auth()
    {
        var camera = BuildNetworkCamera();
        camera.Connection.IpAddress = "10.0.0.1";
        camera.Connection.Port = 554;
        camera.Connection.Path = "stream1";
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = "secret";

        var vm = BuildVm(camera);
        vm.IsUsbSource = true;

        // The VM owns its own clone — assert against vm.Camera, not the seed.
        vm.Camera.Connection.Source.Should().Be(CameraSource.Usb);
        vm.Camera.Connection.IpAddress.Should().BeEmpty();
        vm.Camera.Connection.Port.Should().Be(0);
        vm.Camera.Connection.Path.Should().BeNull();
        vm.Camera.Authentication.UserName.Should().BeEmpty();
        vm.Camera.Authentication.Password.Should().BeEmpty();
        vm.Camera.Connection.Usb.Should().NotBeNull();
    }

    [Fact]
    public void Switching_From_Usb_To_Network_Resets_UsbSettings()
    {
        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings
        {
            DeviceId = "abc",
            FriendlyName = "Cam",
            Format = new AtcSoft.VideoSurveillance.Models.UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30 },
        };

        var vm = BuildVm(camera);
        vm.IsNetworkSource = true;

        vm.Camera.Connection.Source.Should().Be(CameraSource.Network);
        vm.Camera.Connection.Usb.Should().BeNull();
    }

    [Fact]
    public void Switching_To_Same_Source_Is_NoOp()
    {
        var camera = BuildNetworkCamera();
        camera.Connection.IpAddress = "10.0.0.1";

        var vm = BuildVm(camera);
        vm.IsNetworkSource = true; // already Network — must not wipe fields

        vm.Camera.Connection.IpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public void Switching_To_Usb_Sets_ConnectionType_Usb()
    {
        var camera = BuildNetworkCamera();
        camera.Connection.ConnectionType = CameraConnectionType.Wifi;

        var vm = BuildVm(camera);
        vm.IsUsbSource = true;

        vm.Camera.Connection.ConnectionType.Should().Be(CameraConnectionType.Usb);
        vm.SelectedConnectionTypeKey.Should().Be(nameof(CameraConnectionType.Usb));
    }

    [Fact]
    public void Switching_From_Usb_To_Network_Resets_ConnectionType_To_None()
    {
        var camera = BuildUsbCamera();
        camera.Connection.ConnectionType = CameraConnectionType.Usb;

        var vm = BuildVm(camera);
        vm.IsNetworkSource = true;

        vm.Camera.Connection.ConnectionType.Should().Be(CameraConnectionType.None);
    }

    [Fact]
    public void SelectedConnectionTypeKey_Sets_ConnectionType()
    {
        var vm = BuildVm(BuildNetworkCamera());

        vm.SelectedConnectionTypeKey = nameof(CameraConnectionType.Lan);

        vm.Camera.Connection.ConnectionType.Should().Be(CameraConnectionType.Lan);
    }

    [Fact]
    public void SelectedConnectionTypeKey_Ignores_Usb_For_Network_Camera()
    {
        var vm = BuildVm(BuildNetworkCamera());

        vm.SelectedConnectionTypeKey = nameof(CameraConnectionType.Usb);

        vm.Camera.Connection.ConnectionType.Should().Be(CameraConnectionType.None);
    }

    [Fact]
    public void Detection_Is_Unavailable_Without_Detector()
    {
        var vm = BuildVm(BuildNetworkCamera());

        vm.IsConnectionTypeDetectionAvailable.Should().BeFalse();
        vm.DetectConnectionTypeCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Detect_Cannot_Execute_Without_IpAddress()
    {
        var vm = BuildVmWithDetector(BuildNetworkCamera(), CameraConnectionType.Wifi);

        vm.IsConnectionTypeDetectionAvailable.Should().BeTrue();
        vm.DetectConnectionTypeCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Detect_Explicit_Overwrites_Current_Value()
    {
        var camera = BuildNetworkCamera();
        camera.Connection.IpAddress = "10.0.0.5";
        camera.Connection.ConnectionType = CameraConnectionType.Lan;
        var vm = BuildVmWithDetector(camera, CameraConnectionType.Wifi);

        await vm.DetectConnectionTypeCommand.ExecuteAsync(null);

        vm.Camera.Connection.ConnectionType.Should().Be(CameraConnectionType.Wifi);
        vm.ConnectionTypeDetectionStatus.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Detect_Failure_Keeps_Current_Value_And_Reports_It()
    {
        var camera = BuildNetworkCamera();
        camera.Connection.IpAddress = "10.0.0.5";
        camera.Connection.ConnectionType = CameraConnectionType.Lan;
        var vm = BuildVmWithDetector(camera, CameraConnectionType.None);

        await vm.DetectConnectionTypeCommand.ExecuteAsync(null);

        vm.Camera.Connection.ConnectionType.Should().Be(CameraConnectionType.Lan);
        vm.ConnectionTypeDetectionStatus.Should().Be(AtcSoft.VideoSurveillance.Resources.Translations.ConnectionTypeDetectFailed);
    }

    [Fact]
    public async Task Detect_Passes_Camera_Credentials()
    {
        var camera = BuildNetworkCamera();
        camera.Connection.IpAddress = "10.0.0.5";
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = "secret";
        var detector = Substitute.For<ICameraConnectionTypeDetector>();
        detector.DetectAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(CameraConnectionType.Lan);
        var vm = BuildVmCore(camera, [], Substitute.For<IVideoPlayerFactory>(), isNew: true, detector);

        await vm.DetectConnectionTypeCommand.ExecuteAsync(null);

        await detector.Received(1).DetectAsync("10.0.0.5", "admin", "secret", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CanEditSource_Is_True_For_New_Camera_And_False_For_Existing()
    {
        var camera = BuildNetworkCamera();

        BuildVm(camera, isNew: true).CanEditSource.Should().BeTrue();
        BuildVm(camera, isNew: false).CanEditSource.Should().BeFalse();
    }

    [Fact]
    public void CanEditUsbDevice_Is_True_For_New_Camera_And_False_For_Existing()
    {
        var camera = BuildUsbCamera();

        BuildVm(camera, isNew: true).CanEditUsbDevice.Should().BeTrue();
        BuildVm(camera, isNew: false).CanEditUsbDevice.Should().BeFalse();
    }

    [Fact]
    public void HasMinimumSourceIdentity_Network_Requires_IpAddress()
    {
        // Indirect check via CanSave — directly testing the private
        // helper would be brittle and the gating effect on Save is
        // the user-visible behaviour we care about.
        var camera = BuildNetworkCamera();
        camera.Display.DisplayName = "Cam";

        var vm = BuildVm(camera);
        vm.SaveCommand.CanExecute(null).Should().BeFalse();

        vm.Camera.Connection.IpAddress = "10.0.0.1";
        vm.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void HasMinimumSourceIdentity_Usb_Requires_Device_OrFriendlyName()
    {
        var camera = BuildUsbCamera();
        camera.Display.DisplayName = "Cam";
        camera.Connection.Usb = new UsbConnectionSettings();

        var vm = BuildVm(camera);
        vm.SaveCommand.CanExecute(null).Should().BeFalse();

        vm.Camera.Connection.Usb!.DeviceId = "abc";
        vm.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Save_Is_Blocked_For_A_Network_Camera_With_A_Port_Out_Of_Range(
        int port)
    {
        var camera = BuildNetworkCamera();
        camera.Display.DisplayName = "Cam";
        camera.Connection.IpAddress = "10.0.0.1";

        var vm = BuildVm(camera);
        vm.SaveCommand.CanExecute(null).Should().BeTrue();

        vm.Camera.Connection.Port = port;
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Save_Is_Blocked_For_A_Display_Name_That_Is_Too_Long()
    {
        var camera = BuildNetworkCamera();
        camera.Display.DisplayName = "Cam";
        camera.Connection.IpAddress = "10.0.0.1";

        var vm = BuildVm(camera);
        vm.SaveCommand.CanExecute(null).Should().BeTrue();

        vm.Camera.Display.DisplayName = new string('x', 257);
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Save_Is_Allowed_For_A_Usb_Camera_Without_Address_And_Port()
    {
        // Switching a camera to USB clears its address and sets port 0;
        // the network rules must not apply to it.
        var camera = BuildUsbCamera();
        camera.Display.DisplayName = "Cam";
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "abc" };
        camera.Connection.IpAddress = string.Empty;
        camera.Connection.Port = 0;

        var vm = BuildVm(camera);

        vm.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task TestConnection_UsbCamera_Opens_Pipeline_With_Dshow_Locator()
    {
        // USB cameras must route the test-connection path through
        // CameraUriHelper.BuildSourceLocator so the dshow input-format
        // + raw device spec reach the player. Network's BuildUri()
        // path would throw InvalidOperationException for USB sources.
        var camera = BuildUsbCamera();
        camera.Display.DisplayName = "Lab Cam";
        camera.Connection.Usb = new UsbConnectionSettings
        {
            DeviceId = @"\\?\usb#vid_046d&pid_085e",
            FriendlyName = "Logitech BRIO",
            Format = new AtcSoft.VideoSurveillance.Models.UsbStreamFormat
            {
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                PixelFormat = "nv12",
            },
        };

        Uri? capturedUri = null;
        StreamOptions? capturedOptions = null;
        var player = Substitute.For<IVideoPlayer>();
        player
            .When(p => p.Open(Arg.Any<Uri>(), Arg.Any<StreamOptions>()))
            .Do(call =>
            {
                capturedUri = call.Arg<Uri>();
                capturedOptions = call.Arg<StreamOptions>();
                player.StateChanged += Raise.EventWith(
                    player,
                    new PlayerStateChangedEventArgs(PlayerState.Stopped, PlayerState.Playing));
            });

        var playerFactory = Substitute.For<IVideoPlayerFactory>();
        playerFactory.Create().Returns(player);

        var vm = BuildVmWithPlayerFactory(camera, playerFactory);

        await vm.TestConnectionCommand.ExecuteAsync(parameter: null);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.InputFormat.Should().Be(InputFormatKind.Dshow);
        capturedOptions.RawDeviceSpec.Should().Be("video=Logitech BRIO");
        capturedOptions.VideoSize.Should().Be("1920x1080");
        capturedOptions.FrameRate.Should().Be("30");

        // PixelFormat is deliberately suppressed for USB by CameraUriHelper
        // (see comment at CameraUriHelper.cs:120) — MF enumerates transcoded
        // formats but dshow only sees raw output, so forwarding PixelFormat
        // breaks MJPG-only cameras. This assertion guards against accidental
        // pixel-format forwarding being reintroduced.
        capturedOptions.PixelFormat.Should().BeNull();
    }

    [Fact]
    public void Endpoint_Uniqueness_Skipped_For_Usb_Cameras()
    {
        // The endpoint-uniqueness check is bypassed for USB cameras
        // because two recording configs against the same physical
        // device are operator-meaningful, and the OS arbitrates the
        // single-open contention at runtime regardless.
        IReadOnlyCollection<(string IpAddress, string? Path)> existing = [("10.0.0.1", "stream1")];

        var usbCamera = BuildUsbCamera();
        usbCamera.Display.DisplayName = "USB Cam";
        usbCamera.Connection.Usb = new UsbConnectionSettings { DeviceId = "abc" };

        var vm = BuildVmWithEndpoints(usbCamera, existing);

        vm.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    private static CameraConfigurationDialogViewModel BuildVm(
        CameraConfiguration camera,
        bool isNew = true)
        => BuildVmWithEndpoints(camera, [], isNew);

    private static CameraConfigurationDialogViewModel BuildVmWithEndpoints(
        CameraConfiguration camera,
        IReadOnlyCollection<(string IpAddress, string? Path)> endpoints,
        bool isNew = true)
        => BuildVmCore(camera, endpoints, Substitute.For<IVideoPlayerFactory>(), isNew);

    private static CameraConfigurationDialogViewModel BuildVmWithPlayerFactory(
        CameraConfiguration camera,
        IVideoPlayerFactory playerFactory,
        bool isNew = true)
        => BuildVmCore(camera, [], playerFactory, isNew);

    private static CameraConfigurationDialogViewModel BuildVmWithDetector(
        CameraConfiguration camera,
        CameraConnectionType detected)
    {
        var detector = Substitute.For<ICameraConnectionTypeDetector>();
        detector.DetectAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(detected);
        return BuildVmCore(camera, [], Substitute.For<IVideoPlayerFactory>(), isNew: true, detector);
    }

    private static CameraConfigurationDialogViewModel BuildVmCore(
        CameraConfiguration camera,
        IReadOnlyCollection<(string IpAddress, string? Path)> endpoints,
        IVideoPlayerFactory playerFactory,
        bool isNew,
        ICameraConnectionTypeDetector? detector = null)
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings());
        settingsService.Connection.Returns(new ConnectionAppSettings());
        settingsService.Recording.Returns(new RecordingSettings());
        settingsService.Performance.Returns(new PerformanceSettings());
        settingsService.MotionDetection.Returns(new MotionDetectionSettings());

        return new CameraConfigurationDialogViewModel(
            camera,
            isNew,
            endpoints,
            settingsService,
            playerFactory,
            NullUsbCameraEnumerator.Instance,
            detector);
    }

    private static CameraConfiguration BuildNetworkCamera()
    {
        var camera = new CameraConfiguration();
        camera.Connection.Source = CameraSource.Network;
        camera.Connection.Protocol = CameraProtocol.Rtsp;
        camera.Connection.Port = 554;
        return camera;
    }

    private static CameraConfiguration BuildUsbCamera()
    {
        var camera = new CameraConfiguration();
        camera.Connection.Source = CameraSource.Usb;
        camera.Connection.Usb = new UsbConnectionSettings();
        return camera;
    }
}