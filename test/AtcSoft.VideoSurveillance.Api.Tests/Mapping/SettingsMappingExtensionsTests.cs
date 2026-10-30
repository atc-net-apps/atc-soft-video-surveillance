namespace AtcSoft.VideoSurveillance.Api.Mapping;

public class SettingsMappingExtensionsTests
{
    [Fact]
    public void ToApiModel_CombinesAllSettingsSources()
    {
        // Arrange
        var general = new GeneralSettings
        {
            ThemeBase = "Dark",
            ThemeAccent = "Blue",
            Language = "1033",
            ConnectCamerasOnStartup = true,
            StartMaximized = false,
        };
        var cameraDisplay = new CameraDisplayAppSettings
        {
            ShowOverlayTitle = true,
            SnapshotPath = @"C:\snapshots",
        };
        var connection = new ConnectionAppSettings { DefaultPort = 554 };
        var performance = new PerformanceSettings { VideoQuality = "Auto" };
        var motionDetection = new MotionDetectionSettings { Sensitivity = 30 };
        var recording = new RecordingSettings
        {
            RecordingPath = @"C:\recordings",
            RecordingFormat = "mp4",
        };
        var advanced = new AdvancedSettings
        {
            EnableDebugLogging = true,
            LogPath = @"C:\logs",
        };

        // Act
        var api = SettingsMappingExtensions.ToApiModel(
            general,
            cameraDisplay,
            connection,
            performance,
            motionDetection,
            recording,
            advanced);

        // Assert
        api.ThemeBase.Should().Be(AppSettingsThemeBase.Dark);
        api.ThemeAccent.Should().Be("Blue");
#pragma warning disable CS0618 // the server still maps the deprecated language
        api.Language.Should().Be("1033");
#pragma warning restore CS0618
        api.ConnectOnStartup.Should().BeTrue();
        api.ShowOverlayTitle.Should().BeTrue();
        api.SnapshotPath.Should().Be(@"C:\snapshots");
        api.DefaultPort.Should().Be(554);
        api.VideoQuality.Should().Be("Auto");
        api.MotionSensitivity.Should().Be(30);
        api.RecordingPath.Should().Be(@"C:\recordings");
        api.RecordingFormat.Should().Be(AppSettingsRecordingFormat.Mp4);
        api.EnableDebugLogging.Should().BeTrue();
        api.LogPath.Should().Be(@"C:\logs");
    }

    [Fact]
    public void ToApiModel_LightTheme_MapsCorrectly()
    {
        // Arrange
        var general = new GeneralSettings { ThemeBase = "Light" };

        // Act
        var api = SettingsMappingExtensions.ToApiModel(
            general,
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            new RecordingSettings(),
            new AdvancedSettings());

        // Assert
        api.ThemeBase.Should().Be(AppSettingsThemeBase.Light);
    }

    [Theory]
    [InlineData("mp4", AppSettingsRecordingFormat.Mp4)]
    [InlineData("mkv", AppSettingsRecordingFormat.Mkv)]
    [InlineData("avi", AppSettingsRecordingFormat.Avi)]
    [InlineData("MKV", AppSettingsRecordingFormat.Mkv)]
    public void ToApiModel_RecordingFormat_ParsesCorrectly(
        string format,
        AppSettingsRecordingFormat expected)
    {
        // Arrange
        var recording = new RecordingSettings { RecordingFormat = format };

        // Act
        var api = SettingsMappingExtensions.ToApiModel(
            new GeneralSettings(),
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            recording,
            new AdvancedSettings());

        // Assert
        api.RecordingFormat.Should().Be(expected);
    }

    [Fact]
    public void ApplyToCore_AppliesAllFields()
    {
        // Arrange
        var general = new GeneralSettings();
        var cameraDisplay = new CameraDisplayAppSettings();
        var connection = new ConnectionAppSettings();
        var performance = new PerformanceSettings();
        var motionDetection = new MotionDetectionSettings();
        var recording = new RecordingSettings();
        var advanced = new AdvancedSettings();

        var api = CreateFullUpdateRequest(
            themeBase: AppSettingsThemeBase.Light,
            language: "1030",
            connectOnStartup: false,
            recordingPath: @"D:\rec",
            recordingFormat: AppSettingsRecordingFormat.Mkv,
            enableDebugLogging: true,
            logPath: @"D:\logs");

        // Act
        api.ApplyToCore(general, cameraDisplay, connection, performance, motionDetection, recording, advanced);

        // Assert
        general.ThemeBase.Should().Be("Light");
        general.Language.Should().Be("1030");
        general.ConnectCamerasOnStartup.Should().BeFalse();
        recording.RecordingPath.Should().Be(@"D:\rec");
        recording.RecordingFormat.Should().Be("mkv");
        advanced.EnableDebugLogging.Should().BeTrue();
        advanced.LogPath.Should().Be(@"D:\logs");
    }

