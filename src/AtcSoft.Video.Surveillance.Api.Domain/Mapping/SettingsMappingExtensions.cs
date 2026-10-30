using CoreAdvancedSettings = AtcSoft.Video.Surveillance.Models.Settings.AdvancedSettings;
using CoreCameraDisplayAppSettings = AtcSoft.Video.Surveillance.Models.Settings.CameraDisplayAppSettings;
using CoreConnectionAppSettings = AtcSoft.Video.Surveillance.Models.Settings.ConnectionAppSettings;
using CoreGeneralSettings = AtcSoft.Video.Surveillance.Models.Settings.GeneralSettings;
using CoreMotionDetectionSettings = AtcSoft.Video.Surveillance.Models.Settings.MotionDetectionSettings;
using CorePerformanceSettings = AtcSoft.Video.Surveillance.Models.Settings.PerformanceSettings;
using CoreRecordingSettings = AtcSoft.Video.Surveillance.Models.Settings.RecordingSettings;

namespace AtcSoft.Video.Surveillance.Api.Domain.Mapping;

internal static class SettingsMappingExtensions
{
    public static AppSettings ToApiModel(
        CoreGeneralSettings general,
        CoreCameraDisplayAppSettings cameraDisplay,
        CoreConnectionAppSettings connection,
        CorePerformanceSettings performance,
        CoreMotionDetectionSettings motionDetection,
        CoreRecordingSettings recording,
        CoreAdvancedSettings advanced)
        => new(
            ThemeBase: ParseThemeBase(general.ThemeBase),
            ThemeAccent: general.ThemeAccent,
            Language: general.Language,
            ConnectOnStartup: general.ConnectCamerasOnStartup,
            StartMaximized: general.StartMaximized,
            ShowOverlayTitle: cameraDisplay.ShowOverlayTitle,
            ShowOverlayDescription: cameraDisplay.ShowOverlayDescription,
            ShowOverlayTime: cameraDisplay.ShowOverlayTime,
            ShowOverlayConnectionStatus: cameraDisplay.ShowOverlayConnectionStatus,
            ShowOverlayQuickActions: cameraDisplay.ShowOverlayQuickActions,
            ShowOverlayConnectionTypeIcon: cameraDisplay.ShowOverlayConnectionTypeIcon,
            ConnectionTypeIconPlacement: ToApiConnectionTypeIconPlacement(cameraDisplay.ConnectionTypeIconPlacement),
            OverlayOpacity: cameraDisplay.OverlayOpacity,
            OverlayPosition: ParseOverlayPosition(cameraDisplay.OverlayPosition),
            AllowDragAndDropReorder: cameraDisplay.AllowDragAndDropReorder,
            AutoSaveLayoutChanges: cameraDisplay.AutoSaveLayoutChanges,
            SnapshotPath: cameraDisplay.SnapshotPath,
            DefaultProtocol: ParseDefaultProtocol(connection.DefaultProtocol),
            DefaultPort: connection.DefaultPort,
            ConnectionTimeoutSeconds: connection.ConnectionTimeoutSeconds,
            ReconnectDelaySeconds: connection.ReconnectDelaySeconds,
            AutoReconnectOnFailure: connection.AutoReconnectOnFailure,
            ShowNotificationOnDisconnect: connection.ShowNotificationOnDisconnect,
            ShowNotificationOnReconnect: connection.ShowNotificationOnReconnect,
            PlayNotificationSound: connection.PlayNotificationSound,
            VideoQuality: VideoQualityHelper.Normalize(performance.VideoQuality) ?? VideoQualityHelper.Auto,
            HardwareAcceleration: performance.HardwareAcceleration,
            LowLatencyMode: performance.LowLatencyMode,
            BufferDurationMs: performance.BufferDurationMs,
            RtspTransport: ParseRtspTransport(performance.RtspTransport),
            MaxLatencyMs: performance.MaxLatencyMs,
            MotionSensitivity: motionDetection.Sensitivity,
            SmallestObjectPercent: motionDetection.SmallestObjectPercent,
            StoppedObjectSeconds: motionDetection.StoppedObjectSeconds,
            IgnoreLightingChanges: motionDetection.IgnoreLightingChanges,
            MinimumChangePercent: motionDetection.MinimumChangePercent,
            AnalysisFrameRate: motionDetection.AnalysisFrameRate,
            AnalysisWidth: motionDetection.AnalysisWidth,
            AnalysisHeight: motionDetection.AnalysisHeight,
            PostMotionDurationSeconds: motionDetection.PostMotionDurationSeconds,
            CooldownSeconds: motionDetection.CooldownSeconds,
            BoundingBoxShowInGrid: motionDetection.BoundingBox.ShowInGrid,
            BoundingBoxShowInFullScreen: motionDetection.BoundingBox.ShowInFullScreen,
            BoundingBoxColor: motionDetection.BoundingBox.Color,
            BoundingBoxThickness: motionDetection.BoundingBox.Thickness,
            BoundingBoxMinArea: motionDetection.BoundingBox.MinArea,
            BoundingBoxPadding: motionDetection.BoundingBox.Padding,
            BoundingBoxSmoothing: motionDetection.BoundingBox.Smoothing,
            RecordingPath: recording.RecordingPath,
            RecordingFormat: ParseRecordingFormat(recording.RecordingFormat),
            RecordAsH264: recording.TranscodeVideoCodec == AtcSoft.Video.Surveillance.Enums.VideoTranscodeCodec.H264,
            EnableRecordingOnMotion: recording.EnableRecordingOnMotion,
            EnableRecordingOnConnect: recording.EnableRecordingOnConnect,
            EnableActivityIndex: recording.EnableActivityIndex,
            EnableHourlySegmentation: recording.EnableHourlySegmentation,
            MaxRecordingDurationMinutes: recording.MaxRecordingDurationMinutes,
            ThumbnailTileCount: recording.ThumbnailTileCount,
            UseDailySubfolders: recording.UseDailySubfolders,
            SeparateThumbnailFolder: recording.SeparateThumbnailFolder,
            EnableTimelapse: recording.EnableTimelapse,
            TimelapseInterval: recording.TimelapseInterval,
            CleanupSchedule: ParseCleanupSchedule(recording.Cleanup.Schedule),
            RecordingRetentionDays: recording.Cleanup.RecordingRetentionDays,
            CleanupIncludeSnapshots: recording.Cleanup.IncludeSnapshots,
            SnapshotRetentionDays: recording.Cleanup.SnapshotRetentionDays,
            EnableDiskSpaceGuard: recording.Cleanup.EnableDiskSpaceGuard,
            MinFreeSpaceMb: recording.Cleanup.MinFreeSpaceMb,
            MaxRecordingStorageGb: recording.Cleanup.MaxRecordingStorageGb,
            PlaybackShowFilename: recording.PlaybackOverlay.ShowFilename,
            PlaybackFilenameColor: recording.PlaybackOverlay.FilenameColor,
            PlaybackShowTimestamp: recording.PlaybackOverlay.ShowTimestamp,
            PlaybackTimestampColor: recording.PlaybackOverlay.TimestampColor,
            EnableDebugLogging: advanced.EnableDebugLogging,
            LogPath: advanced.LogPath);

