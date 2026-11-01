namespace AtcSoft.Video.Surveillance.Api.Services;

public sealed class CameraConnectionServiceTests
{
    [Fact]
    public async Task A_Rotation_Change_Reaches_A_Running_Pipeline_On_The_Next_Pass()
    {
        // Arrange — a camera that records on connect, so the service keeps a pipeline for it
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };
        camera.Display.DisplayName = "Cam";
        camera.Connection.IpAddress = "10.0.0.1";

        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns([camera]);

        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(new RecordingSettings { EnableRecordingOnConnect = true });

        var pipeline = Substitute.For<IMediaPipeline>();
        pipeline.IsRecordingActive.Returns(true);
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        pipelineFactory.Create(camera).Returns(pipeline);

        var recordingService = Substitute.For<IRecordingService>();

        using var sut = new CameraConnectionService(
            NullLogger<CameraConnectionService>.Instance,
            new CameraConnectionServiceOptions(),
            Substitute.For<IBackgroundServiceHealthService>(),
            storage,
            settings,
            recordingService,
            pipelineFactory,
            Substitute.For<IUsbCameraLifecycleCoordinator>(),
            Substitute.For<IMotionDetectionService>());

        await sut.DoWorkAsync(CancellationToken.None);
        recordingService.IsRecording(camera.Id).Returns(true);
        pipeline.ClearReceivedCalls();

        // Act — the camera is turned through the API while it is recording
        camera.Display.Rotation = CoreCameraRotation.Rotate90;
        await sut.DoWorkAsync(CancellationToken.None);

        // Assert
        pipeline.Received(1).SetRotation(CoreCameraRotation.Rotate90);
        pipelineFactory.Received(1).Create(camera);
    }

    [Fact]
    public async Task A_Deleted_Camera_Has_Its_Recording_Stopped_And_Its_Pipeline_Closed()
    {
        // Arrange — a camera recorded on connect, with its pipeline open
        var (camera, storage, pipeline, recordingService, motion, sut) = CreateRecordedCamera();
        using var _ = sut;
        await sut.DoWorkAsync(CancellationToken.None);
        sut.FindPipeline(camera.Id).Should().BeSameAs(pipeline);

        // Act
        storage.GetAllCameras().Returns([]);
        sut.Forget(camera.Id);

        // Assert
        recordingService.Received(1).StopRecording(camera.Id);
        motion.Received().StopDetection(camera.Id);
        sut.FindPipeline(camera.Id).Should().BeNull();
        await WaitForAsync(() => pipeline.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IDisposable.Dispose)));
    }

    [Fact]
    public async Task A_Recording_Of_A_Camera_That_Is_Gone_Is_Stopped_By_The_Next_Pass()
    {
        // Arrange — a recording runs for a camera the storage no longer has
        var (camera, storage, _, recordingService, _, sut) = CreateRecordedCamera();
        using var _ = sut;
        await sut.DoWorkAsync(CancellationToken.None);
        storage.GetAllCameras().Returns([]);
        recordingService.GetActiveSessions().Returns([new RecordingSession(camera.Id, "Cam", "c:/r/a.mp4", isManualRecording: true)]);

        // Act
        await sut.DoWorkAsync(CancellationToken.None);

        // Assert
        recordingService.Received(1).StopRecording(camera.Id);
        sut.FindPipeline(camera.Id).Should().BeNull();
    }

    private static (CameraConfiguration Camera, ICameraStorageService Storage, IMediaPipeline Pipeline, IRecordingService Recordings, IMotionDetectionService Motion, CameraConnectionService Sut) CreateRecordedCamera()
    {
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };
        camera.Display.DisplayName = "Cam";
        camera.Connection.IpAddress = "10.0.0.1";

        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns([camera]);

        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(new RecordingSettings { EnableRecordingOnConnect = true });

        var pipeline = Substitute.For<IMediaPipeline>();
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        pipelineFactory.Create(camera).Returns(pipeline);

        var recordingService = Substitute.For<IRecordingService>();
        recordingService.GetActiveSessions().Returns([]);
        var motion = Substitute.For<IMotionDetectionService>();

        var sut = new CameraConnectionService(
            NullLogger<CameraConnectionService>.Instance,
            new CameraConnectionServiceOptions(),
            Substitute.For<IBackgroundServiceHealthService>(),
            storage,
            settings,
            recordingService,
            pipelineFactory,
            Substitute.For<IUsbCameraLifecycleCoordinator>(),
            motion);

        return (camera, storage, pipeline, recordingService, motion, sut);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        condition().Should().BeTrue();
    }
}