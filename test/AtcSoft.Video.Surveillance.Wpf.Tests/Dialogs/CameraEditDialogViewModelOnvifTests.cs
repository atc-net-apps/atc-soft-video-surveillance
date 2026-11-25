namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

using OnvifDiscoveredDevice = global::VideoSurveillance.Generated.Onvif.Models.OnvifDiscoveredDevice;
using OnvifDiscoveryResult = global::VideoSurveillance.Generated.Onvif.Models.OnvifDiscoveryResult;
using OnvifProfilesRequest = global::VideoSurveillance.Generated.Onvif.Models.OnvifProfilesRequest;
using OnvifProfilesResult = global::VideoSurveillance.Generated.Onvif.Models.OnvifProfilesResult;
using OnvifProfilesResultOutcome = global::VideoSurveillance.Generated.Onvif.Models.OnvifProfilesResultOutcome;

/// <summary>
/// ONVIF in the camera dialog, through the server.
/// </summary>
public sealed class CameraEditDialogViewModelOnvifTests
{
    private readonly IOnvifGateway onvif = Substitute.For<IOnvifGateway>();

    [Fact]
    public async Task A_Camera_Found_Fills_In_Its_Address_Port_And_Name()
    {
        onvif.DiscoverOnvifCamerasAsync(Arg.Any<CancellationToken>()).Returns(new OnvifDiscoveryResult(
            Devices: [new OnvifDiscoveredDevice("192.168.1.64", 8000, "Front Door", "DS-2CD")],
            CouldProbe: true));
        var vm = new CameraEditDialogViewModel { Onvif = onvif };

        await vm.FindOnvifCamerasCommand.ExecuteAsync(null);
        vm.UseOnvifDeviceCommand.Execute(vm.OnvifDevices.Single());

        (vm.IpAddress, vm.OnvifPort, vm.DisplayName).Should().Be(("192.168.1.64", 8000, "Front Door"));
    }

    [Fact]
    public async Task The_Streams_Read_Fill_In_The_Main_Stream_And_The_Sub_Stream()
    {
        onvif.ReadOnvifProfilesAsync(Arg.Any<OnvifProfilesRequest>(), Arg.Any<CancellationToken>()).Returns(new OnvifProfilesResult(
            Outcome: OnvifProfilesResultOutcome.Read,
            Profiles: [])
        {
            Port = 554,
            Path = "/cam/realmonitor?channel=1&subtype=0",
            SubStreamPath = "/cam/realmonitor?channel=1&subtype=1",
        });
        var vm = new CameraEditDialogViewModel { Onvif = onvif, IpAddress = "192.168.1.64", Username = "admin", Password = "secret" };

        await vm.ReadFromCameraCommand.ExecuteAsync(null);

        await onvif.Received(1).ReadOnvifProfilesAsync(
            Arg.Is<OnvifProfilesRequest>(r => r.Address == "192.168.1.64" && r.UserName == "admin" && r.Password == "secret" && r.Port == null),
            Arg.Any<CancellationToken>());
        (vm.Path, vm.SubStreamPath, vm.SubStreamPort).Should().Be(("/cam/realmonitor?channel=1&subtype=0", "/cam/realmonitor?channel=1&subtype=1", 0));
        vm.OnvifStatus.Should().Be(AtcSoft.Video.Surveillance.Resources.Translations.OnvifReadWithSubStream);
    }

    [Fact]
    public async Task A_Server_That_Refuses_Is_Said_In_Its_Words()
    {
        onvif.DiscoverOnvifCamerasAsync(Arg.Any<CancellationToken>()).Returns<OnvifDiscoveryResult>(_ => throw new HttpRequestException("Forbidden: operators and admins only"));
        var vm = new CameraEditDialogViewModel { Onvif = onvif };

        await vm.FindOnvifCamerasCommand.ExecuteAsync(null);

        vm.OnvifStatus.Should().Be("Forbidden: operators and admins only");
        vm.IsOnvifBusy.Should().BeFalse();
    }
}