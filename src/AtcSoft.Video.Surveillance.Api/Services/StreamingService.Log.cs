namespace AtcSoft.Video.Surveillance.Api.Services;

public sealed partial class StreamingService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "HLS stream stopped for camera {CameraId} (no viewers)")]
    private partial void LogHlsStreamStopped(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting FFmpeg for camera {CameraId}: ffmpeg {Args}")]
    private partial void LogStartingFfmpeg(Guid cameraId, string args);

    // FFmpeg writes a line every few seconds per stream: kept out of the log
    // unless debug logging is on. The last lines are kept for the viewer's error.
    [LoggerMessage(Level = LogLevel.Debug, Message = "[FFmpeg {CameraId}] {Line}")]
    private partial void LogFfmpegOutput(Guid cameraId, string line);

    [LoggerMessage(Level = LogLevel.Information, Message = "HLS stream started for camera {CameraId} -> {PlaylistPath}")]
    private partial void LogHlsStreamStarted(Guid cameraId, string playlistPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "HLS stream started for camera {CameraId} from the pipeline the server has open -> {PlaylistPath}")]
    private partial void LogHlsStreamStartedFromPipeline(Guid cameraId, string playlistPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "HLS stream reaped for camera {CameraId} (idle past inactivity timeout)")]
    private partial void LogHlsStreamReaped(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HLS stream reaper failed to dispose session for camera {CameraId}")]
    private partial void LogHlsStreamReapFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No job object for the live-stream FFmpeg processes ({Reason}); one may outlive the server if it is killed, and is then ended at the next start")]
    private partial void LogProcessJobUnavailable(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The live-stream FFmpeg process of camera {CameraId} could not be put in the server's job ({Reason})")]
    private partial void LogProcessNotInJob(Guid cameraId, string reason);
}