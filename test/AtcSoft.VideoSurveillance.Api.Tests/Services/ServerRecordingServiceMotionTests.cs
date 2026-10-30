namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// A recording started by motion borrows the pipeline that watches the
/// camera: it has to leave it running when it stops, or the camera would no
/// longer be watched.
/// </summary>
public sealed class ServerRecordingServiceMotionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-server-motion-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingSettings recordingSettings;
    private readonly IApplicationSettingsService settingsService = Substitute.For<IApplicationSettingsService>();
    private readonly ICameraStorageService cameraStorageService = Substitute.For<ICameraStorageService>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly CameraConfiguration camera = new() { Id = Guid.NewGuid() };
    private readonly FakeClock clock = new();
    private readonly List<(RecordingState Old, RecordingState New)> stateChanges = [];
    private readonly List<ConnectionState> connectionStates = [];
    private readonly ServerRecordingService sut;

    public ServerRecordingServiceMotionTests()
    {
        recordingSettings = new RecordingSettings { RecordingPath = root, RecordingFormat = "mkv" };
        recordingSettings.Cleanup.EnableDiskSpaceGuard = false;
        settingsService.Recording.Returns(recordingSettings);
        settingsService.MotionDetection.Returns(new MotionDetectionSettings { CooldownSeconds = 5 });

        camera.Display.DisplayName = "Front Door";
        cameraStorageService.GetCameraById(camera.Id).Returns(camera);

        sut = new ServerRecordingService(
            settingsService,
            cameraStorageService,
            NullLogger<ServerRecordingService>.Instance,
            thumbnailService: null,
            clock);
        sut.RecordingStateChanged += (_, e) => stateChanges.Add((e.OldState, e.NewState));
        sut.CameraConnectionStateChanged += (_, state) => connectionStates.Add(state);
    }

    public void Dispose()
    {
        sut.Dispose();
        pipeline.Dispose();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Motion_Starts_A_Recording_That_Is_Known_As_A_Motion_Recording()
    {
        sut.TriggerMotionRecording(camera, pipeline).Should().BeTrue();

        pipeline.Received(1).StartRecording(Arg.Is<string>(path => path.StartsWith(root, StringComparison.Ordinal)));
        sut.IsRecording(camera.Id).Should().BeTrue();
        sut.GetRecordingState(camera.Id).Should().Be(RecordingState.RecordingMotion);
        sut.GetSession(camera.Id)!.IsManualRecording.Should().BeFalse();
        stateChanges.Should().Equal((RecordingState.Idle, RecordingState.RecordingMotion));
        connectionStates.Should().BeEmpty("the camera was connected already; whoever watches it has said so");
    }

    [Fact]
    public void More_Motion_During_A_Motion_Recording_Does_Not_Start_Another()
    {
        sut.TriggerMotionRecording(camera, pipeline);

        sut.TriggerMotionRecording(camera, pipeline).Should().BeTrue();

        pipeline.ReceivedWithAnyArgs(1).StartRecording(default!);
    }

    [Fact]
    public void Motion_During_A_Recording_Someone_Started_Leaves_That_Recording_Alone()
    {
        sut.StartRecording(camera, pipeline);

        sut.TriggerMotionRecording(camera, pipeline).Should().BeTrue();

        pipeline.ReceivedWithAnyArgs(1).StartRecording(default!);
        sut.GetRecordingState(camera.Id).Should().Be(RecordingState.Recording);
    }

    [Fact]
    public void Stopping_A_Motion_Recording_Leaves_The_Pipeline_Running()
    {
        // The pipeline is the one that watches the camera for motion.
        sut.TriggerMotionRecording(camera, pipeline);
        connectionStates.Clear();

        sut.StopRecording(camera.Id);

        pipeline.Received(1).StopRecording();
        pipeline.DidNotReceive().Dispose();
        sut.IsRecording(camera.Id).Should().BeFalse();
        stateChanges.Should().EndWith((RecordingState.RecordingMotion, RecordingState.Idle));
        connectionStates.Should().NotContain(ConnectionState.Disconnected, "the camera is still connected and watched");
    }

    [Fact]
    public void Stopping_A_Recording_Someone_Started_Still_Closes_Its_Pipeline()
    {
        sut.StartRecording(camera, pipeline);

        sut.StopRecording(camera.Id);

        pipeline.Received(1).Dispose();
        connectionStates.Should().EndWith(ConnectionState.Disconnected);
    }

    [Fact]
    public void After_A_Motion_Recording_The_Next_One_Waits_For_The_Cooldown()
    {
        sut.TriggerMotionRecording(camera, pipeline);
        sut.StopRecording(camera.Id);

        clock.Advance(TimeSpan.FromSeconds(4));
        sut.TriggerMotionRecording(camera, pipeline).Should().BeFalse("the cooldown is five seconds");

        clock.Advance(TimeSpan.FromSeconds(1));
        sut.TriggerMotionRecording(camera, pipeline).Should().BeTrue();
    }

    [Fact]
    public void The_Cooldown_Is_The_Cameras_Own_When_It_Has_One()
    {
        camera.Overrides = new CameraOverrides();
        camera.Overrides.MotionDetection.CooldownSeconds = 0;
        sut.TriggerMotionRecording(camera, pipeline);
        sut.StopRecording(camera.Id);

        sut.TriggerMotionRecording(camera, pipeline).Should().BeTrue();
    }

    [Fact]
    public void A_Recording_Someone_Started_Is_Not_Held_Back_By_The_Cooldown()
    {
        sut.TriggerMotionRecording(camera, pipeline);
        sut.StopRecording(camera.Id);

        sut.StartRecording(camera, pipeline).Should().BeTrue();
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}