    [Fact]
    public void ApplyToCore_NullTheme_DoesNotChangeTheme()
    {
        // Arrange
        var general = new GeneralSettings { ThemeBase = "Dark" };

        var api = CreateFullUpdateRequest(themeBase: null);

        // Act
        api.ApplyToCore(
            general,
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            new RecordingSettings(),
            new AdvancedSettings());

        // Assert
        general.ThemeBase.Should().Be("Dark");
    }

    [Fact]
    public void ConnectionTypeIcon_Settings_RoundTrip()
    {
        // Arrange
        var cameraDisplay = new CameraDisplayAppSettings
        {
            ShowOverlayConnectionTypeIcon = true,
            ConnectionTypeIconPlacement = ConnectionTypeIconPlacement.SameColumn,
        };

        // Act
        var api = SettingsMappingExtensions.ToApiModel(
            new GeneralSettings(),
            cameraDisplay,
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            new RecordingSettings(),
            new AdvancedSettings());
        var target = new CameraDisplayAppSettings();
        api.AsUpdateRequest().ApplyToCore(
            new GeneralSettings(),
            target,
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            new RecordingSettings(),
            new AdvancedSettings());

        // Assert
        api.ShowOverlayConnectionTypeIcon.Should().BeTrue();
        api.ConnectionTypeIconPlacement.Should().Be(AppSettingsConnectionTypeIconPlacement.SameColumn);
        target.ShowOverlayConnectionTypeIcon.Should().BeTrue();
        target.ConnectionTypeIconPlacement.Should().Be(ConnectionTypeIconPlacement.SameColumn);
    }

    [Fact]
    public void RecordingLayout_Settings_RoundTrip()
    {
        // Arrange
        // Both differ from their defaults (on).
        var recording = new RecordingSettings
        {
            UseDailySubfolders = false,
            SeparateThumbnailFolder = false,
        };

        // Act
        var api = SettingsMappingExtensions.ToApiModel(
            new GeneralSettings(),
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            recording,
            new AdvancedSettings());
        var target = new RecordingSettings();
        api.AsUpdateRequest().ApplyToCore(
            new GeneralSettings(),
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            target,
            new AdvancedSettings());

        // Assert
        api.UseDailySubfolders.Should().BeFalse();
        api.SeparateThumbnailFolder.Should().BeFalse();
        target.UseDailySubfolders.Should().BeFalse();
        target.SeparateThumbnailFolder.Should().BeFalse();
    }

    [Fact]
    public void ApplyToCore_Keeps_The_Recording_Layout_When_The_Request_Does_Not_Mention_It()
    {
        // Arrange — changing the layout moves files, so a client that does
        // not know the two properties (and sends nothing) must not change it
        var api = CreateFullUpdateRequest();
        var recording = new RecordingSettings
        {
            UseDailySubfolders = false,
            SeparateThumbnailFolder = false,
        };

        // Act
        api.ApplyToCore(
            new GeneralSettings(),
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            recording,
            new AdvancedSettings());

        // Assert
        recording.UseDailySubfolders.Should().BeFalse();
        recording.SeparateThumbnailFolder.Should().BeFalse();
    }

    [Fact]
    public void DiskSpaceGuard_Settings_RoundTrip()
    {
        // Arrange — both values differ from their defaults (on, 2048 MB)
        var recording = new RecordingSettings();
        recording.Cleanup.EnableDiskSpaceGuard = false;
        recording.Cleanup.MinFreeSpaceMb = 5000;

        // Act
        var api = SettingsMappingExtensions.ToApiModel(
            new GeneralSettings(),
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            recording,
            new AdvancedSettings());
        var target = new RecordingSettings();
        api.AsUpdateRequest().ApplyToCore(
            new GeneralSettings(),
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            target,
            new AdvancedSettings());

        // Assert
        api.EnableDiskSpaceGuard.Should().BeFalse();
        api.MinFreeSpaceMb.Should().Be(5000);
        target.Cleanup.EnableDiskSpaceGuard.Should().BeFalse();
        target.Cleanup.MinFreeSpaceMb.Should().Be(5000);
    }

