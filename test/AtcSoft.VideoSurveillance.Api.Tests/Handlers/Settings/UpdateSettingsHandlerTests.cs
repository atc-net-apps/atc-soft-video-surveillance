namespace AtcSoft.VideoSurveillance.Api.Handlers.Settings;

public class UpdateSettingsHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_AppliesSettingsAndReturnsOk()
    {
        // Arrange
        var settingsService = Substitute.For<IApplicationSettingsService>();
        var general = new GeneralSettings();
        var cameraDisplay = new CameraDisplayAppSettings();
        var connection = new ConnectionAppSettings();
        var performance = new PerformanceSettings();
        var motionDetection = new MotionDetectionSettings();
        var recording = new RecordingSettings();
        var advanced = new AdvancedSettings();
        settingsService.General.Returns(general);
        settingsService.CameraDisplay.Returns(cameraDisplay);
        settingsService.Connection.Returns(connection);
        settingsService.Performance.Returns(performance);
        settingsService.MotionDetection.Returns(motionDetection);
        settingsService.Recording.Returns(recording);
        settingsService.Advanced.Returns(advanced);
        var handler = new UpdateSettingsHandler(settingsService, Substitute.For<IRecordingService>());

        var apiSettings = CreateFullUpdateRequest(
            themeBase: AppSettingsThemeBase.Light,
            language: "1030",
            enableDebugLogging: true);

        // Act
        var result = await handler.ExecuteAsync(
            new UpdateSettingsParameters(apiSettings),
            CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Ok<AppSettings>>();
        settingsService.Received(1).SaveGeneral(general);
        settingsService.Received(1).SaveCameraDisplay(cameraDisplay);
        settingsService.Received(1).SaveConnection(connection);
        settingsService.Received(1).SavePerformance(performance);
        settingsService.Received(1).SaveMotionDetection(motionDetection);
        settingsService.Received(1).SaveRecording(recording);
        settingsService.Received(1).SaveAdvanced(advanced);
    }

    [Fact]
    public async Task ExecuteAsync_SavesAllSections()
    {
        // Arrange
        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.General.Returns(new GeneralSettings());
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings());
        settingsService.Connection.Returns(new ConnectionAppSettings());
        settingsService.Performance.Returns(new PerformanceSettings());
        settingsService.MotionDetection.Returns(new MotionDetectionSettings());
        settingsService.Recording.Returns(new RecordingSettings());
        settingsService.Advanced.Returns(new AdvancedSettings());
        var handler = new UpdateSettingsHandler(settingsService, Substitute.For<IRecordingService>());

        var apiSettings = CreateFullUpdateRequest();

        // Act
        await handler.ExecuteAsync(
            new UpdateSettingsParameters(apiSettings),
            CancellationToken.None);

        // Assert
        settingsService.Received(1).SaveGeneral(Arg.Any<GeneralSettings>());
        settingsService.Received(1).SaveCameraDisplay(Arg.Any<CameraDisplayAppSettings>());
        settingsService.Received(1).SaveConnection(Arg.Any<ConnectionAppSettings>());
        settingsService.Received(1).SavePerformance(Arg.Any<PerformanceSettings>());
        settingsService.Received(1).SaveMotionDetection(Arg.Any<MotionDetectionSettings>());
        settingsService.Received(1).SaveRecording(Arg.Any<RecordingSettings>());
        settingsService.Received(1).SaveAdvanced(Arg.Any<AdvancedSettings>());
    }

    [Fact]
    public async Task ExecuteAsync_A_Changed_Folder_Layout_Moves_The_Recordings()
    {
        // Arrange — a flat archive, and a request that switches day folders on
        var root = Path.Combine(Path.GetTempPath(), "atcsoft-settings-handler-" + Guid.NewGuid().ToString("N"));
        try
        {
            Touch(Path.Combine(root, "Cam", "Cam_20261012_101500_000.mkv"));
            var (handler, _) = CreateHandler(root, useDailySubfolders: false, separateThumbnailFolder: false);

            // Act
            await handler.ExecuteAsync(
                new UpdateSettingsParameters(CreateFullUpdateRequest() with { RecordingPath = root, UseDailySubfolders = true }),
                CancellationToken.None);

            // Assert — the server names files in UTC; the day folder is the local day
            var day = new DateTime(2026, 10, 12, 10, 15, 0, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            File.Exists(Path.Combine(root, "Cam", day, "Cam_20261012_101500_000.mkv")).Should().BeTrue();
            File.Exists(Path.Combine(root, "Cam", "Cam_20261012_101500_000.mkv")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_A_Changed_Folder_Layout_Restarts_A_Running_Recording()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "atcsoft-settings-handler-" + Guid.NewGuid().ToString("N"));
        try
        {
            var live = Touch(Path.Combine(root, "Cam", "Cam_20261012_101500_000.mkv"));
            var (handler, recordingService) = CreateHandler(root, useDailySubfolders: false, separateThumbnailFolder: false);
            var session = new RecordingSession(Guid.NewGuid(), "Cam", live, isManualRecording: true);
            recordingService.GetActiveSessions().Returns([session]);

            // Act
            await handler.ExecuteAsync(
                new UpdateSettingsParameters(CreateFullUpdateRequest() with { RecordingPath = root, UseDailySubfolders = true }),
                CancellationToken.None);

            // Assert
            recordingService.Received(1).SegmentRecording(session.CameraId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_An_Unchanged_Folder_Layout_Moves_Nothing()
    {
        // Arrange — the request does not mention the layout
        var root = Path.Combine(Path.GetTempPath(), "atcsoft-settings-handler-" + Guid.NewGuid().ToString("N"));
        try
        {
            var flat = Touch(Path.Combine(root, "Cam", "Cam_20261012_101500_000.mkv"));
            var (handler, recordingService) = CreateHandler(root, useDailySubfolders: true, separateThumbnailFolder: true);

            // Act
            await handler.ExecuteAsync(
                new UpdateSettingsParameters(CreateFullUpdateRequest() with { RecordingPath = root }),
                CancellationToken.None);

            // Assert
            File.Exists(flat).Should().BeTrue();
            recordingService.DidNotReceive().SegmentRecording(Arg.Any<Guid>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (UpdateSettingsHandler Handler, IRecordingService RecordingService) CreateHandler(
        string recordingPath,
        bool useDailySubfolders,
        bool separateThumbnailFolder)
    {
        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.General.Returns(new GeneralSettings());
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings());
        settingsService.Connection.Returns(new ConnectionAppSettings());
        settingsService.Performance.Returns(new PerformanceSettings());
        settingsService.MotionDetection.Returns(new MotionDetectionSettings());
        settingsService.Advanced.Returns(new AdvancedSettings());
        settingsService.Recording.Returns(new RecordingSettings
        {
            RecordingPath = recordingPath,
            UseDailySubfolders = useDailySubfolders,
            SeparateThumbnailFolder = separateThumbnailFolder,
        });

        var recordingService = Substitute.For<IRecordingService>();
        recordingService.GetActiveSessions().Returns([]);
        recordingService.SegmentRecording(Arg.Any<Guid>()).Returns(true);

        return (new UpdateSettingsHandler(settingsService, recordingService), recordingService);
    }

    private static string Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    private static UpdateSettingsRequest CreateFullUpdateRequest(
        AppSettingsThemeBase? themeBase = AppSettingsThemeBase.Dark,
        string language = "1033",
        bool enableDebugLogging = false)
        => new(
            ThemeBase: themeBase,
            ThemeAccent: "Blue",
            Language: language,
            ConnectOnStartup: true,
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
            RecordingPath: @"C:\recordings",
            RecordingFormat: AppSettingsRecordingFormat.Mp4,
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
            LogPath: @"C:\logs");
}