namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// Keeps the recording folder in line with the folder layout settings of
/// the standalone app: restarts running recordings that are not in place and
/// moves the existing recordings and thumbnails.
/// </summary>
[Registration(Lifetime.Singleton)]
public partial class RecordingLayoutSynchronizer : IRecordingLayoutSynchronizer
{
    private static readonly string[] RecordingFileExtensions = [".mp4", ".mkv", ".avi"];

    private readonly ILogger<RecordingLayoutSynchronizer> logger;
    private readonly IApplicationSettingsService settingsService;
    private readonly IRecordingService recordingService;
    private readonly ICameraStorageService? cameraStorage;

    public RecordingLayoutSynchronizer(
        ILogger<RecordingLayoutSynchronizer> logger,
        IApplicationSettingsService settingsService,
        IRecordingService recordingService,
        ICameraStorageService? cameraStorage = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        this.cameraStorage = cameraStorage;
    }

    /// <inheritdoc />
    public Task<RecordingLayoutReorganizeResult> SynchronizeAsync(
        CancellationToken cancellationToken = default)
    {
        var recording = settingsService.Recording;
        var globalPath = !string.IsNullOrEmpty(recording.RecordingPath)
            ? recording.RecordingPath
            : ApplicationPaths.DefaultRecordingsPath;

        // The global folder and each camera's own folder. A running recording outside the folder
        // being synchronized counts as in place, so each pass restarts only its own.
        var roots = RecordingRoots.Collect(
            globalPath,
            cameraStorage?.GetAllCameras().Select(c => c.Overrides?.Recording.RecordingPath) ?? []);
        var layout = RecordingLayoutOptions.From(recording);
        var activeSessions = recordingService.GetActiveSessions();

        // Moving a large archive takes a while; keep it off the caller's thread.
        return Task.Run(
            () =>
            {
                var movedRecordings = 0;
                var movedThumbnails = 0;
                var errors = new List<MediaCleanupRunError>();
                foreach (var recordingPath in roots)
                {
                    var result = RecordingLayoutReorganizer.Synchronize(
                        recordingPath,
                        layout,
                        RecordingFileExtensions,
                        activeSessions,
                        recordingService.SegmentRecording);

                    if (result.MovedRecordings > 0 || result.MovedThumbnails > 0 || result.Errors.Count > 0)
                    {
                        LogSynchronized(recordingPath, result.MovedRecordings, result.MovedThumbnails, result.Errors.Count);
                    }

                    foreach (var error in result.Errors)
                    {
                        LogMoveFailed(error.Exception, error.Path);
                    }

                    movedRecordings += result.MovedRecordings;
                    movedThumbnails += result.MovedThumbnails;
                    errors.AddRange(result.Errors);
                }

                return new RecordingLayoutReorganizeResult(movedRecordings, movedThumbnails, errors);
            },
            cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording folder '{RecordingPath}' brought in line with the layout settings - recordings moved: {Recordings}, thumbnails moved: {Thumbnails}, errors: {Errors}")]
    private partial void LogSynchronized(string recordingPath, int recordings, int thumbnails, int errors);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not move '{Path}' to the new recording folder layout")]
    private partial void LogMoveFailed(Exception ex, string path);
}