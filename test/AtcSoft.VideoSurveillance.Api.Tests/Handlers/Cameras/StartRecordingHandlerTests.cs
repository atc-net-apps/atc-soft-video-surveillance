namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

public class StartRecordingHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_CameraNotFound_ReturnsNotFound()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var recordingService = Substitute.For<IRecordingService>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        var cameraId = Guid.NewGuid();
        storage.GetCameraById(cameraId).Returns((CameraConfiguration?)null);
        var handler = new StartRecordingHandler(storage, recordingService, pipelineFactory);

        // Act
        var result = await handler.ExecuteAsync(
            new StartRecordingParameters(cameraId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task A_Usb_Camera_Is_Recorded_On_Its_Shared_Pipeline_Also_While_It_Is_Watched_Live()
    {
        // Windows gives a USB camera to one program at a time, so the
        // camera is never opened here: the recording uses the pipeline the
        // host keeps, which a viewer may be watching on already.
        var usb = new UsbScenario();
        usb.Pipeline.FramesDecoded.Returns(100);
        usb.RecordingService.StartRecordingOnSharedPipeline(usb.Camera, usb.Pipeline).Returns(true);
        usb.RecordingService.GetRecordingState(usb.Camera.Id).Returns(RecordingState.Recording);

        var result = await usb.ExecuteAsync();

        result.Result.Should().BeOfType<Ok<RecordingStatus>>();
        usb.Host.Received(1).Acquire(usb.Camera, CameraPipelineUse.Recording);
        usb.PipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
        usb.RecordingService.DidNotReceiveWithAnyArgs().StartRecording(default!, default!);
        usb.Pipeline.DidNotReceive().Dispose();
        usb.Host.DidNotReceiveWithAnyArgs().Release(Guid.Empty, default);
    }

    [Fact]
    public async Task A_Usb_Camera_That_Was_Opened_For_The_Recording_Is_Recorded_Once_It_Delivers_Pictures()
    {
        var usb = new UsbScenario();
        usb.Pipeline.FramesDecoded.Returns(0);
        usb.RecordingService.StartRecordingOnSharedPipeline(usb.Camera, usb.Pipeline).Returns(true);

        var executing = usb.ExecuteAsync();
        await Task.Delay(150, TestContext.Current.CancellationToken);
        usb.RecordingService.DidNotReceiveWithAnyArgs().StartRecordingOnSharedPipeline(default!, default!);

        usb.Pipeline.FramesDecoded.Returns(1);
        var result = await executing;

        result.Result.Should().BeOfType<Ok<RecordingStatus>>();
    }

    [Fact]
    public async Task A_Usb_Camera_That_Cannot_Be_Opened_Is_Refused_With_The_Reason()
    {
        var usb = new UsbScenario();
        usb.Host.Acquire(usb.Camera, CameraPipelineUse.Recording).Returns((IMediaPipeline?)null);

        var result = await usb.ExecuteAsync();

        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        result.Problem!.Detail.Should().Contain("unplugged");
        usb.PipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task A_Usb_Camera_That_Fails_To_Open_Gives_The_Pipeline_Back_Without_Closing_It()
    {
        // Someone may be waiting for the same pipeline to watch the camera.
        var usb = new UsbScenario();
        usb.Pipeline.FramesDecoded.Returns(0);

        var executing = usb.ExecuteAsync();
        usb.Pipeline.ConnectionStateChanged += Raise.EventWith(
            new AtcSoft.VideoSurveillance.Events.ConnectionStateChangedEventArgs(
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Connecting,
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Error));
        var result = await executing;

        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        usb.Host.Received(1).Release(usb.Camera.Id, CameraPipelineUse.Recording);
        usb.Pipeline.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task A_Usb_Camera_Whose_Recording_Could_Not_Start_Gives_The_Pipeline_Back()
    {
        var usb = new UsbScenario();
        usb.Pipeline.FramesDecoded.Returns(100);
        usb.RecordingService.StartRecordingOnSharedPipeline(usb.Camera, usb.Pipeline).Returns(false);

        var result = await usb.ExecuteAsync();

        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        usb.Host.Received(1).Release(usb.Camera.Id, CameraPipelineUse.Recording);
        usb.Pipeline.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task A_Network_Camera_Is_Opened_For_Its_Recording_As_Before()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var recordingService = Substitute.For<IRecordingService>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        var host = Substitute.For<ICameraPipelineHost>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        storage.GetCameraById(cameraId).Returns(camera);
        pipelineFactory.Create(camera).Returns(_ => throw new InvalidOperationException("opened"));
        var handler = new StartRecordingHandler(storage, recordingService, pipelineFactory, host);

        var act = () => handler.ExecuteAsync(
            new StartRecordingParameters(cameraId),
            TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("opened");
        host.DidNotReceiveWithAnyArgs().Acquire(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyRecording_ReturnsConflict()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var recordingService = Substitute.For<IRecordingService>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        storage.GetCameraById(cameraId).Returns(camera);
        recordingService.IsRecording(cameraId).Returns(true);
        var handler = new StartRecordingHandler(storage, recordingService, pipelineFactory);

        // Act
        var result = await handler.ExecuteAsync(
            new StartRecordingParameters(cameraId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ExecuteAsync_CameraExistsNotRecording_CreatesPipelineAndStartsRecording()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var recordingService = Substitute.For<IRecordingService>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        var pipeline = Substitute.For<IMediaPipeline>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        storage.GetCameraById(cameraId).Returns(camera);
        recordingService.IsRecording(cameraId).Returns(false);
        pipelineFactory.Create(camera).Returns(pipeline);
        recordingService.StartRecording(camera, pipeline).Returns(true);
        recordingService.GetRecordingState(cameraId).Returns(RecordingState.Recording);
        recordingService.GetSession(cameraId).Returns((RecordingSession?)null);
        var handler = new StartRecordingHandler(storage, recordingService, pipelineFactory);

        // Act — kick off ExecuteAsync; it will subscribe to
        // pipeline.ConnectionStateChanged and yield at the await on the
        // TaskCompletionSource. We then raise the Connected event so it
        // can complete and proceed to call StartRecording.
        var executeTask = handler.ExecuteAsync(
            new StartRecordingParameters(cameraId),
            CancellationToken.None);

        pipeline.ConnectionStateChanged += Raise.EventWith(
            new AtcSoft.VideoSurveillance.Events.ConnectionStateChangedEventArgs(
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Connecting,
                AtcSoft.VideoSurveillance.Enums.ConnectionState.Connected));

        var result = await executeTask;

        // Assert
        pipelineFactory.Received(1).Create(camera);
        recordingService.Received(1).StartRecording(camera, pipeline);
        var okResult = result.Result.Should().BeOfType<Ok<RecordingStatus>>().Subject;
        okResult.Value.Should().NotBeNull();
        okResult.Value!.CameraId.Should().Be(cameraId);
        okResult.Value.State.Should().Be(RecordingStatusState.Recording);
    }

    private sealed class UsbScenario
    {
        public UsbScenario()
        {
            Camera.Connection.Source = CoreCameraSource.Usb;
            Storage.GetCameraById(Camera.Id).Returns(Camera);
            Host.Hosts(Camera).Returns(true);
            Host.Acquire(Camera, CameraPipelineUse.Recording).Returns(Pipeline);
        }

        public CameraConfiguration Camera { get; } = new() { Id = Guid.NewGuid() };

        public ICameraStorageService Storage { get; } = Substitute.For<ICameraStorageService>();

        public IRecordingService RecordingService { get; } = Substitute.For<IRecordingService>();

        public IMediaPipelineFactory PipelineFactory { get; } = Substitute.For<IMediaPipelineFactory>();

        public ICameraPipelineHost Host { get; } = Substitute.For<ICameraPipelineHost>();

        public IMediaPipeline Pipeline { get; } = Substitute.For<IMediaPipeline>();

        public Task<global::VideoSurveillance.Generated.Cameras.Results.StartRecordingResult> ExecuteAsync()
            => new StartRecordingHandler(Storage, RecordingService, PipelineFactory, Host).ExecuteAsync(
                new StartRecordingParameters(Camera.Id),
                TestContext.Current.CancellationToken);
    }
}