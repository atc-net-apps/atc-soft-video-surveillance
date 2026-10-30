namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class JsonCameraStorageService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to save camera storage to {Path}")]
    private partial void LogStorageSaveFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {CameraCount} cameras and {LayoutCount} layouts from {Path}")]
    private partial void LogStorageLoaded(int cameraCount, int layoutCount, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load camera storage from {Path}")]
    private partial void LogStorageLoadFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The password of camera {CameraId} ({DisplayName}) cannot be read with this server's keys; enter it again")]
    private partial void LogPasswordUnreadable(string cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Encrypted {Count} camera password(s) kept in plain text in {Path}")]
    private partial void LogPasswordsEncrypted(int count, string path);
}