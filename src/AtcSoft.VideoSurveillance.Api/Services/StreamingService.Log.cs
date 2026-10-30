namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class StreamingService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "HLS stream stopped for camera {CameraId} (no viewers)")]
    private partial void LogHlsStreamStopped(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting FFmpeg for camera {CameraId}: ffmpeg {Args}")]
    private partial void LogStartingFfmpeg(Guid cameraId, string args);

    [LoggerMessage(Level = LogLevel.Information, Message = "[FFmpeg {CameraId}] {Line}")]
    private partial void LogFfmpegOutput(Guid cameraId, string line);

    [LoggerMessage(Level = LogLevel.Information, Message = "HLS stream started for camera {CameraId} -> {PlaylistPath}")]
    private partial void LogHlsStreamStarted(Guid cameraId, string playlistPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "HLS stream started for camera {CameraId} from the pipeline the server has open -> {PlaylistPath}")]
    private partial void LogHlsStreamStartedFromPipeline(Guid cameraId, string playlistPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "HLS stream reaped for camera {CameraId} (idle past inactivity timeout)")]
    private partial void LogHlsStreamReaped(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HLS stream reaper failed to dispose session for camera {CameraId}")]
    private partial void LogHlsStreamReapFailed(Exception ex, Guid cameraId);
}