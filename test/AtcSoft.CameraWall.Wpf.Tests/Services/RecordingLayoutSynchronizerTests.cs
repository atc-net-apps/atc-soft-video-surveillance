namespace AtcSoft.CameraWall.Wpf.Services;

using RecordingSession = AtcSoft.VideoSurveillance.Models.RecordingSession;

public sealed class RecordingLayoutSynchronizerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-layout-sync-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Moves_The_Recordings_To_The_Layout_In_The_Settings()
    {
        Touch("Cam", "Cam_20261012_101500_000.mkv");
        Touch("Cam", "Cam_20261012_101500_000.png");
        var sut = CreateSut(useDailySubfolders: true, separateThumbnailFolder: true, out _);

        var result = await sut.SynchronizeAsync(TestContext.Current.CancellationToken);

        result.MovedRecordings.Should().Be(1);
        result.MovedThumbnails.Should().Be(1);
        File.Exists(Path.Combine(root, "Cam", "2026-10-12", "Cam_20261012_101500_000.mkv")).Should().BeTrue();
        File.Exists(Path.Combine(root, "thumbnails", "Cam", "2026-10-12", "Cam_20261012_101500_000.png")).Should().BeTrue();
    }

    [Fact]
    public async Task Restarts_A_Running_Recording_That_Is_Not_In_Place()
    {
        var live = Touch("Cam", "Cam_20261012_101500_000.mkv");
        var session = new RecordingSession(Guid.NewGuid(), "Cam", live, isManualRecording: true);
        var sut = CreateSut(useDailySubfolders: true, separateThumbnailFolder: false, out var recordingService);
        recordingService.GetActiveSessions().Returns([session]);

        await sut.SynchronizeAsync(TestContext.Current.CancellationToken);

        recordingService.Received(1).SegmentRecording(session.CameraId);
    }

    [Fact]
    public async Task Moves_The_Recordings_In_A_Cameras_Own_Folder_Too()
    {
        var own = Path.Combine(root, "own");
        Directory.CreateDirectory(Path.Combine(root, "global"));
        Touch("own", "Garden", "Garden_20261012_101500_000.mkv");
        var camera = new CameraConfiguration();
        camera.Overrides.Recording.RecordingPath = own;
        var cameraStorage = Substitute.For<ICameraStorageService>();
        cameraStorage.GetAllCameras().Returns([camera]);
        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = Path.Combine(root, "global"), UseDailySubfolders = true });
        var recordingService = Substitute.For<IRecordingService>();
        recordingService.GetActiveSessions().Returns([]);
        var sut = new RecordingLayoutSynchronizer(NullLogger<RecordingLayoutSynchronizer>.Instance, settingsService, recordingService, cameraStorage);

        var result = await sut.SynchronizeAsync(TestContext.Current.CancellationToken);

        result.MovedRecordings.Should().Be(1);
        File.Exists(Path.Combine(own, "Garden", "2026-10-12", "Garden_20261012_101500_000.mkv")).Should().BeTrue();
    }

    [Fact]
    public async Task Does_Nothing_When_There_Is_No_Recording_Folder_Yet()
    {
        var sut = CreateSut(useDailySubfolders: true, separateThumbnailFolder: true, out _);

        var result = await sut.SynchronizeAsync(TestContext.Current.CancellationToken);

        result.MovedRecordings.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    private RecordingLayoutSynchronizer CreateSut(
        bool useDailySubfolders,
        bool separateThumbnailFolder,
        out IRecordingService recordingService)
    {
        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings
        {
            RecordingPath = root,
            UseDailySubfolders = useDailySubfolders,
            SeparateThumbnailFolder = separateThumbnailFolder,
        });

        recordingService = Substitute.For<IRecordingService>();
        recordingService.GetActiveSessions().Returns([]);
        recordingService.SegmentRecording(Arg.Any<Guid>()).Returns(true);

        return new RecordingLayoutSynchronizer(
            NullLogger<RecordingLayoutSynchronizer>.Instance,
            settingsService,
            recordingService);
    }

    private string Touch(params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }
}