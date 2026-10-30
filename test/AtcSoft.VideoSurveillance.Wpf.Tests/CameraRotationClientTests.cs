namespace AtcSoft.VideoSurveillance.Wpf;

using ApiCameraRotation = global::VideoSurveillance.Generated.Cameras.Models.CameraRotation;
using CoreCameraRotation = AtcSoft.VideoSurveillance.Enums.CameraRotation;

/// <summary>
/// The API client's side of camera rotation: reading it from the server,
/// sending it back, and turning the picture it plays.
/// </summary>
public class CameraRotationClientTests
{
    [Theory]
    [InlineData(ApiCameraRotation.Names.None, CoreCameraRotation.None)]
    [InlineData(ApiCameraRotation.Names.Rotate90, CoreCameraRotation.Rotate90)]
    [InlineData(ApiCameraRotation.Names.Rotate180, CoreCameraRotation.Rotate180)]
    [InlineData(ApiCameraRotation.Names.Rotate270, CoreCameraRotation.Rotate270)]
    public void ToCameraConfiguration_Reads_The_Rotation_From_The_Server(
        string api,
        CoreCameraRotation expected)
    {
        var config = TestCameras.Network(new ApiCameraRotation(api)).ToCameraConfiguration();

        config.Display.Rotation.Should().Be(expected);
    }

    [Fact]
    public void ToCreateCameraRequest_Sends_The_Rotation()
    {
        var config = TestCameras.Network(ApiCameraRotation.Rotate90).ToCameraConfiguration();

        config.ToCreateCameraRequest().Rotation.Should().Be(ApiCameraRotation.Rotate90);
    }

    [Fact]
    public void ToUpdateCameraRequest_Sends_The_Rotation()
    {
        var config = TestCameras.Network(ApiCameraRotation.Rotate270).ToCameraConfiguration();

        config.ToUpdateCameraRequest().Rotation.Should().Be(ApiCameraRotation.Rotate270);
    }

    [Fact]
    public void ToUpdateCameraRequest_Sends_Upright_So_A_Rotation_Can_Be_Removed()
    {
        var config = TestCameras.Network(ApiCameraRotation.None).ToCameraConfiguration();

        config.ToUpdateCameraRequest().Rotation.Should().Be(ApiCameraRotation.None);
    }

    [Fact]
    public void EditDialog_Shows_The_Camera_Rotation()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network(ApiCameraRotation.Rotate180));

        vm.SelectedRotation.Should().Be(ApiCameraRotation.Rotate180);
    }

    [Fact]
    public void EditDialog_Sends_The_Selected_Rotation_On_Update()
    {
        var vm = new CameraEditDialogViewModel(TestCameras.Network(ApiCameraRotation.None))
        {
            SelectedRotation = ApiCameraRotation.Rotate90,
        };

        vm.BuildUpdateRequest().Rotation.Should().Be(ApiCameraRotation.Rotate90);
    }

    [Fact]
    public void EditDialog_Sends_The_Selected_Rotation_On_Create()
    {
        var vm = new CameraEditDialogViewModel
        {
            SelectedRotation = ApiCameraRotation.Rotate270,
        };

        vm.BuildCreateRequest().Rotation.Should().Be(ApiCameraRotation.Rotate270);
    }

    [Theory]
    [InlineData(CoreCameraRotation.Rotate90, VideoRotation.Rotate90)]
    [InlineData(CoreCameraRotation.Rotate180, VideoRotation.Rotate180)]
    [InlineData(CoreCameraRotation.Rotate270, VideoRotation.Rotate270)]
    public async Task Tile_Turns_Its_Player_When_The_Camera_Is_Rotated(
        CoreCameraRotation rotation,
        VideoRotation expected)
    {
        // The server streams the camera's own pixels, so the client rotates.
        var player = Substitute.For<IVideoPlayer>();
        var factory = Substitute.For<IVideoPlayerFactory>();
        factory.Create().Returns(player);
        var streams = Substitute.For<IStreamLeaseGateway>();
        using var events = TestEvents.Service();

        using var tile = new CameraTileViewModel(factory, streams, events, "http://localhost");
        tile.Rotation = rotation;

        player.Received(1).SetRotation(expected);
    }
}