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

    [Fact]
    public async Task Turning_Record_On_Connect_Off_Stops_The_Recording_The_Server_Started()
    {
        // Arrange — a camera recorded on connect, recording on the server's pipeline
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };
        camera.Display.DisplayName = "Cam";
        camera.Connection.IpAddress = "10.0.0.1";
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns([camera]);
        var recordingSettings = new RecordingSettings { EnableRecordingOnConnect = true };
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(recordingSettings);
        settings.MotionDetection.Returns(new MotionDetectionSettings());
        var pipeline = Substitute.For<IMediaPipeline>();
        pipeline.FramesDecoded.Returns(10);
        pipeline.IsRecordingActive.Returns(true);
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        pipelineFactory.Create(camera).Returns(pipeline);
        var recordingService = Substitute.For<IRecordingService>();
        recordingService.GetActiveSessions().Returns([]);
        recordingService.StartRecording(camera, pipeline).Returns(true);

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
        await sut.DoWorkAsync(CancellationToken.None);
        recordingService.Received(1).StartRecording(camera, pipeline);
        recordingService.IsRecording(camera.Id).Returns(true);

        // Act
        recordingSettings.EnableRecordingOnConnect = false;
        await sut.DoWorkAsync(CancellationToken.None);

        // Assert
        recordingService.Received(1).StopRecording(camera.Id);
        sut.FindPipeline(camera.Id).Should().BeNull();
    }

    [Fact]
    public async Task A_Recording_Someone_Started_Is_Opened_Again_After_It_Dropped_Without_Record_On_Connect()
    {
        // Arrange — record-on-connect is off, but someone asked for the camera to be recorded
        var (camera, _, pipeline, recordingService, _, _) = CreateRecordedCamera();
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns([camera]);
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(new RecordingSettings { EnableRecordingOnConnect = false });
        settings.MotionDetection.Returns(new MotionDetectionSettings());
        var pipelineFactory = Substitute.For<IMediaPipelineFactory>();
        pipelineFactory.Create(camera).Returns(pipeline);
        var intents = Substitute.For<IRecordingIntents>();
        intents.IsWanted(camera.Id).Returns(true);

        using var sut = new CameraConnectionService(
            NullLogger<CameraConnectionService>.Instance,
            new CameraConnectionServiceOptions(),
            Substitute.For<IBackgroundServiceHealthService>(),
            storage,
            settings,
            recordingService,
            pipelineFactory,
            Substitute.For<IUsbCameraLifecycleCoordinator>(),
            Substitute.For<IMotionDetectionService>(),
            recordingIntents: intents);

        // Act — the recording is gone (it dropped, or the server restarted)
        await sut.DoWorkAsync(CancellationToken.None);

        // Assert — the camera is opened to be recorded again
        pipelineFactory.Received(1).Create(camera);
        sut.FindPipeline(camera.Id).Should().BeSameAs(pipeline);
    }

    [Fact]
    public async Task A_Recording_That_Could_Write_Nothing_Is_Not_Started_Again_At_Once()
    {
        // Arrange — a camera recorded on connect, whose recording then fails to write
        var (camera, _, pipeline, recordingService, _, sut) = CreateRecordedCamera();
        using var _ = sut;
        pipeline.FramesDecoded.Returns(10);
        pipeline.IsRecordingActive.Returns(true);
        recordingService.StartRecording(camera, pipeline).Returns(true);
        await sut.DoWorkAsync(CancellationToken.None);
        await sut.DoWorkAsync(CancellationToken.None);
        recordingService.Received(1).StartRecording(camera, pipeline);

        pipeline.RecordingError.Returns("No space left on device");
        pipeline.IsRecordingActive.Returns(false);

        // Act — the pass that finds it dead, and the next one
        await sut.DoWorkAsync(CancellationToken.None);
        await sut.DoWorkAsync(CancellationToken.None);

        // Assert — it waits: no new pipeline, no new recording
        recordingService.Received(1).StartRecording(Arg.Any<CameraConfiguration>(), Arg.Any<IMediaPipeline>());
        sut.FindPipeline(camera.Id).Should().BeNull();
    }

    [Fact]
    public async Task A_Recorded_Camera_That_Keeps_Failing_Is_Tried_Again_Within_Two_Minutes()
    {
        var (camera, storage, pipeline, _, _, sut) = CreateRecordedCamera();
        using var _ = sut;
        storage.GetCameraById(camera.Id).Returns(camera);
        await sut.DoWorkAsync(CancellationToken.None);

        for (var i = 0; i < 10; i++)
        {
            pipeline.ConnectionStateChanged += Raise.EventWith(new ConnectionStateChangedEventArgs(ConnectionState.Connecting, ConnectionState.Error));
            if (sut.FindPipeline(camera.Id) is null)
            {
                break;
            }
        }

        sut.GetNextAttemptUtc(camera.Id).Should().NotBeNull();
        sut.GetNextAttemptUtc(camera.Id)!.Value.Should().BeOnOrBefore(DateTime.UtcNow.AddMinutes(2).AddSeconds(5));
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