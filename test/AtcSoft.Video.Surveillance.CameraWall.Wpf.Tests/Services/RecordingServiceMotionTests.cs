namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// A recording started on motion ends once motion has stayed away for the post-motion time, whichever
/// thread started it. Its timer was a DispatcherTimer, which ticks only on the UI thread: started from
/// any other thread, the recording never ended.
/// </summary>
public sealed class RecordingServiceMotionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-cw-motion-" + Guid.NewGuid().ToString("N"));
    private readonly List<CameraConfiguration> cameras = [];

    public RecordingServiceMotionTests()
        => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_Motion_Recording_Started_Off_The_UI_Thread_Ends_After_The_Post_Motion_Time()
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = "Garden";
        cameras.Add(camera);
        var pipeline = Substitute.For<IMediaPipeline>();
        using var sut = CreateSut(postMotionSeconds: 1);

        var started = await Task.Run(() => sut.TriggerMotionRecording(camera, pipeline), TestContext.Current.CancellationToken);
        started.Should().BeTrue();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sut.IsRecording(camera.Id) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        sut.IsRecording(camera.Id).Should().BeFalse("motion stayed away for longer than the post-motion time");
        pipeline.Received().StopRecording();
    }

    private RecordingService CreateSut(int postMotionSeconds)
    {
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(new RecordingSettings
        {
            RecordingPath = Path.Combine(root, "recordings"),
            Cleanup = new MediaCleanupSettings { EnableDiskSpaceGuard = false },
        });
        settings.MotionDetection.Returns(new MotionDetectionSettings { PostMotionDurationSeconds = postMotionSeconds, CooldownSeconds = 0 });
        settings.CameraDisplay.Returns(new CameraDisplayAppSettings { SnapshotPath = Path.Combine(root, "snapshots") });

        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns(_ => cameras.ToList());
        storage.GetCameraById(Arg.Any<Guid>()).Returns(c => cameras.Find(x => x.Id == c.Arg<Guid>()));

        return new RecordingService(
            NullLogger<RecordingService>.Instance,
            settings,
            Substitute.For<IThumbnailGeneratorService>(),
            storage);
    }
}