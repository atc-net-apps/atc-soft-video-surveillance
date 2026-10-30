namespace AtcSoft.VideoSurveillance.Blazor.App.Pages;

using Core = AtcSoft.VideoSurveillance.Models.Settings;

public sealed class SettingsStateTests
{
    [Fact]
    public void The_Starting_Values_Are_The_Servers_Defaults()
    {
        // Shown until the settings have loaded, and used for anything the
        // server leaves out; they must not disagree with a fresh server.
        var sut = new SettingsState();
        var general = new Core.GeneralSettings();
        var display = new Core.CameraDisplayAppSettings();
        var connection = new Core.ConnectionAppSettings();
        var performance = new Core.PerformanceSettings();
        var motion = new Core.MotionDetectionSettings();
        var recording = new Core.RecordingSettings();

        sut.ConnectOnStartup.Should().Be(general.ConnectCamerasOnStartup);
        sut.ShowOverlayQuickActions.Should().Be(display.ShowOverlayQuickActions);
        sut.ReconnectDelaySeconds.Should().Be(connection.ReconnectDelaySeconds);
        sut.ShowNotificationOnReconnect.Should().Be(connection.ShowNotificationOnReconnect);
        sut.VideoQuality.Should().Be(performance.VideoQuality);
        sut.MaxLatencyMs.Should().Be(performance.MaxLatencyMs);
        sut.SmallestObjectPercent.Should().Be(motion.SmallestObjectPercent);
        sut.StoppedObjectSeconds.Should().Be(motion.StoppedObjectSeconds);
        sut.IgnoreLightingChanges.Should().Be(motion.IgnoreLightingChanges);
        sut.AnalysisFrameRate.Should().Be(motion.AnalysisFrameRate);
        sut.AnalysisWidth.Should().Be(motion.AnalysisWidth);
        sut.PostMotionDurationSeconds.Should().Be(motion.PostMotionDurationSeconds);
        sut.CooldownSeconds.Should().Be(motion.CooldownSeconds);
        sut.BoundingBoxShowInFullScreen.Should().Be(motion.BoundingBox.ShowInFullScreen);
        sut.BoundingBoxColor.Should().Be(motion.BoundingBox.Color);
        sut.BoundingBoxPadding.Should().Be(motion.BoundingBox.Padding);
        sut.RecordingFormat.ToString().Should().BeEquivalentTo(recording.RecordingFormat);
        sut.EnableHourlySegmentation.Should().Be(recording.EnableHourlySegmentation);
        sut.MaxRecordingDurationMinutes.Should().Be(recording.MaxRecordingDurationMinutes);
        sut.ThumbnailTileCount.Should().Be(recording.ThumbnailTileCount);
        sut.UseDailySubfolders.Should().Be(recording.UseDailySubfolders);
        sut.SeparateThumbnailFolder.Should().Be(recording.SeparateThumbnailFolder);
        sut.TimelapseInterval.Should().Be(recording.TimelapseInterval);
        sut.CleanupSchedule.ToString().Should().BeEquivalentTo(recording.Cleanup.Schedule.ToString());
        sut.RecordingRetentionDays.Should().Be(recording.Cleanup.RecordingRetentionDays);
        sut.SnapshotRetentionDays.Should().Be(recording.Cleanup.SnapshotRetentionDays);
        sut.MinFreeSpaceMb.Should().Be(recording.Cleanup.MinFreeSpaceMb);
        sut.MaxRecordingStorageGb.Should().Be(recording.Cleanup.MaxRecordingStorageGb);
        sut.PlaybackFilenameColor.Should().Be(recording.PlaybackOverlay.FilenameColor);
        sut.PlaybackTimestampColor.Should().Be(recording.PlaybackOverlay.TimestampColor);
    }

    [Fact]
    public void What_Was_Loaded_Is_What_Is_Saved()
    {
        var loaded = CreateSettings();
        var sut = new SettingsState();

        sut.LoadFrom(loaded);

        // The language is each client's own, not the server's.
#pragma warning disable CS0618 // the web client leaves the deprecated language and detector settings out
        sut.ToApiModel().Should().BeEquivalentTo(
            loaded,
            options => options
                .Excluding(settings => settings.Language)
                .Excluding(settings => settings.MinimumChangePercent)
                .Excluding(settings => settings.AnalysisHeight)
                .Excluding(settings => settings.BoundingBoxMinArea));
#pragma warning restore CS0618
    }

    [Fact]
    public void The_Web_Client_Leaves_The_Servers_Language_Alone()
    {
        var sut = new SettingsState();
        sut.LoadFrom(CreateSettings());

        // Absent = unchanged; the web client's language is the browser's.
#pragma warning disable CS0618 // the web client leaves the deprecated language out
        sut.ToApiModel().Language.Should().BeNull();
#pragma warning restore CS0618
    }

    [Fact]
    public void The_Web_Client_Leaves_The_Old_Detectors_Settings_Alone()
    {
        var sut = new SettingsState();
        sut.LoadFrom(CreateSettings());

        // Absent = unchanged: only the comparison detector reads them.
#pragma warning disable CS0618 // deprecated in the spec
        sut.ToApiModel().MinimumChangePercent.Should().BeNull();
        sut.ToApiModel().BoundingBoxMinArea.Should().BeNull();
#pragma warning restore CS0618
    }