    /// <summary>
    /// Applies the settings the request has. A setting it leaves out keeps its value,
    /// so a client that does not know a setting yet does not reset it. The limits
    /// (a port from 1, a timeout of at least a second) are in the specification and
    /// checked before the handler runs.
    /// </summary>
    public static void ApplyToCore(
        this UpdateSettingsRequest api,
        CoreGeneralSettings general,
        CoreCameraDisplayAppSettings cameraDisplay,
        CoreConnectionAppSettings connection,
        CorePerformanceSettings performance,
        CoreMotionDetectionSettings motionDetection,
        CoreRecordingSettings recording,
        CoreAdvancedSettings advanced)
    {
        if (api.ThemeBase is not null)
        {
            general.ThemeBase = api.ThemeBase.Value.ToString();
        }

        if (!string.IsNullOrEmpty(api.ThemeAccent))
        {
            general.ThemeAccent = api.ThemeAccent;
        }

        // The language is deprecated in the spec but still stored for older clients.
#pragma warning disable CS0618
        if (!string.IsNullOrEmpty(api.Language))
        {
            general.Language = api.Language;
        }
#pragma warning restore CS0618
        if (api.ConnectOnStartup is { } connectOnStartup)
        {
            general.ConnectCamerasOnStartup = connectOnStartup;
        }

        if (api.StartMaximized is { } startMaximized)
        {
            general.StartMaximized = startMaximized;
        }

        if (api.ShowOverlayTitle is { } showOverlayTitle)
        {
            cameraDisplay.ShowOverlayTitle = showOverlayTitle;
        }

        if (api.ShowOverlayDescription is { } showOverlayDescription)
        {
            cameraDisplay.ShowOverlayDescription = showOverlayDescription;
        }

        if (api.ShowOverlayTime is { } showOverlayTime)
        {
            cameraDisplay.ShowOverlayTime = showOverlayTime;
        }

        if (api.ShowOverlayConnectionStatus is { } showOverlayConnectionStatus)
        {
            cameraDisplay.ShowOverlayConnectionStatus = showOverlayConnectionStatus;
        }

        if (api.ShowOverlayQuickActions is not null)
        {
            cameraDisplay.ShowOverlayQuickActions = api.ShowOverlayQuickActions.Value;
        }

        if (api.ShowOverlayConnectionTypeIcon is { } showOverlayConnectionTypeIcon)
        {
            cameraDisplay.ShowOverlayConnectionTypeIcon = showOverlayConnectionTypeIcon;
        }

        if (api.OverlayOpacity is { } overlayOpacity)
        {
            cameraDisplay.OverlayOpacity = overlayOpacity;
        }

        if (api.ConnectionTypeIconPlacement is not null)
        {
            cameraDisplay.ConnectionTypeIconPlacement = ToCoreConnectionTypeIconPlacement(api.ConnectionTypeIconPlacement.Value);
        }

        if (api.OverlayPosition is not null)
        {
            cameraDisplay.OverlayPosition = ToCoreOverlayPosition(api.OverlayPosition.Value);
        }

        if (api.AllowDragAndDropReorder is { } allowDragAndDropReorder)
        {
            cameraDisplay.AllowDragAndDropReorder = allowDragAndDropReorder;
        }

        if (api.AutoSaveLayoutChanges is { } autoSaveLayoutChanges)
        {
            cameraDisplay.AutoSaveLayoutChanges = autoSaveLayoutChanges;
        }

        if (!string.IsNullOrEmpty(api.SnapshotPath))
        {
            cameraDisplay.SnapshotPath = api.SnapshotPath;
        }

        if (api.DefaultProtocol is not null)
        {
            connection.DefaultProtocol = ToCoreProtocol(api.DefaultProtocol.Value);
        }

        if (api.DefaultPort is { } defaultPort)
        {
            connection.DefaultPort = defaultPort;
        }

        if (api.ConnectionTimeoutSeconds is { } connectionTimeoutSeconds)
        {
            connection.ConnectionTimeoutSeconds = connectionTimeoutSeconds;
        }

        if (api.ReconnectDelaySeconds is { } reconnectDelaySeconds)
        {
            connection.ReconnectDelaySeconds = reconnectDelaySeconds;
        }

        if (api.AutoReconnectOnFailure is { } autoReconnectOnFailure)
        {
            connection.AutoReconnectOnFailure = autoReconnectOnFailure;
        }

        if (api.ShowNotificationOnDisconnect is { } showNotificationOnDisconnect)
        {
            connection.ShowNotificationOnDisconnect = showNotificationOnDisconnect;
        }

        if (api.ShowNotificationOnReconnect is { } showNotificationOnReconnect)
        {
            connection.ShowNotificationOnReconnect = showNotificationOnReconnect;
        }

        if (api.PlayNotificationSound is { } playNotificationSound)
        {
            connection.PlayNotificationSound = playNotificationSound;
        }

        // A value that is not a video quality leaves the setting as it is.
        if (VideoQualityHelper.Normalize(api.VideoQuality) is { } videoQuality)
        {
            performance.VideoQuality = videoQuality;
        }

        if (api.HardwareAcceleration is { } hardwareAcceleration)
        {
            performance.HardwareAcceleration = hardwareAcceleration;
        }

        if (api.LowLatencyMode is { } lowLatencyMode)
        {
            performance.LowLatencyMode = lowLatencyMode;
        }

        if (api.BufferDurationMs is { } bufferDurationMs)
        {
            performance.BufferDurationMs = bufferDurationMs;
        }

        if (api.RtspTransport is not null)
        {
            performance.RtspTransport = api.RtspTransport.Value.ToString().ToLowerInvariant();
        }

        if (api.MaxLatencyMs is { } maxLatencyMs)
        {
            performance.MaxLatencyMs = maxLatencyMs;
        }

        if (api.MotionSensitivity is { } motionSensitivity)
        {
            motionDetection.Sensitivity = motionSensitivity;
        }

        if (api.SmallestObjectPercent is { } smallestObjectPercent)
        {
            motionDetection.SmallestObjectPercent = smallestObjectPercent;
        }

        if (api.StoppedObjectSeconds is { } stoppedObjectSeconds)
        {
            motionDetection.StoppedObjectSeconds = stoppedObjectSeconds;
        }

        if (api.IgnoreLightingChanges is { } ignoreLightingChanges)
        {
            motionDetection.IgnoreLightingChanges = ignoreLightingChanges;
        }

        // Deprecated in the spec (only the comparison detector reads it) but still stored for older clients.
#pragma warning disable CS0618
        if (api.MinimumChangePercent is { } minimumChangePercent)
        {
            motionDetection.MinimumChangePercent = minimumChangePercent;
        }
#pragma warning restore CS0618
        if (api.AnalysisFrameRate is { } analysisFrameRate)
        {
            motionDetection.AnalysisFrameRate = analysisFrameRate;
        }

        if (api.AnalysisWidth is { } analysisWidth)
        {
            motionDetection.AnalysisWidth = analysisWidth;
        }

        // Deprecated in the spec (the height follows the picture) but still stored for older clients.
#pragma warning disable CS0618
        if (api.AnalysisHeight is { } analysisHeight)
        {
            motionDetection.AnalysisHeight = analysisHeight;
        }
#pragma warning restore CS0618
        if (api.PostMotionDurationSeconds is { } postMotionDurationSeconds)
        {
            motionDetection.PostMotionDurationSeconds = postMotionDurationSeconds;
        }

        if (api.CooldownSeconds is { } cooldownSeconds)
        {
            motionDetection.CooldownSeconds = cooldownSeconds;
        }

        if (api.BoundingBoxShowInGrid is { } boundingBoxShowInGrid)
        {
            motionDetection.BoundingBox.ShowInGrid = boundingBoxShowInGrid;
        }

        if (api.BoundingBoxShowInFullScreen is { } boundingBoxShowInFullScreen)
        {
            motionDetection.BoundingBox.ShowInFullScreen = boundingBoxShowInFullScreen;
        }

        if (!string.IsNullOrEmpty(api.BoundingBoxColor))
        {
            motionDetection.BoundingBox.Color = api.BoundingBoxColor;
        }

        if (api.BoundingBoxThickness is { } boundingBoxThickness)
        {
            motionDetection.BoundingBox.Thickness = boundingBoxThickness;
        }

        // Deprecated in the spec (only the comparison detector reads it) but still stored for older clients.
#pragma warning disable CS0618
        if (api.BoundingBoxMinArea is { } boundingBoxMinArea)
        {
            motionDetection.BoundingBox.MinArea = boundingBoxMinArea;
        }
#pragma warning restore CS0618
        if (api.BoundingBoxPadding is { } boundingBoxPadding)
        {
            motionDetection.BoundingBox.Padding = boundingBoxPadding;
        }

        if (api.BoundingBoxSmoothing is { } boundingBoxSmoothing)
        {
            motionDetection.BoundingBox.Smoothing = boundingBoxSmoothing;
        }

        if (!string.IsNullOrEmpty(api.RecordingPath))
        {
            recording.RecordingPath = api.RecordingPath;
        }

        if (api.RecordingFormat is not null)
        {
            recording.RecordingFormat = api.RecordingFormat.Value.ToString().ToLowerInvariant();
        }

        if (api.RecordAsH264 is { } recordAsH264)
        {
            recording.TranscodeVideoCodec = recordAsH264
                ? AtcSoft.Video.Surveillance.Enums.VideoTranscodeCodec.H264
                : AtcSoft.Video.Surveillance.Enums.VideoTranscodeCodec.None;
        }

        if (api.EnableRecordingOnMotion is { } enableRecordingOnMotion)
        {
            recording.EnableRecordingOnMotion = enableRecordingOnMotion;
        }

        if (api.EnableRecordingOnConnect is { } enableRecordingOnConnect)
        {
            recording.EnableRecordingOnConnect = enableRecordingOnConnect;
        }

        if (api.EnableActivityIndex is { } enableActivityIndex)
        {
            recording.EnableActivityIndex = enableActivityIndex;
        }

        if (api.EnableHourlySegmentation is { } enableHourlySegmentation)
        {
            recording.EnableHourlySegmentation = enableHourlySegmentation;
        }

        if (api.UseDailySubfolders is not null)
        {
            recording.UseDailySubfolders = api.UseDailySubfolders.Value;
        }

        if (api.SeparateThumbnailFolder is not null)
        {
            recording.SeparateThumbnailFolder = api.SeparateThumbnailFolder.Value;
        }

        if (api.MaxRecordingDurationMinutes is { } maxRecordingDurationMinutes)
        {
            recording.MaxRecordingDurationMinutes = maxRecordingDurationMinutes;
        }

        if (api.ThumbnailTileCount is { } thumbnailTileCount)
        {
            recording.ThumbnailTileCount = thumbnailTileCount;
        }

        if (api.EnableTimelapse is { } enableTimelapse)
        {
            recording.EnableTimelapse = enableTimelapse;
        }

        if (!string.IsNullOrEmpty(api.TimelapseInterval))
        {
            recording.TimelapseInterval = api.TimelapseInterval;
        }

        if (api.CleanupSchedule is not null)
        {
            recording.Cleanup.Schedule = ToCoreCleanupSchedule(api.CleanupSchedule.Value);
        }

        if (api.RecordingRetentionDays is { } recordingRetentionDays)
        {
            recording.Cleanup.RecordingRetentionDays = recordingRetentionDays;
        }

        if (api.CleanupIncludeSnapshots is { } cleanupIncludeSnapshots)
        {
            recording.Cleanup.IncludeSnapshots = cleanupIncludeSnapshots;
        }

        if (api.SnapshotRetentionDays is { } snapshotRetentionDays)
        {
            recording.Cleanup.SnapshotRetentionDays = snapshotRetentionDays;
        }

        if (api.EnableDiskSpaceGuard is not null)
        {
            recording.Cleanup.EnableDiskSpaceGuard = api.EnableDiskSpaceGuard.Value;
        }

        if (api.MinFreeSpaceMb > 0)
        {
            recording.Cleanup.MinFreeSpaceMb = api.MinFreeSpaceMb.Value;
        }

        if (api.MaxRecordingStorageGb is { } maxRecordingStorageGb)
        {
            recording.Cleanup.MaxRecordingStorageGb = maxRecordingStorageGb;
        }

        if (api.PlaybackShowFilename is { } playbackShowFilename)
        {
            recording.PlaybackOverlay.ShowFilename = playbackShowFilename;
        }

        if (!string.IsNullOrEmpty(api.PlaybackFilenameColor))
        {
            recording.PlaybackOverlay.FilenameColor = api.PlaybackFilenameColor;
        }

        if (api.PlaybackShowTimestamp is { } playbackShowTimestamp)
        {
            recording.PlaybackOverlay.ShowTimestamp = playbackShowTimestamp;
        }

        if (!string.IsNullOrEmpty(api.PlaybackTimestampColor))
        {
            recording.PlaybackOverlay.TimestampColor = api.PlaybackTimestampColor;
        }

        if (api.EnableDebugLogging is { } enableDebugLogging)
        {
            advanced.EnableDebugLogging = enableDebugLogging;
        }

        if (!string.IsNullOrEmpty(api.LogPath))
        {
            advanced.LogPath = api.LogPath;
        }
    }

