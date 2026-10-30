namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// A camera that can only be opened once (a USB camera) has one pipeline
/// for everything. A recording someone starts on it uses that pipeline and
/// leaves it open when it stops: someone may be watching the camera.
/// </summary>
public sealed class ServerRecordingServiceSharedPipelineTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-server-shared-" + Guid.NewGuid().ToString("N"));
    private readonly IApplicationSettingsService settingsService = Substitute.For<IApplicationSettingsService>();
    private readonly ICameraStorageService cameraStorageService = Substitute.For<ICameraStorageService>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly CameraConfiguration camera = new() { Id = Guid.NewGuid() };
    private readonly List<(RecordingState Old, RecordingState New)> stateChanges = [];
    private readonly List<ConnectionState> connectionStates = [];
    private readonly ServerRecordingService sut;

    public ServerRecordingServiceSharedPipelineTests()
    {
        var recordingSettings = new RecordingSettings { RecordingPath = root, RecordingFormat = "mkv" };
        recordingSettings.Cleanup.EnableDiskSpaceGuard = false;
        settingsService.Recording.Returns(recordingSettings);
        settingsService.MotionDetection.Returns(new MotionDetectionSettings { CooldownSeconds = 5 });

        camera.Display.DisplayName = "Webcam";
        cameraStorageService.GetCameraById(camera.Id).Returns(camera);

        sut = new ServerRecordingService(
            settingsService,
            cameraStorageService,
            NullLogger<ServerRecordingService>.Instance);
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
    public void A_Recording_On_A_Shared_Pipeline_Is_A_Recording_Someone_Started()
    {
        IRecordingService service = sut;

        service.StartRecordingOnSharedPipeline(camera, pipeline).Should().BeTrue();

        pipeline.Received(1).StartRecording(Arg.Is<string>(path => path.StartsWith(root, StringComparison.Ordinal)));
        sut.GetRecordingState(camera.Id).Should().Be(RecordingState.Recording);
        sut.GetSession(camera.Id)!.IsManualRecording.Should().BeTrue("the end of a motion must not stop it");
        stateChanges.Should().Equal((RecordingState.Idle, RecordingState.Recording));
        connectionStates.Should().BeEmpty("the pipeline's owner reports the camera's connection");
    }

    [Fact]
    public void Stopping_It_Leaves_The_Shared_Pipeline_Open()
    {
        IRecordingService service = sut;
        service.StartRecordingOnSharedPipeline(camera, pipeline);

        sut.StopRecording(camera.Id);

        pipeline.Received(1).StopRecording();
        pipeline.DidNotReceive().Dispose();
        sut.IsRecording(camera.Id).Should().BeFalse();
        connectionStates.Should().BeEmpty("the camera is still connected");
        stateChanges.Should().EndWith((RecordingState.Recording, RecordingState.Idle));
    }

    [Fact]
    public void A_Camera_That_Is_Recording_Already_Is_Not_Recorded_Twice()
    {
        IRecordingService service = sut;
        service.StartRecordingOnSharedPipeline(camera, pipeline);

        service.StartRecordingOnSharedPipeline(camera, pipeline).Should().BeFalse();

        pipeline.ReceivedWithAnyArgs(1).StartRecording(default!);
        pipeline.DidNotReceive().Dispose();
    }

    [Fact]
    public void A_Recording_Service_Without_Shared_Pipelines_Does_Not_Record_On_One()
    {
        // The desktop apps' recording services own every pipeline they
        // record on. Asked for a shared one they say no, instead of closing
        // someone else's pipeline when the recording stops.
        var other = Substitute.ForPartsOf<RecordingServiceWithoutSharedPipelines>();

        ((IRecordingService)other).StartRecordingOnSharedPipeline(camera, pipeline).Should().BeFalse();
    }
}