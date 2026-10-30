namespace AtcSoft.VideoSurveillance.Api.Services;

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
}