namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

using CameraConfiguration = AtcSoft.Video.Surveillance.Wpf.Core.Models.CameraConfiguration;
using IWpfCoreSettingsService = AtcSoft.Video.Surveillance.Wpf.Core.Services.IApplicationSettingsService;

/// <summary>
/// A new camera from a camera hub (motionEye on a Raspberry Pi): the camera form lists the hub's
/// cameras and fills in the camera from the one picked.
/// </summary>
public sealed class CameraConfigurationDialogViewModelHubTests
{
    private static readonly Uri Hub = new("http://pi-garage:8765/");

    private static readonly HubCamera Garage = new("1", "Garage", new Uri("http://pi-garage:8081/"), HubStreamKind.Mjpeg, "viewer", "pw", "1280x720", 5, IsUsb: true, HubCameraProblem.None);

    private static readonly HubCamera Off = new("4", "Local only", null, HubStreamKind.Mjpeg, null, null, null, null, IsUsb: true, HubCameraProblem.StreamingOff);

    [Fact]
    public void The_Hub_Part_Is_Offered_For_A_New_Network_Camera_In_An_App_That_Reaches_Hubs()
    {
        BuildVm(isNew: true, Provider()).CanUseCameraHub.Should().BeTrue();
        BuildVm(isNew: false, Provider()).CanUseCameraHub.Should().BeFalse("an existing camera keeps its connection");
        BuildVm(isNew: true, provider: null).CanUseCameraHub.Should().BeFalse("the app reaches no hubs itself");
    }

    [Fact]
    public async Task Connecting_Lists_The_Hubs_Cameras_With_Why_One_Cannot_Be_Used()
    {
        var vm = BuildVm(isNew: true, Provider());
        vm.HubAddress = "pi-garage";

        await vm.ConnectHubCommand.ExecuteAsync(null);

        vm.HubStatus.Should().Be("Cameras on the hub: 2");
        vm.HubCameras.Select(c => c.Name).Should().Equal("Garage", "Local only");
        vm.HubCameras[0].CanUse.Should().BeTrue();
        vm.HubCameras[0].Details.Should().Be("1280x720, 5 fps, USB");
        vm.HubCameras[1].CanUse.Should().BeFalse();
        vm.HubCameras[1].Problem.Should().Contain("Video Streaming");
        vm.HubWarning.Should().BeNull();
    }

    [Fact]
    public async Task An_Older_motionEye_Is_Listed_With_A_Warning()
    {
        var vm = BuildVm(isNew: true, Provider(older: true));
        vm.HubAddress = "pi-garage";

        await vm.ConnectHubCommand.ExecuteAsync(null);

        vm.HubWarning.Should().Contain("less secure");
    }

    [Fact]
    public async Task A_Refused_Password_Says_So()
    {
        var provider = Substitute.For<ICameraHubProvider>();
        provider.ListAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CameraHubListing.Failed(CameraHubOutcome.SignInRefused, CameraHubKinds.MotionEye, Hub));
        var vm = BuildVm(isNew: true, provider);
        vm.HubAddress = "pi-garage";

        await vm.ConnectHubCommand.ExecuteAsync(null);

        vm.HubStatus.Should().Be("The hub refused the user name or password.");
        vm.HubCameras.Should().BeEmpty();
    }

    [Fact]
    public async Task Using_A_Hub_Camera_Fills_In_The_Camera()
    {
        var vm = BuildVm(isNew: true, Provider());
        vm.HubAddress = "pi-garage";
        await vm.ConnectHubCommand.ExecuteAsync(null);

        vm.UseHubCameraCommand.Execute(vm.HubCameras[0]);

        vm.Camera.Connection.Protocol.Should().Be(CameraProtocol.Http);
        vm.Camera.Connection.IpAddress.Should().Be("pi-garage");
        vm.Camera.Connection.Port.Should().Be(8081);
        vm.Camera.Authentication.UserName.Should().Be("viewer");
        vm.Camera.Display.DisplayName.Should().Be("Garage");
        vm.Camera.Connection.Hub!.CameraId.Should().Be("1");
        vm.OverrideTranscodeVideoCodec.Should().Be("H264", "MJPEG is recorded as H.264");
    }

    private static ICameraHubProvider Provider(bool older = false)
    {
        var provider = Substitute.For<ICameraHubProvider>();
        provider.Kind.Returns(CameraHubKinds.MotionEye);
        provider.ListAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CameraHubListing(CameraHubOutcome.Listed, CameraHubKinds.MotionEye, Hub, [Garage, Off]) { OlderSignIn = older });
        provider.GetSnapshotAsync(Arg.Any<Uri>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        return provider;
    }

    private static CameraConfigurationDialogViewModel BuildVm(
        bool isNew,
        ICameraHubProvider? provider)
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings());
        settingsService.Connection.Returns(new ConnectionAppSettings());
        settingsService.Recording.Returns(new RecordingSettings());
        settingsService.Performance.Returns(new PerformanceSettings());
        settingsService.MotionDetection.Returns(new MotionDetectionSettings());

        var vm = new CameraConfigurationDialogViewModel(
            new CameraConfiguration(),
            isNew,
            [],
            settingsService,
            Substitute.For<IVideoPlayerFactory>(),
            NullUsbCameraEnumerator.Instance);
        vm.UseCameraHubs(provider);
        return vm;
    }
}