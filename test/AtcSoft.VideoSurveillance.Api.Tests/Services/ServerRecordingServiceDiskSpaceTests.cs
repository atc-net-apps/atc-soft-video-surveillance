namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// The server's disk-space guard: no recording starts on a full volume or one whose free space
/// cannot be read, old recordings make room, and a volume that fills stops its recordings.
/// </summary>
public sealed class ServerRecordingServiceDiskSpaceTests : IDisposable
{
    private const long Plenty = 10L * 1024 * 1024 * 1024;

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-server-guard-" + Guid.NewGuid().ToString("N"));
    private readonly Volumes volumes = new();
    private readonly RecordingSettings recordingSettings;
    private readonly IApplicationSettingsService settingsService = Substitute.For<IApplicationSettingsService>();
    private readonly ICameraStorageService cameraStorageService = Substitute.For<ICameraStorageService>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly CameraConfiguration camera = new() { Id = Guid.NewGuid() };

    public ServerRecordingServiceDiskSpaceTests()
    {
        Directory.CreateDirectory(root);
        recordingSettings = new RecordingSettings { RecordingPath = Path.Combine(root, "recordings"), RecordingFormat = "mkv" };
        recordingSettings.Cleanup.EnableDiskSpaceGuard = true;
        recordingSettings.Cleanup.MinFreeSpaceMb = 1;
        settingsService.Recording.Returns(recordingSettings);
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings { SnapshotPath = Path.Combine(root, "snapshots") });

        camera.Display.DisplayName = "Front Door";
        cameraStorageService.GetCameraById(camera.Id).Returns(camera);
        pipeline.IsRecordingActive.Returns(true);
        volumes.Free = Plenty;
    }

    public void Dispose()
    {
        pipeline.Dispose();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_Recording_Does_Not_Start_On_A_Full_Volume()
    {
        volumes.Free = 0;
        using var sut = CreateSut();

        sut.StartRecording(camera, pipeline).Should().BeFalse();
    }

    [Fact]
    public void A_Recording_Does_Not_Start_When_The_Free_Space_Cannot_Be_Read()
    {
        volumes.Free = null;
        using var sut = CreateSut();

        sut.StartRecording(camera, pipeline).Should().BeFalse();
    }

    [Fact]
    public void Old_Recordings_Make_Room_For_A_New_One()
    {
        var old = Path.Combine(recordingSettings.RecordingPath, "Front Door", "old.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(old)!);
        File.WriteAllBytes(old, new byte[2 * 1024 * 1024]);
        File.SetLastWriteTime(old, DateTime.Now.AddDays(-5));
        volumes.Free = 512 * 1024;
        using var sut = CreateSut();

        sut.StartRecording(camera, pipeline).Should().BeTrue();
        File.Exists(old).Should().BeFalse();
    }

    [Fact]
    public void A_Volume_That_Fills_Stops_Its_Recordings_But_One_That_Cannot_Be_Read_Does_Not()
    {
        using var sut = CreateSut();
        sut.StartRecording(camera, pipeline).Should().BeTrue();

        volumes.Free = null;
        sut.EnforceDiskSpaceGuard().Should().BeFalse();
        sut.IsRecording(camera.Id).Should().BeTrue();

        volumes.Free = 0;
        sut.EnforceDiskSpaceGuard().Should().BeTrue();
        sut.IsRecording(camera.Id).Should().BeFalse();
    }

    [Fact]
    public void A_Recording_Whose_Writes_Fail_Is_Stopped_By_The_Reaper()
    {
        using var sut = CreateSut();
        sut.StartRecording(camera, pipeline).Should().BeTrue();

        pipeline.RecordingError.Returns("Writing 'front.mkv' failed: No space left on device");
        pipeline.IsRecordingActive.Returns(false);

        sut.ReapInactiveSessions().Should().Be(1);
        sut.IsRecording(camera.Id).Should().BeFalse();
    }

    private ServerRecordingService CreateSut()
        => new(
            settingsService,
            cameraStorageService,
            NullLogger<ServerRecordingService>.Instance,
            diskSpaceGuard: new DiskSpaceGuard(volumes));

    // One volume for the whole test folder, its free space set by the test.
    private sealed class Volumes : IFreeSpaceReader
    {
        public long? Free { get; set; }

        public string? GetVolume(string path)
            => "test-volume";

        public long? GetFreeBytes(string volume)
            => Free;
    }
}