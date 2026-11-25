namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

using CameraConfiguration = AtcSoft.Video.Surveillance.Wpf.Core.Models.CameraConfiguration;
using IWpfCoreSettingsService = AtcSoft.Video.Surveillance.Wpf.Core.Services.IApplicationSettingsService;
using Translations = AtcSoft.Video.Surveillance.Resources.Translations;

/// <summary>
/// ONVIF in the camera form: the cameras on the network are listed and one picked fills in its
/// address; the streams read from a camera fill in the main stream and the sub-stream.
/// </summary>
public sealed class CameraConfigurationDialogViewModelOnvifTests
{
    private readonly IOnvifDiscovery discovery = Substitute.For<IOnvifDiscovery>();
    private readonly IOnvifMediaClient client = Substitute.For<IOnvifMediaClient>();

    [Fact]
    public async Task The_Cameras_Found_Are_Listed_And_One_Picked_Fills_In_Its_Address_And_Name()
    {
        discovery.DiscoverAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns(new OnvifDiscoveryResult(
            [new OnvifDevice("urn:uuid:1", new Uri("http://192.168.1.64:8000/onvif/device_service"), "Front Door", "DS-2CD")],
            CouldProbe: true));
        var vm = BuildVm();

        await vm.FindOnvifCamerasCommand.ExecuteAsync(null);
        vm.UseOnvifDeviceCommand.Execute(vm.OnvifDevices.Single());

        vm.OnvifDevices.Single().Text.Should().Be("Front Door · DS-2CD · 192.168.1.64");
        vm.Camera.Connection.IpAddress.Should().Be("192.168.1.64");
        vm.OnvifPort.Should().Be(8000);
        vm.Camera.Display.DisplayName.Should().Be("Front Door");
        vm.SelectedTabIndex.Should().Be(CameraConfigurationDialogViewModel.CameraTabIndex, "the address picked is on the Camera tab");
    }

    [Fact]
    public async Task A_Network_That_Cannot_Be_Searched_Says_So()
    {
        discovery.DiscoverAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns(new OnvifDiscoveryResult([], CouldProbe: false));
        var vm = BuildVm();

        await vm.FindOnvifCamerasCommand.ExecuteAsync(null);

        vm.OnvifStatus.Should().Be(Translations.OnvifNoMulticast);
    }

    [Fact]
    public async Task The_Streams_Read_Fill_In_The_Main_Stream_And_The_Sub_Stream()
    {
        var main = new OnvifStreamProfile("1", "main", "H264", 1920, 1080, new Uri("rtsp://10.0.0.9:554/Streaming/Channels/101"));
        var sub = new OnvifStreamProfile("2", "sub", "H264", 640, 360, new Uri("rtsp://10.0.0.9:8554/Streaming/Channels/102"));
        client.ReadProfilesAsync("192.168.1.64", null, "admin", "secret", Arg.Any<CancellationToken>())
            .Returns(new OnvifImportResult(OnvifImportOutcome.Read, [main, sub], new AtcSoft.Video.Surveillance.Helpers.OnvifStreamChoice(main, sub)));
        var vm = BuildVm();
        vm.Camera.Connection.IpAddress = "192.168.1.64";
        vm.Camera.Authentication.UserName = "admin";
        vm.Camera.Authentication.Password = "secret";

        await vm.ReadFromCameraCommand.ExecuteAsync(null);

        vm.Camera.Connection.IpAddress.Should().Be("192.168.1.64", "the address typed is kept");
        (vm.Camera.Connection.Protocol, vm.Camera.Connection.Port, vm.Camera.Connection.Path).Should().Be((CameraProtocol.Rtsp, 554, "/Streaming/Channels/101"));
        (vm.Camera.Connection.SubStreamPath, vm.SubStreamPortValue).Should().Be(("/Streaming/Channels/102", 8554));
        vm.OnvifReadStatus.Should().Be(Translations.OnvifReadWithSubStream, "the read says how it went on the Camera tab");
        vm.OnvifStatus.Should().BeNull("the search status on the Find tab is the search's");
    }

    [Fact]
    public async Task A_Refused_Sign_In_Changes_Nothing_And_Says_So()
    {
        client.ReadProfilesAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OnvifImportResult.Failed(OnvifImportOutcome.Unauthorized));
        var vm = BuildVm();
        vm.Camera.Connection.IpAddress = "192.168.1.64";
        vm.Camera.Connection.Path = "/typed";

        await vm.ReadFromCameraCommand.ExecuteAsync(null);

        vm.Camera.Connection.Path.Should().Be("/typed");
        vm.OnvifReadStatus.Should().Be(Translations.OnvifUnauthorized);
    }

    [Fact]
    public void Without_Onvif_The_Part_Is_Not_Shown()
    {
        var vm = BuildVm();
        vm.UseOnvif(discovery: null, mediaClient: null);

        vm.CanUseOnvif.Should().BeFalse();
    }

    [Fact]
    public void A_New_Network_Camera_Has_The_Find_And_Stream_Tabs()
    {
        var vm = BuildVm();

        (vm.ShowFindTab, vm.ShowStreamTab).Should().Be((true, true));
    }

    [Fact]
    public void A_Usb_Camera_Leaves_The_Find_Tab_For_The_Camera_Tab()
    {
        var vm = BuildVm();
        vm.SelectedTabIndex = CameraConfigurationDialogViewModel.FindTabIndex;

        vm.IsUsbSource = true;

        (vm.ShowFindTab, vm.ShowStreamTab).Should().Be((false, false), "a USB camera is neither found on the network nor streamed over it");
        vm.SelectedTabIndex.Should().Be(CameraConfigurationDialogViewModel.CameraTabIndex, "a hidden tab is not left selected");
    }

    [Fact]
    public void A_Camera_Edited_Without_An_Onvif_Search_Has_No_Find_Tab()
    {
        var vm = BuildVm(isNew: false);
        vm.UseOnvif(discovery: null, client);

        vm.ShowFindTab.Should().BeFalse("the scanner and the hubs are for a new camera, and there is no search");
        vm.ShowStreamTab.Should().BeTrue();
    }

    private CameraConfigurationDialogViewModel BuildVm(bool isNew = true)
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
        vm.UseOnvif(discovery, client);
        return vm;
    }
}