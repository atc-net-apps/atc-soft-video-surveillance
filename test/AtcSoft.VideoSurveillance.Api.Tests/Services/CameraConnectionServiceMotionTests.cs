namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// A camera that is recorded on motion is kept open and watched; motion
/// starts a recording on the same pipeline, and its end stops it again.
/// </summary>
public sealed class CameraConnectionServiceMotionTests : IDisposable
{
    private readonly CameraConfiguration camera = new() { Id = Guid.NewGuid() };
    private readonly ICameraStorageService storage = Substitute.For<ICameraStorageService>();
    private readonly IApplicationSettingsService settings = Substitute.For<IApplicationSettingsService>();
    private readonly IRecordingService recordingService = Substitute.For<IRecordingService>();
    private readonly IMediaPipelineFactory pipelineFactory = Substitute.For<IMediaPipelineFactory>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly IMotionDetectionService motionDetection = Substitute.For<IMotionDetectionService>();
    private readonly ICameraConnectionStateSink connectionStates = Substitute.For<ICameraConnectionStateSink>();
    private readonly RecordingSettings recordingSettings = new() { EnableRecordingOnMotion = true };
    private readonly CameraConnectionService sut;

    public CameraConnectionServiceMotionTests()
    {
        camera.Display.DisplayName = "Front Door";
        camera.Connection.IpAddress = "10.0.0.1";
        storage.GetAllCameras().Returns([camera]);
        storage.GetCameraById(camera.Id).Returns(camera);
        settings.Recording.Returns(recordingSettings);
        settings.MotionDetection.Returns(new MotionDetectionSettings { Sensitivity = 42 });
        pipelineFactory.Create(camera).Returns(pipeline);

        sut = new CameraConnectionService(
            NullLogger<CameraConnectionService>.Instance,
            new CameraConnectionServiceOptions(),
            Substitute.For<IBackgroundServiceHealthService>(),
            storage,
            settings,
            recordingService,
            pipelineFactory,
            Substitute.For<IUsbCameraLifecycleCoordinator>(),
            motionDetection,
            connectionTypeAutoDetector: null,
            connectionStates);
    }

    public void Dispose()
    {
        sut.Dispose();
        pipeline.Dispose();
    }

    [Fact]
    public async Task A_Camera_Recorded_On_Motion_Is_Opened_And_Watched_Without_Recording()
    {
        await ConnectAsync();

        pipelineFactory.Received(1).Create(camera);
        motionDetection.Received(1).StartDetection(
            camera.Id,
            pipeline,
            Arg.Is<MotionDetectionSettings>(s => s.Sensitivity == 42),
            camera.MotionZones);
        recordingService.DidNotReceiveWithAnyArgs().StartRecording(default!, default!);
        connectionStates.Received(1).Report(camera.Id, ConnectionState.Connected);
    }

