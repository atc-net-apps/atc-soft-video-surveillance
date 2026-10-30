namespace AtcSoft.CameraWall.Wpf.Services;

public partial class MediaCleanupService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Cleanup already in progress, skipping")]
    private partial void LogCleanupAlreadyInProgress();

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting media cleanup - Recording retention: {RecordingDays} days, Include snapshots: {IncludeSnapshots}, Snapshot retention: {SnapshotDays} days")]
    private partial void LogCleanupStarting(int recordingDays, bool includeSnapshots, int snapshotDays);

    [LoggerMessage(Level = LogLevel.Information, Message = "Media cleanup completed - Recordings: {Recordings}, Snapshots: {Snapshots}, Thumbnails: {Thumbnails}, Directories: {Directories}, Bytes freed: {Bytes}, Errors: {Errors}")]
    private partial void LogCleanupCompleted(int recordings, int snapshots, int thumbnails, int directories, string bytes, int errors);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to enumerate active recording sessions during cleanup; falling back to deleting all eligible files")]
    private partial void LogActiveSessionLookupFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File enumeration failed in {Path}; cleanup batch aborted")]
    private partial void LogEnumerationFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted thumbnail: {File}")]
    private partial void LogDeletedThumbnail(string file);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted old media file: {File}")]
    private partial void LogDeletedOldMediaFile(string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete file: {File}")]
    private partial void LogFailedToDeleteFile(Exception ex, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Access denied deleting file: {File}")]
    private partial void LogAccessDeniedDeletingFile(Exception ex, string file);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed empty directory: {Directory}")]
    private partial void LogRemovedEmptyDirectory(string directory);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not remove directory: {Directory}")]
    private partial void LogCouldNotRemoveDirectory(Exception ex, string directory);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Access denied removing directory: {Directory}")]
    private partial void LogAccessDeniedRemovingDirectory(Exception ex, string directory);
}