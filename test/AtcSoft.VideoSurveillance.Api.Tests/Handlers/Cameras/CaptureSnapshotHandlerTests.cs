namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

public class CaptureSnapshotHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_CameraNotFound_ReturnsNotFound()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        var settingsService = Substitute.For<IApplicationSettingsService>();
        var cameraId = Guid.NewGuid();
        storage.GetCameraById(cameraId).Returns((CameraConfiguration?)null);
        var handler = new CaptureSnapshotHandler(storage, pipelineFactory, settingsService);

        // Act
        var result = await handler.ExecuteAsync(
            new CaptureSnapshotParameters(cameraId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task A_Camera_Being_Recorded_Gives_The_Recordings_Picture_Without_Being_Opened_Again()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        var settingsService = Substitute.For<IApplicationSettingsService>();
        var folder = Path.Combine(Path.GetTempPath(), "snapshot-" + Guid.NewGuid().ToString("N"));
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings { SnapshotPath = folder });
        var recordings = Substitute.For<IRecordingService>();
        var recording = Substitute.For<IMediaPipeline>();
        var camera = new CameraConfiguration();
        storage.GetCameraById(camera.Id).Returns(camera);
        recordings.FindPipeline(camera.Id).Returns(recording);
        recording.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns(new byte[] { 1, 2, 3 });
        var handler = new CaptureSnapshotHandler(storage, pipelineFactory, settingsService, recordings: recordings);

        try
        {
            var result = await handler.ExecuteAsync(new CaptureSnapshotParameters(camera.Id), CancellationToken.None);

            (await result.Result.ExecuteAsync()).StatusCode.Should().Be(StatusCodes.Status200OK);
            pipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
            recording.DidNotReceive().Dispose();
            Directory.GetFiles(folder).Should().ContainSingle("the snapshot is saved as before");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_Recording_That_Gives_No_Picture_Leaves_The_Camera_To_Be_Opened()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        var settingsService = Substitute.For<IApplicationSettingsService>();
        var recordings = Substitute.For<IRecordingService>();
        var recording = Substitute.For<IMediaPipeline>();
        var pipeline = Substitute.For<IMediaPipeline>();
        var camera = new CameraConfiguration();
        storage.GetCameraById(camera.Id).Returns(camera);
        recordings.FindPipeline(camera.Id).Returns(recording);
        recording.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<byte[]?>(new ObjectDisposedException("recording")));
        pipelineFactory.Create(camera).Returns(pipeline);
        var handler = new CaptureSnapshotHandler(storage, pipelineFactory, settingsService, recordings: recordings);

        var executeTask = handler.ExecuteAsync(new CaptureSnapshotParameters(camera.Id), CancellationToken.None);
        pipeline.ConnectionStateChanged += Raise.EventWith(
            new AtcSoft.VideoSurveillance.Events.ConnectionStateChangedEventArgs(
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Connecting,
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Error));
        var result = await executeTask;

        pipelineFactory.Received(1).Create(camera);
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task ExecuteAsync_CaptureReturnsNull_IsServiceUnavailable()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        var settingsService = Substitute.For<IApplicationSettingsService>();
        var pipeline = Substitute.For<IMediaPipeline>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        storage.GetCameraById(cameraId).Returns(camera);
        pipelineFactory.Create(camera).Returns(pipeline);
        pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        var handler = new CaptureSnapshotHandler(storage, pipelineFactory, settingsService);

        // Act — kick off ExecuteAsync, then raise Connected so the handler
        // proceeds past the connection wait and into CaptureFrameAsync.
        var executeTask = handler.ExecuteAsync(
            new CaptureSnapshotParameters(cameraId),
            CancellationToken.None);

        pipeline.ConnectionStateChanged += Raise.EventWith(
            new AtcSoft.VideoSurveillance.Events.ConnectionStateChangedEventArgs(
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Connecting,
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Connected));

        var result = await executeTask;

        // Assert
        pipelineFactory.Received(1).Create(camera);
        await pipeline.Received(1).CaptureFrameAsync(Arg.Any<CancellationToken>());
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);

        // A camera that is still connecting is worth asking again shortly.
        var response = await result.Result.ExecuteAsync();
        response.Headers.RetryAfter.ToString().Should().Be("5");
    }

    [Fact]
    public async Task ExecuteAsync_PipelineEntersErrorState_IsServiceUnavailableWithoutCalling_CaptureFrameAsync()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        var settingsService = Substitute.For<IApplicationSettingsService>();
        var pipeline = Substitute.For<IMediaPipeline>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        storage.GetCameraById(cameraId).Returns(camera);
        pipelineFactory.Create(camera).Returns(pipeline);
        var handler = new CaptureSnapshotHandler(storage, pipelineFactory, settingsService);

        // Act — fire an Error transition; the connection waiter should
        // complete with `false` and the handler should short-circuit before
        // ever calling CaptureFrameAsync.
        var executeTask = handler.ExecuteAsync(
            new CaptureSnapshotParameters(cameraId),
            CancellationToken.None);

        pipeline.ConnectionStateChanged += Raise.EventWith(
            new AtcSoft.VideoSurveillance.Events.ConnectionStateChangedEventArgs(
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Connecting,
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Error));

        var result = await executeTask;

        // Assert
        await pipeline.DidNotReceive().CaptureFrameAsync(Arg.Any<CancellationToken>());
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task A_Snapshot_Of_A_Usb_Camera_Is_Taken_From_Its_Shared_Pipeline()
    {
        // Windows gives a USB camera to one program at a time: opening it
        // here as well would get no picture while it is watched or recorded.
        using var usb = new UsbScenario();
        usb.Pipeline.FramesDecoded.Returns(100);

        var result = await usb.ExecuteAsync();

        result.Result.Should().NotBeOfType<ProblemHttpResult>();
        File.Exists(Directory.EnumerateFiles(usb.SnapshotFolder).Single()).Should().BeTrue();
        usb.PipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
        usb.Host.Received(1).Acquire(usb.Camera, CameraPipelineUse.Snapshot);
        usb.Host.Received(1).Release(usb.Camera.Id, CameraPipelineUse.Snapshot);
        usb.Pipeline.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task A_Usb_Camera_That_Had_To_Be_Opened_For_The_Snapshot_Is_Waited_For()
    {
        using var usb = new UsbScenario();
        usb.Pipeline.FramesDecoded.Returns(0);

        var executing = usb.ExecuteAsync();
        await Task.Delay(150, TestContext.Current.CancellationToken);
        await usb.Pipeline.DidNotReceive().CaptureFrameAsync(Arg.Any<CancellationToken>());

        usb.Pipeline.FramesDecoded.Returns(1);
        var result = await executing;

        result.Result.Should().NotBeOfType<ProblemHttpResult>();
        usb.Host.Received(1).Release(usb.Camera.Id, CameraPipelineUse.Snapshot);
    }

    [Fact]
    public async Task A_Usb_Camera_That_Cannot_Be_Opened_Gives_No_Snapshot_And_Says_Why()
    {
        using var usb = new UsbScenario();
        usb.Host.Acquire(usb.Camera, CameraPipelineUse.Snapshot).Returns((IMediaPipeline?)null);

        var result = await usb.ExecuteAsync();

        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        result.Problem!.Detail.Should().Contain("unplugged");
        usb.PipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
        usb.Host.DidNotReceiveWithAnyArgs().Release(Guid.Empty, default);
    }

    [Fact]
    public async Task A_Usb_Camera_That_Fails_To_Open_Gives_The_Pipeline_Back()
    {
        using var usb = new UsbScenario();
        usb.Pipeline.FramesDecoded.Returns(0);

        var executing = usb.ExecuteAsync();
        usb.Pipeline.ConnectionStateChanged += Raise.EventWith(
            new AtcSoft.VideoSurveillance.Events.ConnectionStateChangedEventArgs(
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Connecting,
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Error));
        var result = await executing;

        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        usb.Host.Received(1).Release(usb.Camera.Id, CameraPipelineUse.Snapshot);
        usb.Pipeline.DidNotReceive().Dispose();
    }

    private sealed class UsbScenario : IDisposable
    {
        public UsbScenario()
        {
            Camera.Display.DisplayName = "Webcam";
            Camera.Connection.Source = CoreCameraSource.Usb;
            Storage.GetCameraById(Camera.Id).Returns(Camera);
            Host.Hosts(Camera).Returns(true);
            Host.Acquire(Camera, CameraPipelineUse.Snapshot).Returns(Pipeline);
            Settings.CameraDisplay.Returns(new CameraDisplayAppSettings { SnapshotPath = SnapshotFolder });
            Pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns(new byte[] { 1, 2, 3 });
        }

        public string SnapshotFolder { get; } = Path.Combine(Path.GetTempPath(), "atcsoft-snapshot-" + Guid.NewGuid().ToString("N"));

        public CameraConfiguration Camera { get; } = new() { Id = Guid.NewGuid() };

        public ICameraStorageService Storage { get; } = Substitute.For<ICameraStorageService>();

        public IMediaPipelineFactory PipelineFactory { get; } = Substitute.For<IMediaPipelineFactory>();

        public IApplicationSettingsService Settings { get; } = Substitute.For<IApplicationSettingsService>();

        public ICameraPipelineHost Host { get; } = Substitute.For<ICameraPipelineHost>();

        public IMediaPipeline Pipeline { get; } = Substitute.For<IMediaPipeline>();

        public Task<global::VideoSurveillance.Generated.Cameras.Results.CaptureSnapshotResult> ExecuteAsync()
            => new CaptureSnapshotHandler(Storage, PipelineFactory, Settings, Host).ExecuteAsync(
                new CaptureSnapshotParameters(Camera.Id),
                TestContext.Current.CancellationToken);

        public void Dispose()
        {
            if (Directory.Exists(SnapshotFolder))
            {
                Directory.Delete(SnapshotFolder, recursive: true);
            }
        }
    }
}