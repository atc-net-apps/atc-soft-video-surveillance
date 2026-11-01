namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// A recording goes on until a person stops it: when its file ends while the camera sends pictures
/// again (the stream dropped and came back), a new file starts at once; when the service had to stop
/// it (a write that failed, a full disk), it starts again with back-off. A recording a person stopped
/// stays stopped.
/// </summary>
public sealed class RecordingServiceResumeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-cw-resume-" + Guid.NewGuid().ToString("N"));
    private readonly List<CameraConfiguration> cameras = [];
    private readonly ManualClock clock = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingSettings recording;

    public RecordingServiceResumeTests()
    {
        Directory.CreateDirectory(root);
        recording = new RecordingSettings
        {
            RecordingPath = Path.Combine(root, "recordings"),
            Cleanup = new MediaCleanupSettings { EnableDiskSpaceGuard = false },
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_Recording_Whose_File_Ended_While_The_Camera_Sends_Pictures_Gets_A_New_File()
    {
        var door = Camera("Door");
        var pipeline = Pipeline();
        using var sut = CreateSut();
        sut.StartRecording(door, pipeline).Should().BeTrue();
        var first = sut.GetSession(door.Id)!.CurrentFilePath;

        // The stream dropped (the engine ended the file) and came back.
        pipeline.IsRecordingActive.Returns(false);
        pipeline.LastPacketUtc.Returns(clock.GetUtcNow().UtcDateTime);

        sut.ResumeRecordings().Should().Be(1);

        pipeline.Received(1).SwitchRecording(Arg.Any<string>());
        sut.IsRecording(door.Id).Should().BeTrue();
        sut.GetSession(door.Id)!.CurrentFilePath.Should().NotBe(first);
    }

    [Fact]
    public void A_Recording_Is_Not_Resumed_While_Its_Camera_Sends_Nothing()
    {
        var door = Camera("Door");
        var pipeline = Pipeline();
        using var sut = CreateSut();
        sut.StartRecording(door, pipeline).Should().BeTrue();

        pipeline.IsRecordingActive.Returns(false);
        pipeline.LastPacketUtc.Returns(clock.GetUtcNow().UtcDateTime.AddMinutes(-1));

        sut.ResumeRecordings().Should().Be(0);
        pipeline.DidNotReceive().SwitchRecording(Arg.Any<string>());
        sut.IsRecording(door.Id).Should().BeTrue("the session waits for the camera");
    }

    [Fact]
    public void A_Recording_That_Writes_Is_Left_Alone()
    {
        var door = Camera("Door");
        var pipeline = Pipeline();
        using var sut = CreateSut();
        sut.StartRecording(door, pipeline).Should().BeTrue();
        pipeline.LastPacketUtc.Returns(clock.GetUtcNow().UtcDateTime);

        sut.ResumeRecordings().Should().Be(0);
        pipeline.DidNotReceive().SwitchRecording(Arg.Any<string>());
    }

    [Fact]
    public void A_Recording_Stopped_Because_A_Write_Failed_Starts_Again_After_The_Back_Off()
    {
        var door = Camera("Door");
        var pipeline = Pipeline();
        using var sut = CreateSut();
        sut.StartRecording(door, pipeline).Should().BeTrue();

        pipeline.RecordingError.Returns("Writing 'door.mkv' failed: the network name is no longer available");
        pipeline.IsRecordingActive.Returns(false);
        sut.EnforceDiskSpaceGuard();
        sut.IsRecording(door.Id).Should().BeFalse("nothing can be written");

        pipeline.RecordingError.Returns((string?)null);
        pipeline.LastPacketUtc.Returns(clock.GetUtcNow().UtcDateTime);
        sut.ResumeRecordings().Should().Be(0, "not before the back-off");

        clock.Advance(TimeSpan.FromSeconds(31));
        pipeline.LastPacketUtc.Returns(clock.GetUtcNow().UtcDateTime);
        sut.ResumeRecordings().Should().Be(1);

        sut.IsRecording(door.Id).Should().BeTrue();
        pipeline.Received(2).StartRecording(Arg.Any<string>());
    }

    [Fact]
    public void A_Recording_A_Person_Stopped_Is_Not_Started_Again()
    {
        var door = Camera("Door");
        var pipeline = Pipeline();
        using var sut = CreateSut();
        sut.StartRecording(door, pipeline).Should().BeTrue();
        pipeline.RecordingError.Returns("Writing 'door.mkv' failed");
        sut.EnforceDiskSpaceGuard();

        sut.StopRecording(door.Id);
        clock.Advance(TimeSpan.FromMinutes(10));
        pipeline.RecordingError.Returns((string?)null);
        pipeline.LastPacketUtc.Returns(clock.GetUtcNow().UtcDateTime);

        sut.ResumeRecordings().Should().Be(0);
        sut.IsRecording(door.Id).Should().BeFalse();
    }

    [Fact]
    public void A_Restart_That_Fails_Waits_Longer_Each_Time()
    {
        var door = Camera("Door");
        var pipeline = Pipeline();
        using var sut = CreateSut();
        sut.StartRecording(door, pipeline).Should().BeTrue();
        pipeline.RecordingError.Returns("Writing 'door.mkv' failed");
        sut.EnforceDiskSpaceGuard();

        // The folder cannot be made: every start fails.
        recording.RecordingPath = Path.Combine(root, "file-not-folder");
        File.WriteAllText(recording.RecordingPath, "in the way");

        clock.Advance(TimeSpan.FromSeconds(31));
        pipeline.LastPacketUtc.Returns(clock.GetUtcNow().UtcDateTime);
        sut.ResumeRecordings().Should().Be(0);

        clock.Advance(TimeSpan.FromSeconds(31));
        pipeline.LastPacketUtc.Returns(clock.GetUtcNow().UtcDateTime);
        sut.ResumeRecordings();
        pipeline.Received(1).StartRecording(Arg.Any<string>());
    }

    private RecordingService CreateSut()
    {
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(recording);
        settings.CameraDisplay.Returns(new CameraDisplayAppSettings { SnapshotPath = Path.Combine(root, "snapshots") });

        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns(_ => cameras.ToList());
        storage.GetCameraById(Arg.Any<Guid>()).Returns(c => cameras.Find(x => x.Id == c.Arg<Guid>()));

        return new RecordingService(
            NullLogger<RecordingService>.Instance,
            settings,
            Substitute.For<IThumbnailGeneratorService>(),
            storage)
        {
            Time = clock,
        };
    }

    private CameraConfiguration Camera(string name)
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = name;
        cameras.Add(camera);
        return camera;
    }

    private static IMediaPipeline Pipeline()
    {
        var pipeline = Substitute.For<IMediaPipeline>();
        pipeline.IsRecordingActive.Returns(true);
        return pipeline;
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow()
            => now;

        public void Advance(TimeSpan by)
            => now += by;
    }
}