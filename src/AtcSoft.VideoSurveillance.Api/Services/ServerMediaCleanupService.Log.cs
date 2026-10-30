namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class ServerMediaCleanupService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Cleanup pass starting - Recording retention: {RecordingDays} days, Include snapshots: {IncludeSnapshots}, Snapshot retention: {SnapshotDays} days")]
    private partial void LogCleanupStarting(int recordingDays, bool includeSnapshots, int snapshotDays);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleanup pass complete - Recordings: {Recordings}, Snapshots: {Snapshots}, Thumbnails: {Thumbnails}, Directories: {Directories}, Bytes freed: {Bytes}, Errors: {Errors}")]
    private partial void LogCleanupCompleted(int recordings, int snapshots, int thumbnails, int directories, string bytes, int errors);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted media file: {File}")]
    private partial void LogDeletedFile(string file);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted thumbnail: {File}")]
    private partial void LogDeletedThumbnail(string file);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed empty directory: {Directory}")]
    private partial void LogRemovedEmptyDirectory(string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete file: {File}")]
    private partial void LogFileError(Exception ex, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove directory: {Directory}")]
    private partial void LogDirectoryError(Exception ex, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to enumerate active recording sessions; cleanup may delete an active recording")]
    private partial void LogActiveSessionLookupFailed(Exception ex);
}