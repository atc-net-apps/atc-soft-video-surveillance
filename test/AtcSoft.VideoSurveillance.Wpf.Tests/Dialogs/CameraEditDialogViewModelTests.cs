namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

using CameraConnectionType = global::VideoSurveillance.Generated.Cameras.Models.CameraConnectionType;
using CameraOverlayPosition = global::VideoSurveillance.Generated.Cameras.Models.CameraOverlayPosition;
using CameraProtocol = global::VideoSurveillance.Generated.Cameras.Models.CameraProtocol;
using CameraRotation = global::VideoSurveillance.Generated.Cameras.Models.CameraRotation;
using CameraStreamRtspTransport = global::VideoSurveillance.Generated.Cameras.Models.CameraStreamRtspTransport;

/// <summary>
/// The dialog edits a network camera's fields. An update leaves out what the
/// dialog does not edit, so the server keeps it.
/// </summary>
public sealed class CameraEditDialogViewModelTests
{
    [Fact]
    public void An_Update_Leaves_Out_The_Source_And_The_Usb_Settings()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network());

        var request = vm.BuildUpdateRequest();

        request.Source.Should().BeNull();
        request.UsbDeviceId.Should().BeNull();
        request.UsbFriendlyName.Should().BeNull();
        request.UsbWidth.Should().BeNull();
        request.UsbHeight.Should().BeNull();
        request.UsbFrameRate.Should().BeNull();
        request.UsbPixelFormat.Should().BeNull();
        request.UsbCaptureAudio.Should().BeNull();
        request.UsbAudioDeviceName.Should().BeNull();
    }

    [Fact]
    public void An_Update_Without_A_New_Password_Leaves_The_Password_Out()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network());

        vm.BuildUpdateRequest().Password.Should().BeNull();
    }

    [Fact]
    public void An_Update_Sends_The_Fields_The_Dialog_Edits()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network())
        {
            DisplayName = "Back door",
            Path = "/stream2",
            Password = "secret",
        };

        var request = vm.BuildUpdateRequest();

        request.DisplayName.Should().Be("Back door");
        request.Path.Should().Be("/stream2");
        request.Password.Should().Be("secret");
        request.IpAddress.Should().Be("192.0.2.10");
    }

    // The drop-downs are LabelComboBoxes: their SelectedKey is a string, and the item keys
    // are the API's wire values (rtsp, topLeft), as the E2E smoke test showed the binding
    // to the generated structs failing (no converter, no selection shown or saved).
    [Fact]
    public void The_Drop_Down_Keys_Show_The_Edited_Cameras_Values()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network());

        vm.ProtocolItems.Should().ContainKey(vm.SelectedProtocolKey);
        vm.ConnectionTypeItems.Should().ContainKey(vm.SelectedConnectionTypeKey);
        vm.RotationItems.Should().ContainKey(vm.SelectedRotationKey);
        vm.OverlayPositionItems.Should().ContainKey(vm.SelectedOverlayPositionKey);
        vm.RtspTransportItems.Should().ContainKey(vm.SelectedRtspTransportKey);
        vm.SelectedProtocolKey.Should().Be(TestCameras.Network().Protocol.ToString());
    }

    [Fact]
    public void A_Key_Picked_In_A_Drop_Down_Is_Sent_To_The_Server()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network())
        {
            SelectedProtocolKey = "http",
            SelectedConnectionTypeKey = "wifi",
            SelectedRotationKey = "rotate180",
            SelectedOverlayPositionKey = "bottomRight",
            SelectedRtspTransportKey = "udp",
        };

        var request = vm.BuildUpdateRequest();

        request.Protocol.Should().Be(CameraProtocol.Http);
        request.ConnectionType.Should().Be(CameraConnectionType.Wifi);
        request.Rotation.Should().Be(CameraRotation.Rotate180);
        request.OverlayPosition.Should().Be(CameraOverlayPosition.BottomRight);
        request.StreamRtspTransport.Should().Be(CameraStreamRtspTransport.Udp);
    }

    [Fact]
    public void A_Changed_Value_Moves_Its_Drop_Down()
    {
        var vm = new CameraEditDialogViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SelectedRotation = CameraRotation.Rotate90;

        changed.Should().Contain(nameof(CameraEditDialogViewModel.SelectedRotationKey));
        vm.SelectedRotationKey.Should().Be("rotate90");
    }

    [Fact]
    public void Every_Value_Of_The_Api_Has_A_Drop_Down_Entry()
    {
        var vm = new CameraEditDialogViewModel();

        foreach (var protocol in CameraProtocol.KnownValues)
        {
            vm.ProtocolItems.Should().ContainKey(protocol.ToString());
        }

        foreach (var rotation in CameraRotation.KnownValues)
        {
            vm.RotationItems.Should().ContainKey(rotation.ToString());
        }

        foreach (var position in CameraOverlayPosition.KnownValues)
        {
            vm.OverlayPositionItems.Should().ContainKey(position.ToString());
        }

        foreach (var transport in CameraStreamRtspTransport.KnownValues)
        {
            vm.RtspTransportItems.Should().ContainKey(transport.ToString());
        }
    }
}