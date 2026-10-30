namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// A USB camera is streamed to browsers from the one pipeline the server
/// keeps for it: Windows would not let a second program open the camera,
/// so a stream with a process of its own would shut out every recording.
/// </summary>
public sealed class StreamingServicePipelineTests : IDisposable
{
    private readonly CameraConfiguration camera = new() { Id = Guid.NewGuid() };
    private readonly ICameraStorageService storage = Substitute.For<ICameraStorageService>();
    private readonly ICameraPipelineHost host = Substitute.For<ICameraPipelineHost>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly StreamingService sut;

    public StreamingServicePipelineTests()
    {
        camera.Connection.Source = CoreCameraSource.Usb;
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "usb#1", FriendlyName = "Webcam" };
        storage.GetCameraById(camera.Id).Returns(camera);
        host.Hosts(camera).Returns(true);
        host.Acquire(camera, CameraPipelineUse.Viewing).Returns(pipeline);
        pipeline.FramesDecoded.Returns(100);
        pipeline.IsLiveStreamActive.Returns(true);

        sut = new StreamingService(storage, NullLogger<StreamingService>.Instance, host);
    }

    public void Dispose()
    {
        sut.Dispose();
        pipeline.Dispose();
    }

    [Fact]
    public void A_Usb_Camera_Is_Streamed_From_The_Pipeline_The_Server_Keeps_For_It()
    {
        var playlist = sut.StartStream(camera.Id);

        playlist.Should().Be(Path.Combine(sut.HlsOutputRoot, camera.Id.ToString("N"), "stream.m3u8"));
        host.Received(1).Acquire(camera, CameraPipelineUse.Viewing);
        pipeline.Received(1).StartLiveStream(playlist);
        Directory.Exists(Path.GetDirectoryName(playlist)).Should().BeTrue("the pipeline writes the segments there");
        sut.IsStreaming(camera.Id).Should().BeTrue();
        sut.HasProcessExited(camera.Id).Should().BeFalse();
    }

    [Fact]
    public void A_Second_Viewer_Shares_The_Stream()
    {
        sut.StartStream(camera.Id);
        sut.StartStream(camera.Id);

        host.Received(1).Acquire(camera, CameraPipelineUse.Viewing);
        pipeline.Received(1).StartLiveStream(Arg.Any<string>());
        sut.GetViewerCount(camera.Id).Should().Be(2);
    }

    [Fact]
    public void The_Stream_Stops_With_Its_Last_Viewer_And_The_Pipeline_Is_Given_Back_Not_Closed()
    {
        var playlist = sut.StartStream(camera.Id);
        sut.StartStream(camera.Id);

        sut.StopStream(camera.Id);
        pipeline.DidNotReceive().StopLiveStream();
        host.DidNotReceiveWithAnyArgs().Release(Guid.Empty, default);

        sut.StopStream(camera.Id);

        pipeline.Received(1).StopLiveStream();
        host.Received(1).Release(camera.Id, CameraPipelineUse.Viewing);
        pipeline.DidNotReceive().Dispose();
        sut.IsStreaming(camera.Id).Should().BeFalse();
        Directory.Exists(Path.GetDirectoryName(playlist)).Should().BeFalse();
    }

    [Fact]
    public async Task A_Camera_That_Was_Opened_For_The_Viewer_Is_Streamed_Once_It_Delivers_Pictures()
    {
        // Nobody had the camera open: the host opened it just now, and a
        // pipeline cannot stream before it plays.
        pipeline.FramesDecoded.Returns(0);

        var playlist = sut.StartStream(camera.Id);

        pipeline.DidNotReceiveWithAnyArgs().StartLiveStream(default!);
        sut.HasProcessExited(camera.Id).Should().BeFalse("it is still coming up; the viewer waits");

        pipeline.FramesDecoded.Returns(1);

        await WaitForAsync(() => pipeline.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IMediaPipeline.StartLiveStream)));
        pipeline.Received(1).StartLiveStream(playlist);
    }

    [Fact]
    public void A_Camera_That_Fails_To_Open_For_The_Viewer_Ends_The_Stream()
    {
        pipeline.FramesDecoded.Returns(0);
        sut.StartStream(camera.Id);

        pipeline.ConnectionStateChanged += Raise.EventWith(
            new ConnectionStateChangedEventArgs(ConnectionState.Connecting, ConnectionState.Error));

        sut.HasProcessExited(camera.Id).Should().BeTrue();
    }

    [Fact]
    public async Task A_Viewer_Who_Leaves_Before_The_Camera_Came_Up_Gets_No_Stream_Started_Behind_Them()
    {
        pipeline.FramesDecoded.Returns(0);
        sut.StartStream(camera.Id);

        sut.StopStream(camera.Id);
        pipeline.FramesDecoded.Returns(1);

        await Task.Delay(600, TestContext.Current.CancellationToken);
        pipeline.DidNotReceiveWithAnyArgs().StartLiveStream(default!);
        host.Received(1).Release(camera.Id, CameraPipelineUse.Viewing);
    }

    [Fact]
    public void A_Stream_Whose_Pipeline_Was_Closed_Counts_As_Ended()
    {
        // The camera was unplugged.
        sut.StartStream(camera.Id);

        pipeline.IsLiveStreamActive.Returns(false);

        sut.HasProcessExited(camera.Id).Should().BeTrue();
    }

    [Fact]
    public void A_Usb_Camera_That_Cannot_Be_Opened_Right_Now_Says_So_And_Leaves_No_Stream_Behind()
    {
        host.Acquire(camera, CameraPipelineUse.Viewing).Returns((IMediaPipeline?)null);

        var act = () => sut.StartStream(camera.Id);

        act.Should().Throw<InvalidOperationException>().WithMessage("*USB camera*unplugged*");
        sut.IsStreaming(camera.Id).Should().BeFalse();
        sut.GetViewerCount(camera.Id).Should().Be(0);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The stream was not started.");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }
}