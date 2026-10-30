namespace AtcSoft.VideoSurveillance.Wpf.Windows;

using CoreCameraRotation = AtcSoft.VideoSurveillance.Enums.CameraRotation;

public class FullScreenCameraWindowTests
{
    [Fact]
    public void The_Window_Can_Be_Created()
    {
        // Loading the XAML resolves every StaticResource in it, so a resource
        // key that no longer exists fails here instead of when a user opens a
        // camera in full screen.
        var error = RunOnStaThread(() =>
        {
            using var events = TestEvents.Service();
            using var viewModel = new FullScreenCameraWindowViewModel(
                events,
                Guid.NewGuid(),
                existingPlayer: null,
                "Cam",
                string.Empty,
                CoreCameraRotation.None);
            var window = new FullScreenCameraWindow(viewModel);
            window.Close();
        });

        error.Should().BeNull();
    }

    [Theory]
    [InlineData(CoreCameraRotation.None, 1920, 1080)]
    [InlineData(CoreCameraRotation.Rotate90, 1080, 1920)]
    [InlineData(CoreCameraRotation.Rotate180, 1920, 1080)]
    [InlineData(CoreCameraRotation.Rotate270, 1080, 1920)]
    public void The_Motion_Boxes_Are_Sized_On_The_Picture_As_It_Is_Shown(
        CoreCameraRotation rotation,
        int expectedWidth,
        int expectedHeight)
    {
        // The player turns the picture, so a quarter turn swaps width and height.
        var player = Substitute.For<IVideoPlayer>();
        player.StreamInfo.Returns(new VideoStreamInfo { Width = 1920, Height = 1080 });
        using var events = TestEvents.Service();

        using var viewModel = new FullScreenCameraWindowViewModel(
            events,
            Guid.NewGuid(),
            player,
            "Cam",
            string.Empty,
            rotation);

        viewModel.MotionVideoWidth.Should().Be(expectedWidth);
        viewModel.MotionVideoHeight.Should().Be(expectedHeight);
    }

    private static Exception? RunOnStaThread(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return error;
    }
}