    private static AppSettingsThemeBase ParseThemeBase(string? themeBase)
        => string.Equals(themeBase, "Light", StringComparison.OrdinalIgnoreCase)
            ? AppSettingsThemeBase.Light
            : AppSettingsThemeBase.Dark;

    private static AppSettingsRecordingFormat ParseRecordingFormat(
        string? format)
        => format?.ToUpperInvariant() switch
        {
            "MKV" => AppSettingsRecordingFormat.Mkv,
            "AVI" => AppSettingsRecordingFormat.Avi,
            _ => AppSettingsRecordingFormat.Mp4,
        };

    private static AppSettingsConnectionTypeIconPlacement ToApiConnectionTypeIconPlacement(
        AtcSoft.Video.Surveillance.Enums.ConnectionTypeIconPlacement placement)
        => placement == AtcSoft.Video.Surveillance.Enums.ConnectionTypeIconPlacement.SameColumn
            ? AppSettingsConnectionTypeIconPlacement.SameColumn
            : AppSettingsConnectionTypeIconPlacement.SameRow;

    private static AtcSoft.Video.Surveillance.Enums.ConnectionTypeIconPlacement ToCoreConnectionTypeIconPlacement(
        AppSettingsConnectionTypeIconPlacement placement)
        => placement == AppSettingsConnectionTypeIconPlacement.SameColumn
            ? AtcSoft.Video.Surveillance.Enums.ConnectionTypeIconPlacement.SameColumn
            : AtcSoft.Video.Surveillance.Enums.ConnectionTypeIconPlacement.SameRow;

