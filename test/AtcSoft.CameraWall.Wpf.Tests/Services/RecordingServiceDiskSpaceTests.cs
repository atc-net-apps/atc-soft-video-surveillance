namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// The disk-space guard covers every folder a camera records into, not only the global one: a
/// camera whose own folder is on a full volume does not start, a volume that fills stops only
/// the recordings on it, and a volume whose space cannot be read gets no new recordings.
/// </summary>
public sealed class RecordingServiceDiskSpaceTests : IDisposable
{
    private const long MinFreeMb = 1;
    private const long Plenty = 10L * 1024 * 1024 * 1024;

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-cw-guard-" + Guid.NewGuid().ToString("N"));
    private readonly FakeVolumes volumes = new();
    private readonly List<CameraConfiguration> cameras = [];
    private readonly RecordingSettings recording;

    public RecordingServiceDiskSpaceTests()
    {
        Directory.CreateDirectory(VolumeA);
        Directory.CreateDirectory(VolumeB);
        recording = new RecordingSettings
        {
            RecordingPath = Path.Combine(VolumeA, "recordings"),
            Cleanup = new MediaCleanupSettings { EnableDiskSpaceGuard = true, MinFreeSpaceMb = (int)MinFreeMb },
        };
        volumes.Set(VolumeA, Plenty);
        volumes.Set(VolumeB, Plenty);
    }

    private string VolumeA => Path.Combine(root, "A");

    private string VolumeB => Path.Combine(root, "B");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_Camera_Whose_Own_Folder_Is_On_A_Full_Volume_Does_Not_Start()
    {
        var garden = Camera("Garden", Path.Combine(VolumeB, "garden"));
        var door = Camera("Door");
        volumes.Set(VolumeB, 0);
        using var sut = CreateSut();

        sut.StartRecording(garden, Pipeline()).Should().BeFalse();
        sut.StartRecording(door, Pipeline()).Should().BeTrue("the global folder's volume has space");
    }

    [Fact]
    public void A_Camera_On_A_Volume_Whose_Space_Cannot_Be_Read_Does_Not_Start()
    {
        var share = Camera("Share", Path.Combine(root, "not-a-known-volume", "share"));
        using var sut = CreateSut();

        sut.StartRecording(share, Pipeline()).Should().BeFalse();
    }

    [Fact]
    public void The_Default_Folder_Is_Guarded_When_No_Recording_Folder_Is_Set()
    {
        // The default folder is on a volume this test cannot read, so the start is refused:
        // the guard looked at it (before, an empty setting skipped the guard).
        recording.RecordingPath = string.Empty;
        var door = Camera("Door");
        using var sut = CreateSut();

        sut.StartRecording(door, Pipeline()).Should().BeFalse();
    }

    [Fact]
    public void Old_Recordings_In_A_Cameras_Own_Folder_Make_Room_On_Its_Volume()
    {
        var own = Path.Combine(VolumeB, "garden");
        var garden = Camera("Garden", own);
        var old = Write(Path.Combine(own, "Garden", "old.mkv"), 2 * 1024 * 1024, daysAgo: 20);
        volumes.Set(VolumeB, 512 * 1024);
        using var sut = CreateSut();

        sut.StartRecording(garden, Pipeline()).Should().BeTrue();
        File.Exists(old).Should().BeFalse();
    }

    [Fact]
    public void A_Volume_That_Fills_Stops_Only_The_Recordings_On_It()
    {
        var garden = Camera("Garden", Path.Combine(VolumeB, "garden"));
        var door = Camera("Door");
        using var sut = CreateSut();
        sut.StartRecording(garden, Pipeline()).Should().BeTrue();
        sut.StartRecording(door, Pipeline()).Should().BeTrue();

        volumes.Set(VolumeB, 0);
        var stopped = sut.EnforceDiskSpaceGuard();

        stopped.Should().BeTrue();
        sut.IsRecording(garden.Id).Should().BeFalse();
        sut.IsRecording(door.Id).Should().BeTrue();
    }

    [Fact]
    public void A_Volume_Whose_Space_Can_No_Longer_Be_Read_Does_Not_Stop_Its_Recordings()
    {
        var garden = Camera("Garden", Path.Combine(VolumeB, "garden"));
        using var sut = CreateSut();
        sut.StartRecording(garden, Pipeline()).Should().BeTrue();

        volumes.Set(VolumeB, null);
        sut.EnforceDiskSpaceGuard();

        sut.IsRecording(garden.Id).Should().BeTrue("a share that hiccups must not end a recording; a failed write does");
    }

    [Fact]
    public void A_Recording_Whose_Writes_Fail_Is_Stopped()
    {
        // A full disk the guard did not see coming (another program filled it): the recorder
        // can write nothing more, so the recording must not go on claiming to run.
        var door = Camera("Door");
        var pipeline = Pipeline();
        using var sut = CreateSut();
        sut.StartRecording(door, pipeline).Should().BeTrue();

        pipeline.RecordingError.Returns("Writing 'door.mkv' failed: No space left on device");
        pipeline.IsRecordingActive.Returns(false);
        sut.EnforceDiskSpaceGuard();

        sut.IsRecording(door.Id).Should().BeFalse();
    }

    private RecordingService CreateSut()
    {
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(recording);
        settings.CameraDisplay.Returns(new CameraDisplayAppSettings { SnapshotPath = Path.Combine(VolumeA, "snapshots") });

        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns(_ => cameras.ToList());
        storage.GetCameraById(Arg.Any<Guid>()).Returns(c => cameras.Find(x => x.Id == c.Arg<Guid>()));

        return new RecordingService(
            NullLogger<RecordingService>.Instance,
            settings,
            Substitute.For<IThumbnailGeneratorService>(),
            storage,
            new DiskSpaceGuard(volumes));
    }

    private CameraConfiguration Camera(
        string name,
        string? ownFolder = null)
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = name;
        camera.Overrides.Recording.RecordingPath = ownFolder;
        cameras.Add(camera);
        return camera;
    }

    private static IMediaPipeline Pipeline()
    {
        var pipeline = Substitute.For<IMediaPipeline>();
        pipeline.IsRecordingActive.Returns(true);
        return pipeline;
    }

    private static string Write(
        string path,
        int size,
        int daysAgo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        File.SetLastWriteTime(path, DateTime.Now.AddDays(-daysAgo));
        return path;
    }
}