    [Fact]
    public void ApplyToCore_Keeps_The_Disk_Space_Guard_Off_When_The_Request_Does_Not_Mention_It()
    {
        // Arrange — an older client does not know the property and sends nothing
        var api = CreateFullUpdateRequest();
        var recording = new RecordingSettings();
        recording.Cleanup.EnableDiskSpaceGuard = false;

        // Act
        api.ApplyToCore(
            new GeneralSettings(),
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            recording,
            new AdvancedSettings());

        // Assert
        recording.Cleanup.EnableDiskSpaceGuard.Should().BeFalse();
    }

    [Fact]
    public void ApplyToCore_Keeps_The_Free_Space_Threshold_When_The_Request_Has_None()
    {
        // Arrange — a client that does not send the value sends 0
        var api = CreateFullUpdateRequest();
        var recording = new RecordingSettings();
        recording.Cleanup.MinFreeSpaceMb = 4096;

        // Act
        api.ApplyToCore(
            new GeneralSettings(),
            new CameraDisplayAppSettings(),
            new ConnectionAppSettings(),
            new PerformanceSettings(),
            new MotionDetectionSettings(),
            recording,
            new AdvancedSettings());

        // Assert
        recording.Cleanup.MinFreeSpaceMb.Should().Be(4096);
    }

    private static UpdateSettingsRequest CreateFullUpdateRequest(
        AppSettingsThemeBase? themeBase = AppSettingsThemeBase.Dark,
        string language = "1033",
        bool connectOnStartup = true,
        string recordingPath = "",
        AppSettingsRecordingFormat? recordingFormat = AppSettingsRecordingFormat.Mp4,
        bool enableDebugLogging = false,
        string logPath = "")
        => new(
            ThemeBase: themeBase,
            ThemeAccent: "Blue",
            Language: language,
            ConnectOnStartup: connectOnStartup,
            StartMaximized: false,
            ShowOverlayTitle: true,
            ShowOverlayDescription: true,
            ShowOverlayTime: false,
            ShowOverlayConnectionStatus: true,
            ShowOverlayQuickActions: null,
            ShowOverlayConnectionTypeIcon: false,
            ConnectionTypeIconPlacement: null,
            OverlayOpacity: 0.7,
            OverlayPosition: AppSettingsOverlayPosition.TopLeft,
            AllowDragAndDropReorder: true,
            AutoSaveLayoutChanges: true,
            SnapshotPath: string.Empty,
            DefaultProtocol: AppSettingsDefaultProtocol.Rtsp,
            DefaultPort: 554,
            ConnectionTimeoutSeconds: 10,
            ReconnectDelaySeconds: 5,
            AutoReconnectOnFailure: true,
            ShowNotificationOnDisconnect: true,
            ShowNotificationOnReconnect: true,
            PlayNotificationSound: false,
            VideoQuality: "Auto",
            HardwareAcceleration: true,
            LowLatencyMode: false,
            BufferDurationMs: 500,
            RtspTransport: AppSettingsRtspTransport.Tcp,
            MaxLatencyMs: 1000,
            MotionSensitivity: 30,
            MinimumChangePercent: 0.5,
            AnalysisFrameRate: 5,
            AnalysisWidth: 320,
            AnalysisHeight: 240,
            PostMotionDurationSeconds: 5,
            CooldownSeconds: 3,
            BoundingBoxShowInGrid: false,
            BoundingBoxShowInFullScreen: true,
            BoundingBoxColor: "#FF0000",
            BoundingBoxThickness: 2,
            BoundingBoxMinArea: 500,
            BoundingBoxPadding: 10,
            BoundingBoxSmoothing: 0.3,
            RecordingPath: recordingPath,
            RecordingFormat: recordingFormat,
            RecordAsH264: false,
            EnableRecordingOnMotion: false,
            EnableRecordingOnConnect: false,
            EnableHourlySegmentation: false,
            MaxRecordingDurationMinutes: 60,
            ThumbnailTileCount: 4,
            UseDailySubfolders: null,
            SeparateThumbnailFolder: null,
            EnableTimelapse: false,
            TimelapseInterval: "5m",
            CleanupSchedule: AppSettingsCleanupSchedule.Disabled,
            RecordingRetentionDays: 30,
            CleanupIncludeSnapshots: false,
            SnapshotRetentionDays: 30,
            EnableDiskSpaceGuard: null,
            MinFreeSpaceMb: 0,
            PlaybackShowFilename: true,
            PlaybackFilenameColor: "#FFFFFF",
            PlaybackShowTimestamp: true,
            PlaybackTimestampColor: "#FFFFFF",
            EnableDebugLogging: enableDebugLogging,
            LogPath: logPath);
}