    private static AppSettingsOverlayPosition ParseOverlayPosition(
        AtcSoft.Video.Surveillance.Enums.OverlayPosition position)
        => position switch
        {
            AtcSoft.Video.Surveillance.Enums.OverlayPosition.TopRight => AppSettingsOverlayPosition.TopRight,
            AtcSoft.Video.Surveillance.Enums.OverlayPosition.BottomLeft => AppSettingsOverlayPosition.BottomLeft,
            AtcSoft.Video.Surveillance.Enums.OverlayPosition.BottomRight => AppSettingsOverlayPosition.BottomRight,
            _ => AppSettingsOverlayPosition.TopLeft,
        };

    private static AtcSoft.Video.Surveillance.Enums.OverlayPosition ToCoreOverlayPosition(
        AppSettingsOverlayPosition position)
        => position switch
        {
            AppSettingsOverlayPosition.TopRight => AtcSoft.Video.Surveillance.Enums.OverlayPosition.TopRight,
            AppSettingsOverlayPosition.BottomLeft => AtcSoft.Video.Surveillance.Enums.OverlayPosition.BottomLeft,
            AppSettingsOverlayPosition.BottomRight => AtcSoft.Video.Surveillance.Enums.OverlayPosition.BottomRight,
            _ => AtcSoft.Video.Surveillance.Enums.OverlayPosition.TopLeft,
        };

