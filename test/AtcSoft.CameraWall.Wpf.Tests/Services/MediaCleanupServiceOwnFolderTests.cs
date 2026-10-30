namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// A camera can record into a folder of its own. The clean-up keeps that folder to the
/// retention too (before, it cleaned only the global folder, so those recordings grew forever).
/// </summary>
public sealed class MediaCleanupServiceOwnFolderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-cleanup-own-" + Guid.NewGuid().ToString("N"));
    private int maxStorageGb;

    private string Global
        => Path.Combine(root, "global");

    private string Own
        => Path.Combine(root, "own");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Old_Recordings_In_A_Cameras_Own_Folder_Are_Removed_And_New_Ones_Kept()
    {
        var old = Touch(DateTime.Now.AddDays(-40), Own, "Garden", "Garden_20260901_101500_000.mkv");
        var recent = Touch(DateTime.Now.AddDays(-1), Own, "Garden", "Garden_20261016_101500_000.mkv");
        using var sut = CreateSut();

        await sut.RunCleanupAsync();

        File.Exists(old).Should().BeFalse("it is older than the 30 days kept");
        File.Exists(recent).Should().BeTrue();
    }

    [Fact]
    public async Task Above_The_Size_Limit_The_Oldest_Recordings_Of_Every_Folder_Go()
    {
        maxStorageGb = 1;
        var oldest = Touch(DateTime.Now.AddDays(-3), Own, "Garden", "Garden_20261015_101500_000.mkv", sizeBytes: 600 * 1024 * 1024);
        var newer = Touch(DateTime.Now.AddDays(-1), Global, "Door", "Door_20261017_101500_000.mkv", sizeBytes: 600 * 1024 * 1024);
        using var sut = CreateSut();

        await sut.RunCleanupAsync();

        File.Exists(oldest).Should().BeFalse("1.2 GB of recordings is above the 1 GB kept");
        File.Exists(newer).Should().BeTrue();
    }

    private MediaCleanupService CreateSut()
    {
        Directory.CreateDirectory(Global);
        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings
        {
            RecordingPath = Global,
            Cleanup = new MediaCleanupSettings { RecordingRetentionDays = 30, EnableDiskSpaceGuard = false, MaxRecordingStorageGb = maxStorageGb },
        });
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings { SnapshotPath = Path.Combine(root, "snapshots") });

        var recordingService = Substitute.For<IRecordingService>();
        recordingService.GetActiveSessions().Returns([]);

        var camera = new CameraConfiguration();
        camera.Overrides.Recording.RecordingPath = Own;
        var cameraStorage = Substitute.For<ICameraStorageService>();
        cameraStorage.GetAllCameras().Returns([camera]);

        return new MediaCleanupService(
            NullLogger<MediaCleanupService>.Instance,
            settingsService,
            recordingService,
            cameraStorage);
    }

    private static string Touch(
        DateTime lastWrite,
        params string[] segments)
        => Touch(lastWrite, Path.Combine(segments), sizeBytes: 1);

    private static string Touch(
        DateTime lastWrite,
        string folder,
        string camera,
        string name,
        long sizeBytes)
        => Touch(lastWrite, Path.Combine(folder, camera, name), sizeBytes);

    // A sparse length: the size counts without writing the bytes.
    private static string Touch(
        DateTime lastWrite,
        string path,
        long sizeBytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var file = File.Create(path))
        {
            file.SetLength(sizeBytes);
        }

        File.SetLastWriteTime(path, lastWrite);
        return path;
    }
}