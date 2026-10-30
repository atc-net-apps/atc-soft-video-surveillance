namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// The camera list shows the state in words (<c>Connected</c>), the same
/// whether it came from the camera list or from a hub event.
/// </summary>
public sealed class CameraItemViewModelStateTests
{
    [Theory]
    [InlineData(global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.Connected, "Connected")]
    [InlineData(global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.DeviceUnplugged, "Device unplugged")]
    [InlineData("rebooting", "Rebooting")]
    public void The_State_Is_Shown_In_Words(
        string state,
        string expected)
    {
        var camera = TestCameras.Network() with
        {
            ConnectionState = new global::VideoSurveillance.Generated.Models.CameraConnectionState(state),
        };

        CameraItemViewModel.FromCamera(camera).ConnectionState.Should().Be(expected);
    }

    [Fact]
    public void A_Camera_Without_A_State_Is_Disconnected()
        => CameraItemViewModel.FromCamera(TestCameras.Network()).ConnectionState.Should().Be("Disconnected");
}