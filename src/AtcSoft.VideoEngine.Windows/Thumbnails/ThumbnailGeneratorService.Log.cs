namespace AtcSoft.VideoEngine.Windows.Thumbnails;

public partial class ThumbnailGeneratorService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting thumbnail capture for camera '{CameraName}', output: {ThumbnailPath}, tiles: {TileCount}")]
    private partial void LogStartingThumbnailCapture(string cameraName, string thumbnailPath, int tileCount);

    // Demoted from Warning: a missed first capture is expected — the pipeline
    // hasn't decoded a frame yet when StartCapture runs at recording start.
    // The actual "this thumbnail is broken" condition is covered by
    // LogNoFramesCaptured, which fires only if *every* attempt returned null.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Snapshot capture returned no data for camera '{CameraName}'")]
    private partial void LogSnapshotCaptureNoData(string cameraName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Captured frame {FrameIndex} for camera '{CameraName}'")]
    private partial void LogCapturedFrame(int frameIndex, string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to capture frame for camera '{CameraName}'")]
    private partial void LogFrameCaptureFailed(Exception ex, string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No frames captured for camera '{CameraName}', skipping thumbnail generation")]
    private partial void LogNoFramesCaptured(string cameraName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated thumbnail for camera '{CameraName}' with {FrameCount}/{TotalFrames} frames ({TileCount} tiles): {ThumbnailPath}")]
    private partial void LogGeneratedThumbnail(string cameraName, int frameCount, int totalFrames, int tileCount, string thumbnailPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to generate thumbnail for camera '{CameraName}'")]
    private partial void LogThumbnailGenerationFailed(Exception ex, string cameraName);
}