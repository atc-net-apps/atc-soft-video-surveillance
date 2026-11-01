namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

using System.IO;

/// <summary>
/// A cameras file that cannot be read at start-up (locked by a backup or a virus scanner, damaged)
/// is never taken for no cameras: before, the app started without cameras and the next save wrote
/// the empty list over the file, and every camera was gone.
/// </summary>
public sealed class CameraStorageServiceUnreadableTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "camera-storage-unreadable-" + Guid.NewGuid().ToString("N"));

    public CameraStorageServiceUnreadableTests()
        => Directory.CreateDirectory(folder);

    private string StoragePath => Path.Combine(folder, "cameras.json");

    public void Dispose()
        => TestFolders.Delete(folder);

    [Fact]
    public void A_Damaged_File_Loads_The_Cameras_From_The_Backup_And_Is_Kept()
    {
        // Saved twice: the second save keeps the first as the backup.
        SaveCameras("Door", "Yard").Save();
        File.WriteAllText(StoragePath, "{ this is not json");

        // The store reads its file as it is made.
        var storage = new CameraStorageService(StoragePath);

        storage.GetAllCameras().Select(c => c.Display.DisplayName).Should().BeEquivalentTo("Door", "Yard");
        Directory.GetFiles(folder, "cameras.json.unreadable-*").Should().ContainSingle("the damaged file is kept for a look");
    }

    [Fact]
    public void A_File_Locked_Through_The_Start_Is_Not_Saved_Over()
    {
        _ = SaveCameras("Door", "Yard");
        var before = File.ReadAllText(StoragePath);
        File.Delete(StoragePath + ".bak");

        CameraStorageService storage;
        using (new FileStream(StoragePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            storage = new CameraStorageService(StoragePath);
        }

        storage.GetAllCameras().Should().BeEmpty("the file could not be read");
        storage.Save();

        File.ReadAllText(StoragePath).Should().Be(before, "a save would have written no cameras over the file");
    }

    private CameraStorageService SaveCameras(params string[] names)
    {
        var storage = new CameraStorageService(StoragePath);
        foreach (var name in names)
        {
            var camera = new AtcSoft.Video.Surveillance.Wpf.Core.Models.CameraConfiguration();
            camera.Display.DisplayName = name;
            storage.AddOrUpdateCamera(camera);
        }

        storage.Save();
        return storage;
    }
}