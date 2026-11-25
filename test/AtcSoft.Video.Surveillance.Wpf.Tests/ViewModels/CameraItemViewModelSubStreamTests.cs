namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

using CameraSubStreamStatus = global::VideoSurveillance.Generated.Cameras.Models.CameraSubStreamStatus;

/// <summary>
/// The camera list says whether a camera's sub-stream is used.
/// </summary>
public sealed class CameraItemViewModelSubStreamTests
{
    [Fact]
    public void The_List_Says_Whether_The_Sub_Stream_Is_Used()
    {
        CameraItemViewModel.FromCamera(TestCameras.Network()).SubStream.Should().BeEmpty("the camera has none");
        CameraItemViewModel.FromCamera(TestCameras.Network() with { SubStreamStatus = CameraSubStreamStatus.InUse }).SubStream.Should().Be(Translations.SubStreamInUse);
        CameraItemViewModel.FromCamera(TestCameras.Network() with { SubStreamStatus = CameraSubStreamStatus.Unavailable }).SubStream.Should().Be(Translations.SubStreamUnavailable);
    }
}