namespace AtcSoft.VideoSurveillance.Wpf.Services;

public class ApiApplicationSettingsServiceTests
{
    [Fact]
    public void DiskSpaceGuard_Settings_Survive_The_Trip_To_The_Api_And_Back()
    {
        // Arrange
        var settings = ApiApplicationSettingsService.BuildCacheFrom(new ClientPreferences(), api: null);
        settings.Recording.Cleanup.EnableDiskSpaceGuard = false;
        settings.Recording.Cleanup.MinFreeSpaceMb = 5000;
        settings.Recording.Cleanup.MaxRecordingStorageGb = 500;

        // Act
        var api = ApiApplicationSettingsService.BuildApiSettings(settings);
        var roundTripped = ApiApplicationSettingsService.BuildCacheFrom(new ClientPreferences(), AsServerAnswer(api));

        // Assert
        api.EnableDiskSpaceGuard.Should().BeFalse();
        api.MinFreeSpaceMb.Should().Be(5000);
        roundTripped.Recording.Cleanup.EnableDiskSpaceGuard.Should().BeFalse();
        roundTripped.Recording.Cleanup.MinFreeSpaceMb.Should().Be(5000);
        roundTripped.Recording.Cleanup.MaxRecordingStorageGb.Should().Be(500);
    }

    [Fact]
    public void The_Language_Stays_On_This_Machine()
    {
        var settings = ApiApplicationSettingsService.BuildCacheFrom(new ClientPreferences { Language = "1030" }, api: null);

        var api = ApiApplicationSettingsService.BuildApiSettings(settings);

        // Absent = unchanged on the server; the language is this client's own.
#pragma warning disable CS0618 // the WPF client leaves the deprecated language out
        api.Language.Should().BeNull();
#pragma warning restore CS0618
        settings.General.Language.Should().Be("1030");
    }

    [Fact]
    public void Every_Choice_Survives_The_Trip_To_The_Api_And_Back()
    {
        // Arrange — every choice differs from its default
        var settings = ApiApplicationSettingsService.BuildCacheFrom(new ClientPreferences(), api: null);
        settings.General.ThemeBase = "Light";
        settings.CameraDisplay.OverlayPosition = AtcSoft.VideoSurveillance.Enums.OverlayPosition.BottomRight;
        settings.CameraDisplay.ConnectionTypeIconPlacement = AtcSoft.VideoSurveillance.Enums.ConnectionTypeIconPlacement.SameColumn;
        settings.Connection.DefaultProtocol = AtcSoft.VideoSurveillance.Enums.CameraProtocol.Https;
        settings.Performance.RtspTransport = "udp";
        settings.Recording.RecordingFormat = "mkv";
        settings.Recording.Cleanup.Schedule = AtcSoft.VideoSurveillance.Enums.MediaCleanupSchedule.OnStartupAndPeriodically;

        // Act
        var api = ApiApplicationSettingsService.BuildApiSettings(settings);
        var roundTripped = ApiApplicationSettingsService.BuildCacheFrom(new ClientPreferences(), AsServerAnswer(api));

        // Assert
        api.ThemeBase.Should().Be(AppSettingsThemeBase.Light);
        roundTripped.CameraDisplay.OverlayPosition.Should().Be(AtcSoft.VideoSurveillance.Enums.OverlayPosition.BottomRight);
        roundTripped.CameraDisplay.ConnectionTypeIconPlacement.Should().Be(AtcSoft.VideoSurveillance.Enums.ConnectionTypeIconPlacement.SameColumn);
        roundTripped.Connection.DefaultProtocol.Should().Be(AtcSoft.VideoSurveillance.Enums.CameraProtocol.Https);
        roundTripped.Performance.RtspTransport.Should().Be("udp");
        roundTripped.Recording.RecordingFormat.Should().Be("mkv");
        roundTripped.Recording.Cleanup.Schedule.Should().Be(AtcSoft.VideoSurveillance.Enums.MediaCleanupSchedule.OnStartupAndPeriodically);
    }

    [Fact]
    public void The_Grid_Size_Is_Kept_On_This_Computer_Because_The_Server_Has_No_Place_For_It()
    {
        // Each desktop has its own screen; the size of its grid is no
        // setting of the server.
        var general = new AtcSoft.VideoSurveillance.Models.Settings.GeneralSettings { GridSize = 2, StartRibbonCollapsed = true, ThemeBase = "Light" };

        var preferences = ClientPreferences.From(general);
        var loaded = ApiApplicationSettingsService.BuildCacheFrom(preferences, api: null);

        preferences.GridSize.Should().Be(2);
        loaded.General.GridSize.Should().Be(2);
        loaded.General.StartRibbonCollapsed.Should().BeTrue();
        loaded.General.ThemeBase.Should().Be("Light");
    }

    [Fact]
    public void DiskSpaceGuard_Stays_On_When_The_Api_Is_Unreachable()
    {
        // Act
        var settings = ApiApplicationSettingsService.BuildCacheFrom(new ClientPreferences(), api: null);

        // Assert
        settings.Recording.Cleanup.EnableDiskSpaceGuard.Should().BeTrue();
        settings.Recording.Cleanup.MinFreeSpaceMb.Should().Be(2048);
    }

    /// <summary>
    /// What the server answers to the update: the same settings, as it writes them, with what the
    /// client leaves out (the old detector's settings, the analysis height) kept at the server's values.
    /// </summary>
    private static AppSettings AsServerAnswer(UpdateSettingsRequest update)
    {
        var answer = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(update))!.AsObject();
        var kept = new AtcSoft.VideoSurveillance.Models.Settings.MotionDetectionSettings();
        answer["MinimumChangePercent"] ??= kept.MinimumChangePercent;
        answer["BoundingBoxMinArea"] ??= kept.BoundingBox.MinArea;
        answer["AnalysisHeight"] ??= kept.AnalysisHeight;
        return answer.Deserialize<AppSettings>()!;
    }
}