    [Fact]
    public async Task A_Camera_That_Wants_Neither_Recording_Is_Not_Opened()
    {
        recordingSettings.EnableRecordingOnMotion = false;

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        pipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task A_Watched_Camera_That_Is_Not_Recording_Is_Not_Taken_For_Dead()
    {
        // "Not recording" is what a recorded-on-connect camera's dead
        // pipeline looks like. For a watched camera it is the normal state.
        await ConnectAsync();
        pipeline.IsRecordingActive.Returns(false);
        motionDetection.IsDetectionActive(camera.Id).Returns(true);

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        pipelineFactory.Received(1).Create(camera);
        pipeline.DidNotReceive().Dispose();
        motionDetection.DidNotReceive().StopDetection(camera.Id);
    }

    [Fact]
    public async Task Motion_Starts_A_Recording_On_The_Pipeline_That_Watches()
    {
        await ConnectAsync();

        RaiseMotion(active: true);

        recordingService.Received(1).TriggerMotionRecording(camera, pipeline);
    }

    [Fact]
    public async Task The_End_Of_The_Motion_Stops_The_Motion_Recording()
    {
        await ConnectAsync();
        RaiseMotion(active: true);
        recordingService.GetSession(camera.Id).Returns(new RecordingSession(camera.Id, "Front Door", "a.mkv", isManualRecording: false));

        RaiseMotion(active: false);

        recordingService.Received(1).StopRecording(camera.Id);
    }

    [Fact]
    public async Task The_End_Of_The_Motion_Leaves_A_Recording_Someone_Started_Running()
    {
        await ConnectAsync();
        recordingService.GetSession(camera.Id).Returns(new RecordingSession(camera.Id, "Front Door", "a.mkv", isManualRecording: true));

        RaiseMotion(active: false);

        recordingService.DidNotReceive().StopRecording(camera.Id);
    }

    [Fact]
    public async Task A_Camera_Recorded_All_The_Time_Is_Watched_Too_But_Motion_Starts_Nothing()
    {
        // It is already recording. Watching it is what makes the motion
        // events and webhooks.
        recordingSettings.EnableRecordingOnConnect = true;
        recordingService.StartRecording(camera, pipeline).Returns(true);

        await ConnectAsync();
        RaiseMotion(active: true);

        recordingService.Received(1).StartRecording(camera, pipeline);
        motionDetection.Received(1).StartDetection(camera.Id, pipeline, Arg.Any<MotionDetectionSettings>(), camera.MotionZones);
        recordingService.DidNotReceiveWithAnyArgs().TriggerMotionRecording(default!, default!);
    }

    [Fact]
    public async Task A_Camera_Recorded_All_The_Time_Without_Motion_Recording_Is_Not_Watched()
    {
        recordingSettings.EnableRecordingOnConnect = true;
        recordingSettings.EnableRecordingOnMotion = false;
        recordingService.StartRecording(camera, pipeline).Returns(true);

        await ConnectAsync();

        motionDetection.DidNotReceiveWithAnyArgs().StartDetection(Guid.Empty, default!);
    }

    [Fact]
    public async Task Turning_Motion_Recording_Off_Stops_Watching_And_Closes_The_Camera()
    {
        await ConnectAsync();
        motionDetection.IsDetectionActive(camera.Id).Returns(true);

        recordingSettings.EnableRecordingOnMotion = false;
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        motionDetection.Received().StopDetection(camera.Id);
        await WaitForAsync(() => pipeline.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IDisposable.Dispose)));
        connectionStates.Received(1).Report(camera.Id, ConnectionState.Disconnected);
    }

    [Fact]
    public async Task A_Watched_Camera_That_Sends_Nothing_Any_More_Is_Closed_And_Opened_Again()
    {
        await ConnectAsync();
        pipeline.LastPacketUtc.Returns(DateTime.UtcNow.AddMinutes(-2));

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        motionDetection.Received().StopDetection(camera.Id);
        await WaitForAsync(() => pipeline.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IDisposable.Dispose)));

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);
        pipelineFactory.Received(2).Create(camera);
    }

    [Fact]
    public async Task A_Camera_That_Fails_Stops_Being_Watched_Before_Its_Pipeline_Is_Closed()
    {
        await ConnectAsync();
        recordingService.GetSession(camera.Id).Returns(new RecordingSession(camera.Id, "Front Door", "a.mkv", isManualRecording: false));

        pipeline.ConnectionStateChanged += Raise.EventWith(new ConnectionStateChangedEventArgs(ConnectionState.Connected, ConnectionState.Error));

        motionDetection.Received().StopDetection(camera.Id);
        recordingService.Received(1).StopRecording(camera.Id);
        connectionStates.Received(1).Report(camera.Id, ConnectionState.Disconnected);
    }

    private async Task ConnectAsync()
    {
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);
        pipeline.ConnectionStateChanged += Raise.EventWith(new ConnectionStateChangedEventArgs(ConnectionState.Connecting, ConnectionState.Connected));
    }

    private void RaiseMotion(bool active)
        => motionDetection.MotionDetected += Raise.EventWith(new MotionDetectedEventArgs(camera.Id, 5, active));

    // Pipelines are closed on a thread-pool thread, never on the one that
    // reports the camera's state.
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The pipeline was not closed.");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }
}