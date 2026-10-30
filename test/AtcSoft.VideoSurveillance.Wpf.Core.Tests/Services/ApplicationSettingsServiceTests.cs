namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

using SharedPaths = AtcSoft.VideoSurveillance.Helpers.ApplicationPaths;

/// <summary>
/// The standalone app and the server must be able to run on the same PC
/// without sharing a data folder. The shared settings model defaults to the
/// server's folder, so the app has to point its own data at its own folder.
/// </summary>
public sealed class ApplicationSettingsServiceTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-settings-" + Guid.NewGuid().ToString("N"));
    private readonly string settingsPath;

    public ApplicationSettingsServiceTests()
    {
        System.IO.Directory.CreateDirectory(directory);
        settingsPath = System.IO.Path.Combine(directory, "settings.json");
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(directory))
        {
            TestFolders.Delete(directory);
        }
    }

    [Fact]
    public void The_Apps_Own_Folder_Is_Not_The_Servers()
    {
        ApplicationPaths.DefaultRecordingsPath.Should().NotBe(SharedPaths.DefaultRecordingsPath);
    }

    [Fact]
    public void A_New_Install_Keeps_Its_Data_In_The_Apps_Own_Folder()
    {
        var sut = new ApplicationSettingsService(settingsPath);

        sut.Recording.RecordingPath.Should().Be(ApplicationPaths.DefaultRecordingsPath);
        sut.CameraDisplay.SnapshotPath.Should().Be(ApplicationPaths.DefaultSnapshotsPath);
        sut.Advanced.LogPath.Should().Be(ApplicationPaths.DefaultLogsPath);
    }

    [Fact]
    public void A_New_Install_Writes_Its_Own_Folder_To_The_Settings_File()
    {
        _ = new ApplicationSettingsService(settingsPath);

        var json = System.IO.File.ReadAllText(settingsPath);
        json.Should().NotContain(@"AtcSoft\\VideoSurveillance");
    }

    [Fact]
    public void Paths_Saved_From_The_Old_Shared_Default_Move_To_The_Apps_Own_Folder()
    {
        // Earlier versions wrote the server's folder into the app's settings
        // file as the default; nobody chose it.
        var json = $$"""
            {
              "cameraDisplay": { "snapshotPath": {{System.Text.Json.JsonSerializer.Serialize(SharedPaths.DefaultSnapshotsPath)}} },
              "recording": { "recordingPath": {{System.Text.Json.JsonSerializer.Serialize(SharedPaths.DefaultRecordingsPath)}} },
              "advanced": { "logPath": {{System.Text.Json.JsonSerializer.Serialize(SharedPaths.DefaultLogsPath)}} }
            }
            """;
        System.IO.File.WriteAllText(settingsPath, json);

        var sut = new ApplicationSettingsService(settingsPath);

        sut.Recording.RecordingPath.Should().Be(ApplicationPaths.DefaultRecordingsPath);
        sut.CameraDisplay.SnapshotPath.Should().Be(ApplicationPaths.DefaultSnapshotsPath);
        sut.Advanced.LogPath.Should().Be(ApplicationPaths.DefaultLogsPath);
    }

    [Fact]
    public void Moved_Paths_Are_Written_Back_So_The_File_Says_Where_The_Data_Is()
    {
        var json = $$"""
            { "recording": { "recordingPath": {{System.Text.Json.JsonSerializer.Serialize(SharedPaths.DefaultRecordingsPath)}} } }
            """;
        System.IO.File.WriteAllText(settingsPath, json);

        _ = new ApplicationSettingsService(settingsPath);

        System.IO.File.ReadAllText(settingsPath).Should().NotContain(@"AtcSoft\\VideoSurveillance");
    }

    [Fact]
    public void A_Path_The_User_Chose_Is_Kept()
    {
        const string json = """
            {
              "cameraDisplay": { "snapshotPath": "D:\\Cctv\\snapshots" },
              "recording": { "recordingPath": "D:\\Cctv\\recordings" },
              "advanced": { "logPath": "D:\\Cctv\\logs" }
            }
            """;
        System.IO.File.WriteAllText(settingsPath, json);

        var sut = new ApplicationSettingsService(settingsPath);

        sut.Recording.RecordingPath.Should().Be(@"D:\Cctv\recordings");
        sut.CameraDisplay.SnapshotPath.Should().Be(@"D:\Cctv\snapshots");
        sut.Advanced.LogPath.Should().Be(@"D:\Cctv\logs");
    }

    [Fact]
    public void A_Saved_Setting_Keeps_The_Apps_Own_Folder()
    {
        // Saving one group of settings must not bring the shared default back.
        var sut = new ApplicationSettingsService(settingsPath);
        sut.SaveRecording(new RecordingSettings { RecordingFormat = "mp4" });

        var reloaded = new ApplicationSettingsService(settingsPath);

        reloaded.Recording.RecordingFormat.Should().Be("mp4");
        reloaded.Recording.RecordingPath.Should().Be(ApplicationPaths.DefaultRecordingsPath);
    }
}