    [Fact]
    public void Colours_Saved_As_Hex_By_An_Older_Web_Client_Are_Loaded_As_Names()
    {
        var sut = new SettingsState();

        sut.LoadFrom(CreateSettings() with
        {
            BoundingBoxColor = "#FF0000",
            PlaybackFilenameColor = "#FFFFFF",
            PlaybackTimestampColor = "#ffff00",
        });

        sut.BoundingBoxColor.Should().Be("Red");
        sut.PlaybackFilenameColor.Should().Be("White");
        sut.PlaybackTimestampColor.Should().Be("Yellow");
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(2, 4)]
    [InlineData(16, 4)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    public void A_Thumbnail_Is_One_Tile_Or_Four(
        int loaded,
        int expected)
    {
        var sut = new SettingsState();

        sut.LoadFrom(CreateSettings() with { ThumbnailTileCount = loaded });

        sut.ThumbnailTileCount.Should().Be(expected);
    }

    [Fact]
    public void The_Analysis_Size_Is_The_Longer_Side_And_The_Height_Is_Left_To_The_Server()
    {
        var sut = new SettingsState();
        sut.LoadFrom(CreateSettings());

        sut.AnalysisWidth.Should().Be(640);
#pragma warning disable CS0618 // deprecated in the spec: the height follows the picture
        sut.ToApiModel().AnalysisHeight.Should().BeNull();
#pragma warning restore CS0618
    }

    [Fact]
    public void The_Layout_Has_Changed_When_A_Folder_Switch_Differs_From_What_Was_Loaded()
    {
        var sut = new SettingsState();
        sut.LoadFrom(CreateSettings());
        sut.HasRecordingLayoutChanged.Should().BeFalse();

        sut.UseDailySubfolders = !sut.UseDailySubfolders;

        sut.HasRecordingLayoutChanged.Should().BeTrue();
    }

    [Fact]
    public void The_Example_Follows_The_Switches_And_The_Format()
    {
        var sut = new SettingsState();
        sut.LoadFrom(CreateSettings());

        sut.UseDailySubfolders = false;
        sut.SeparateThumbnailFolder = false;
        sut.RecordingFormat = AppSettingsRecordingFormat.Mp4;

        sut.RecordingPathExample.Should().StartWith(@"Cam1\Cam1_").And.EndWith(".mp4");
        sut.ThumbnailPathExample.Should().StartWith(@"Cam1\Cam1_").And.EndWith(".png");
    }

    private static AppSettings CreateSettings()
        => new(
            ThemeBase: AppSettingsThemeBase.Light,
            ThemeAccent: "Green",
            Language: "1030",
            ConnectOnStartup: false,
            StartMaximized: true,
            ShowOverlayTitle: false,
            ShowOverlayDescription: false,
            ShowOverlayTime: true,
            ShowOverlayConnectionStatus: false,
            ShowOverlayQuickActions: false,
            ShowOverlayConnectionTypeIcon: true,
            ConnectionTypeIconPlacement: AppSettingsConnectionTypeIconPlacement.SameColumn,
            OverlayOpacity: 0.4,
            OverlayPosition: AppSettingsOverlayPosition.BottomRight,
            AllowDragAndDropReorder: false,
            AutoSaveLayoutChanges: false,
            SnapshotPath: @"D:\media\snapshots",
            DefaultProtocol: AppSettingsDefaultProtocol.Http,
            DefaultPort: 8080,
            ConnectionTimeoutSeconds: 20,
            ReconnectDelaySeconds: 30,
            AutoReconnectOnFailure: false,
            ShowNotificationOnDisconnect: false,
            ShowNotificationOnReconnect: true,
            PlayNotificationSound: true,
            VideoQuality: "720p",
            HardwareAcceleration: false,
            LowLatencyMode: true,
            BufferDurationMs: 250,
            RtspTransport: AppSettingsRtspTransport.Udp,
            MaxLatencyMs: 750,
            MotionSensitivity: 50,
            SmallestObjectPercent: 0.1,
            StoppedObjectSeconds: 120,
            IgnoreLightingChanges: false,
            MinimumChangePercent: 1.5,
            AnalysisFrameRate: 10,
            AnalysisWidth: 640,
            AnalysisHeight: 480,
            PostMotionDurationSeconds: 15,
            CooldownSeconds: 7,
            BoundingBoxShowInGrid: true,
            BoundingBoxShowInFullScreen: true,
            BoundingBoxColor: "Lime",
            BoundingBoxThickness: 3,
            BoundingBoxMinArea: 50,
            BoundingBoxPadding: 6,
            BoundingBoxSmoothing: 0.5,
            RecordingPath: @"D:\media\recordings",
            RecordingFormat: AppSettingsRecordingFormat.Mp4,
            RecordAsH264: false,
            EnableRecordingOnMotion: true,
            EnableRecordingOnConnect: true,
            EnableActivityIndex: false,
            EnableHourlySegmentation: false,
            MaxRecordingDurationMinutes: 30,
            ThumbnailTileCount: 1,
            UseDailySubfolders: false,
            SeparateThumbnailFolder: false,
            EnableTimelapse: true,
            TimelapseInterval: "10m",
            CleanupSchedule: AppSettingsCleanupSchedule.OnStartup,
            RecordingRetentionDays: 14,
            CleanupIncludeSnapshots: true,
            SnapshotRetentionDays: 60,
            EnableDiskSpaceGuard: false,
            MinFreeSpaceMb: 5120,
            MaxRecordingStorageGb: 250,
            PlaybackShowFilename: false,
            PlaybackFilenameColor: "Yellow",
            PlaybackShowTimestamp: false,
            PlaybackTimestampColor: "Aqua",
            EnableDebugLogging: true,
            LogPath: @"D:\media\logs");
}