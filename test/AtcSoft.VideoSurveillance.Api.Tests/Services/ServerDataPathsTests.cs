namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed class ServerDataPathsTests
{
    private static readonly string DataPath = Path.Combine(Path.GetTempPath(), "atcsoft-data-paths");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Without_A_Data_Folder_The_Default_Location_Is_Used(
        string? dataPath)
    {
        var sut = new ServerDataPaths(dataPath);

        sut.DataPath.Should().BeNull();
        sut.SettingsPath.Should().Be(ApplicationPaths.DefaultSettingsPath);
        sut.CameraDataPath.Should().Be(ApplicationPaths.DefaultCameraDataPath);
    }

    [Fact]
    public void With_A_Data_Folder_The_Files_Are_In_It()
    {
        var sut = new ServerDataPaths(DataPath);

        sut.SettingsPath.Should().Be(Path.Combine(DataPath, "settings.json"));
        sut.CameraDataPath.Should().Be(Path.Combine(DataPath, "cameras.json"));
        sut.KeysPath.Should().Be(Path.Combine(DataPath, "keys"));
    }

    [Fact]
    public void RelocateDefaults_Moves_The_Default_Folders_Into_The_Data_Folder()
    {
        var recording = new RecordingSettings();
        var cameraDisplay = new CameraDisplayAppSettings();
        var advanced = new AdvancedSettings();
        var settings = CreateSettings(recording, cameraDisplay, advanced);

        new ServerDataPaths(DataPath).RelocateDefaults(settings);

        recording.RecordingPath.Should().Be(Path.Combine(DataPath, "recordings"));
        cameraDisplay.SnapshotPath.Should().Be(Path.Combine(DataPath, "snapshots"));
        advanced.LogPath.Should().Be(Path.Combine(DataPath, "logs"));
        settings.Received(1).SaveRecording(recording);
        settings.Received(1).SaveCameraDisplay(cameraDisplay);
        settings.Received(1).SaveAdvanced(advanced);
    }

    [Fact]
    public void RelocateDefaults_Leaves_A_Folder_The_User_Picked_Alone()
    {
        var picked = Path.Combine(Path.GetTempPath(), "picked-by-the-user");
        var settings = CreateSettings(
            new RecordingSettings { RecordingPath = picked },
            new CameraDisplayAppSettings { SnapshotPath = picked },
            new AdvancedSettings { LogPath = picked });

        new ServerDataPaths(DataPath).RelocateDefaults(settings);

        settings.Recording.RecordingPath.Should().Be(picked);
        settings.CameraDisplay.SnapshotPath.Should().Be(picked);
        settings.Advanced.LogPath.Should().Be(picked);
        settings.DidNotReceiveWithAnyArgs().SaveRecording(default!);
    }

    [Fact]
    public void RelocateDefaults_Does_Nothing_Without_A_Data_Folder()
    {
        var settings = CreateSettings(new RecordingSettings(), new CameraDisplayAppSettings(), new AdvancedSettings());

        new ServerDataPaths(dataPath: null).RelocateDefaults(settings);

        settings.Recording.RecordingPath.Should().Be(ApplicationPaths.DefaultRecordingsPath);
        settings.DidNotReceiveWithAnyArgs().SaveRecording(default!);
    }

    private static IApplicationSettingsService CreateSettings(
        RecordingSettings recording,
        CameraDisplayAppSettings cameraDisplay,
        AdvancedSettings advanced)
    {
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(recording);
        settings.CameraDisplay.Returns(cameraDisplay);
        settings.Advanced.Returns(advanced);
        return settings;
    }
}