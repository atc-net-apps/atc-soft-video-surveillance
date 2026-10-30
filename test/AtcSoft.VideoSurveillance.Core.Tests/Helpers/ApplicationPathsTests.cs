namespace AtcSoft.VideoSurveillance.Helpers;

public class ApplicationPathsTests
{
    [Fact]
    public void DefaultPaths_Are_Not_Null_Or_Empty()
    {
        // Assert
        ApplicationPaths.DefaultLogsPath.Should().NotBeNullOrEmpty();
        ApplicationPaths.DefaultSnapshotsPath.Should().NotBeNullOrEmpty();
        ApplicationPaths.DefaultRecordingsPath.Should().NotBeNullOrEmpty();
        ApplicationPaths.DefaultSettingsPath.Should().NotBeNullOrEmpty();
        ApplicationPaths.DefaultCameraDataPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DefaultPaths_Contain_AtcSoft_Folder()
    {
        // Assert
        ApplicationPaths.DefaultLogsPath.Should().Contain("AtcSoft");
        ApplicationPaths.DefaultSnapshotsPath.Should().Contain("AtcSoft");
        ApplicationPaths.DefaultRecordingsPath.Should().Contain("AtcSoft");
    }

    [Fact]
    public void DefaultPaths_Do_Not_Contain_The_Pre_Rename_Folder()
    {
        // Assert
        ApplicationPaths.DefaultLogsPath.Should().NotContain("Linksoft");
        ApplicationPaths.DefaultSnapshotsPath.Should().NotContain("Linksoft");
        ApplicationPaths.DefaultRecordingsPath.Should().NotContain("Linksoft");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DataPathOverride_Is_Null_Without_A_Value(string? value)
        => ApplicationPaths.ParseDataPathOverride(value).Should().BeNull();

    [Fact]
    public void DataPathOverride_Is_The_Full_Path_Of_The_Value()
        => ApplicationPaths.ParseDataPathOverride(@"C:\Temp\e2e\..\run1")
            .Should().Be(@"C:\Temp\run1");

    [Fact]
    public void LocalDataPath_Is_Under_LocalAppData_Without_An_Override()
        => ApplicationPaths.ResolveLocalDataPath(null, "VideoSurveillance", "servers.json")
            .Should().Be(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtcSoft",
                "VideoSurveillance",
                "servers.json"));

    [Fact]
    public void LocalDataPath_Is_Under_The_Override_When_There_Is_One()
        => ApplicationPaths.ResolveLocalDataPath(@"C:\Temp\run1", "VideoSurveillance", "servers.json")
            .Should().Be(@"C:\Temp\run1\local\VideoSurveillance\servers.json");

    [Fact]
    public void BaseDataPath_Is_The_Override_When_There_Is_One()
        => ApplicationPaths.ResolveBaseDataPath(@"C:\Temp\run1", "CameraWall")
            .Should().Be(@"C:\Temp\run1");

    [Fact]
    public void BaseDataPath_Is_Under_ProgramData_Without_An_Override()
        => ApplicationPaths.ResolveBaseDataPath(null, "CameraWall")
            .Should().Be(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "AtcSoft",
                "CameraWall"));
}