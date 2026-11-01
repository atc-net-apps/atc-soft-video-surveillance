namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

using IMotionDetectionService = AtcSoft.Video.Surveillance.Services.IMotionDetectionService;

/// <summary>
/// Motion is turned into a recording for every camera that records on motion, shown in a tile or
/// not, from the pipeline the camera is watched on.
/// </summary>
public sealed class MotionRecordingCoordinatorTests
{
    private readonly IMotionDetectionService motion = Substitute.For<IMotionDetectionService>();
    private readonly IRecordingService recording = Substitute.For<IRecordingService>();
    private readonly ICameraStorageService storage = Substitute.For<ICameraStorageService>();
    private readonly IApplicationSettingsService settings = Substitute.For<IApplicationSettingsService>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly CameraConfiguration camera = new();

    public MotionRecordingCoordinatorTests()
    {
        camera.Display.DisplayName = "Garden";
        storage.GetCameraById(camera.Id).Returns(camera);
        settings.Recording.Returns(new RecordingSettings { EnableRecordingOnMotion = true });
        motion.FindPipeline(camera.Id).Returns(pipeline);
        recording.TriggerMotionRecording(camera, pipeline).Returns(true);
    }

    [Fact]
    public void Motion_Of_A_Camera_That_Records_On_Motion_Is_Recorded_From_The_Pipeline_It_Is_Watched_On()
    {
        var sut = CreateSut();

        sut.RecordMotion(camera.Id).Should().BeTrue();

        recording.Received(1).TriggerMotionRecording(camera, pipeline);
    }

    [Fact]
    public void Motion_Of_A_Camera_That_Does_Not_Record_On_Motion_Is_Not_Recorded()
    {
        settings.Recording.Returns(new RecordingSettings { EnableRecordingOnMotion = false });
        var sut = CreateSut();

        sut.RecordMotion(camera.Id).Should().BeFalse();

        recording.DidNotReceiveWithAnyArgs().TriggerMotionRecording(default!, default!);
    }

    [Fact]
    public void Motion_Of_A_Camera_That_Is_Not_Watched_Is_Not_Recorded()
    {
        motion.FindPipeline(camera.Id).Returns((IMediaPipeline?)null);
        var sut = CreateSut();

        sut.RecordMotion(camera.Id).Should().BeFalse();
    }

    private MotionRecordingCoordinator CreateSut()
        => new(NullLogger<MotionRecordingCoordinator>.Instance, motion, recording, storage, settings);
}