    private static AppSettingsDefaultProtocol ParseDefaultProtocol(
        AtcSoft.Video.Surveillance.Enums.CameraProtocol protocol)
        => protocol switch
        {
            AtcSoft.Video.Surveillance.Enums.CameraProtocol.Http => AppSettingsDefaultProtocol.Http,
            AtcSoft.Video.Surveillance.Enums.CameraProtocol.Https => AppSettingsDefaultProtocol.Https,
            _ => AppSettingsDefaultProtocol.Rtsp,
        };

    private static AtcSoft.Video.Surveillance.Enums.CameraProtocol ToCoreProtocol(
        AppSettingsDefaultProtocol protocol)
        => protocol switch
        {
            AppSettingsDefaultProtocol.Http => AtcSoft.Video.Surveillance.Enums.CameraProtocol.Http,
            AppSettingsDefaultProtocol.Https => AtcSoft.Video.Surveillance.Enums.CameraProtocol.Https,
            _ => AtcSoft.Video.Surveillance.Enums.CameraProtocol.Rtsp,
        };

    private static AppSettingsRtspTransport ParseRtspTransport(
        string? transport)
        => string.Equals(transport, "udp", StringComparison.OrdinalIgnoreCase)
            ? AppSettingsRtspTransport.Udp
            : AppSettingsRtspTransport.Tcp;

    private static AppSettingsCleanupSchedule ParseCleanupSchedule(
        AtcSoft.Video.Surveillance.Enums.MediaCleanupSchedule schedule)
        => schedule switch
        {
            AtcSoft.Video.Surveillance.Enums.MediaCleanupSchedule.OnStartup => AppSettingsCleanupSchedule.OnStartup,
            AtcSoft.Video.Surveillance.Enums.MediaCleanupSchedule.OnStartupAndPeriodically => AppSettingsCleanupSchedule.OnStartupAndPeriodically,
            _ => AppSettingsCleanupSchedule.Disabled,
        };

    private static AtcSoft.Video.Surveillance.Enums.MediaCleanupSchedule ToCoreCleanupSchedule(
        AppSettingsCleanupSchedule schedule)
        => schedule switch
        {
            AppSettingsCleanupSchedule.OnStartup => AtcSoft.Video.Surveillance.Enums.MediaCleanupSchedule.OnStartup,
            AppSettingsCleanupSchedule.OnStartupAndPeriodically => AtcSoft.Video.Surveillance.Enums.MediaCleanupSchedule.OnStartupAndPeriodically,
            _ => AtcSoft.Video.Surveillance.Enums.MediaCleanupSchedule.Disabled,
        };
}