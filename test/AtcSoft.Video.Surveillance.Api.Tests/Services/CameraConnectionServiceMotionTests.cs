namespace AtcSoft.Video.Surveillance.Api.Services;

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
    public async Task A_Watched_Camera_That_Drops_While_Playing_Is_Opened_Again_Within_Seconds()
    {
        // Before, it waited for a pass after the back-off: 30 s to 2.5 minutes blind.
        await ConnectAsync();

        pipeline.ConnectionStateChanged += Raise.EventWith(new ConnectionStateChangedEventArgs(ConnectionState.Connected, ConnectionState.Error));

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (pipelineFactory.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IMediaPipelineFactory.Create)) < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        pipelineFactory.Received(2).Create(camera);
    }

    [Fact]
    public async Task A_Camera_That_Never_Played_Waits_For_Its_Back_Off()
    {
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        pipeline.ConnectionStateChanged += Raise.EventWith(new ConnectionStateChangedEventArgs(ConnectionState.Connecting, ConnectionState.Error));
        await Task.Delay(TimeSpan.FromSeconds(7), TestContext.Current.CancellationToken);

        pipelineFactory.Received(1).Create(camera);
    }

    [Fact]
    public async Task A_Watched_Camera_Whose_Pictures_Stopped_While_Packets_Arrive_Is_Opened_Again()
    {
        // A decoder that froze: packets keep coming, the picture does not change, and the
        // detector compared the same picture with itself for good.
        await ConnectAsync();
        motionDetection.IsDetectionActive(camera.Id).Returns(true);
        pipeline.LastPacketUtc.Returns(_ => DateTime.UtcNow);
        pipeline.FramesDecoded.Returns(500);

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        motionDetection.Received().StopDetection(camera.Id);
        await WaitForAsync(() => pipeline.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IDisposable.Dispose)));
    }

    [Fact]
    public async Task A_Watched_Camera_Whose_Pictures_Go_On_Is_Left_Alone()
    {
        await ConnectAsync();
        motionDetection.IsDetectionActive(camera.Id).Returns(true);
        pipeline.LastPacketUtc.Returns(_ => DateTime.UtcNow);
        var frames = 500L;
        pipeline.FramesDecoded.Returns(_ => frames += 120);

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        motionDetection.DidNotReceive().StopDetection(camera.Id);
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

    [Fact]
    public async Task A_Motion_Refused_By_The_Cooldown_Is_Recorded_When_It_Ends_Though_Nothing_Moves_Any_More()
    {
        // Before, only a picture that moved tried again: a motion that began in the cooldown after
        // the last motion recording and then stood still was never recorded.
        await ConnectAsync();
        recordingService.TriggerMotionRecording(camera, pipeline).Returns(false, true);

        RaiseMotion(active: true);
        RaiseStillPicture();
        RaiseStillPicture();

        recordingService.Received(2).TriggerMotionRecording(camera, pipeline);
    }

    [Fact]
    public async Task A_Motion_That_Ended_Is_Not_Tried_Again()
    {
        await ConnectAsync();
        recordingService.TriggerMotionRecording(camera, pipeline).Returns(false);

        RaiseMotion(active: true);
        RaiseMotion(active: false);
        RaiseStillPicture();

        recordingService.Received(1).TriggerMotionRecording(camera, pipeline);
    }

    [Fact]
    public async Task A_Viewer_Of_A_Watched_Network_Camera_Shares_Its_Connection()
    {
        // Before, live view opened a second connection with the FFmpeg tool, which cameras that take
        // one or two viewers refuse, and which doubles what the camera sends.
        await ConnectAsync();
        pipeline.FramesDecoded.Returns(100);
        pipeline.LastPacketUtc.Returns(_ => DateTime.UtcNow);

        sut.Acquire(camera, CameraPipelineUse.Viewing).Should().BeSameAs(pipeline);

        sut.Release(camera.Id, CameraPipelineUse.Viewing);
        pipeline.DidNotReceive().Dispose();
        motionDetection.DidNotReceive().StopDetection(camera.Id);
    }

    [Fact]
    public void A_Network_Camera_The_Server_Has_Not_Open_Is_Not_Opened_For_A_Viewer()
    {
        sut.Acquire(camera, CameraPipelineUse.Viewing).Should().BeNull("the viewer gets its own stream from the FFmpeg tool");
        pipelineFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public void A_Motion_End_Raised_After_The_Pipeline_Was_Removed_Still_Ends_The_Motion_Recording()
    {
        // No pipeline is kept for the camera: it was just removed, and stopping
        // the detection raises the end afterwards.
        recordingService.GetSession(camera.Id).Returns(new RecordingSession(camera.Id, "Front Door", "a.mkv", isManualRecording: false));

        RaiseMotion(active: false);

        recordingService.Received(1).StopRecording(camera.Id);
    }

    private async Task ConnectAsync()
    {
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);
        pipeline.ConnectionStateChanged += Raise.EventWith(new ConnectionStateChangedEventArgs(ConnectionState.Connecting, ConnectionState.Connected));
        await SettleStartAsync();
    }

    // A recording on connect is started on the thread pool, not on the
    // pipeline's own thread: wait until it was asked for, or a moment passed.
    private async Task SettleStartAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline &&
               !recordingService.ReceivedCalls().Any(c => c.GetMethodInfo().Name.StartsWith("StartRecording", StringComparison.Ordinal)))
        {
            await Task.Delay(10);
        }

        await Task.Delay(50);
    }

    private void RaiseMotion(bool active)
        => motionDetection.MotionDetected += Raise.EventWith(new MotionDetectedEventArgs(camera.Id, 5, active));

    private void RaiseStillPicture()
        => motionDetection.PictureAnalyzed += Raise.EventWith(new PictureAnalyzedEventArgs(camera.Id, DateTimeOffset.UtcNow, new MotionDetectorResult(false, MotionHoldChange.None, 0, [])));

    // Pipelines are closed on a thread-pool thread, never on the one that
    // reports the camera's state.
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
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