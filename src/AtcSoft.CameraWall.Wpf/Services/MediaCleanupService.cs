namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// Service for automatically cleaning up old recordings and snapshots.
/// </summary>
// Not auto-registered via [Registration]: the App registers this explicitly so
// the SAME singleton instance is exposed both as IMediaCleanupService (for the
// UI: events, IsRunning, manual RunCleanupAsync) and as an IHostedService (for
// the Generic Host to drive its periodic DoWorkAsync loop and graceful stop).
public partial class MediaCleanupService : BackgroundServiceBase<MediaCleanupService>, IMediaCleanupService
{
    private static readonly string[] RecordingExtensions = [".mp4", ".mkv", ".avi"];
    private static readonly string[] SnapshotExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    private readonly IApplicationSettingsService settingsService;
    private readonly IRecordingService recordingService;
    private readonly ICameraStorageService? cameraStorage;
    private readonly DiskSpaceGuard diskSpaceGuard;
    private readonly Lock lockObject = new();
    private bool isRunning;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaCleanupService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="settingsService">The application settings service.</param>
    /// <param name="recordingService">The recording service, used to identify and skip files that are currently being written to.</param>
    /// <param name="cameraStorage">The cameras, whose own recording folders are cleaned too.</param>
    /// <param name="options">When the cleanup runs; the defaults when not given.</param>
    /// <param name="diskSpaceGuard">The guard shared with the recording service; it keeps the size limit.</param>
    public MediaCleanupService(
        ILogger<MediaCleanupService> logger,
        IApplicationSettingsService settingsService,
        IRecordingService recordingService,
        ICameraStorageService? cameraStorage = null,
        MediaCleanupServiceOptions? options = null,
        DiskSpaceGuard? diskSpaceGuard = null)
        : base(logger, options ?? new MediaCleanupServiceOptions())
    {
        this.diskSpaceGuard = diskSpaceGuard ?? new DiskSpaceGuard(FreeSpaceReader.Default);
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        this.cameraStorage = cameraStorage;
    }

    /// <inheritdoc/>
    public event EventHandler<MediaCleanupCompletedEventArgs>? CleanupCompleted;

    /// <inheritdoc/>
    public bool IsRunning
    {
        get
        {
            lock (lockObject)
            {
                return isRunning;
            }
        }
    }

    /// <summary>
    /// Periodic cleanup tick driven by the host. The <see cref="MediaCleanupSchedule"/>
    /// is re-read each pass: <c>Disabled</c> is a no-op, <c>OnStartup</c> runs
    /// once then stops the service, and <c>OnStartupAndPeriodically</c> runs on
    /// every tick (the first at startup, via the short startup delay).
    /// </summary>
    /// <param name="stoppingToken">The stopping token.</param>
    public override async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        var schedule = settingsService.Recording.Cleanup.Schedule;
        if (schedule == MediaCleanupSchedule.Disabled)
        {
            return;
        }

        await RunCleanupAsync().ConfigureAwait(false);

        if (schedule == MediaCleanupSchedule.OnStartup)
        {
            await StopAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<MediaCleanupResult> RunCleanupAsync()
    {
        lock (lockObject)
        {
            if (isRunning)
            {
                LogCleanupAlreadyInProgress();
                return new MediaCleanupResult();
            }

            isRunning = true;
        }

        try
        {
            var settings = settingsService.Recording.Cleanup;
            var result = new MediaCleanupResult();

            LogCleanupStarting(
                settings.RecordingRetentionDays,
                settings.IncludeSnapshots,
                settings.SnapshotRetentionDays);

            // Snapshot active recording paths so an in-progress recording is
            // never deleted by the cleanup pass (sharing-violation IOException
            // on Windows; orphaned undeletable file otherwise).
            var activePaths = GetActiveRecordingPaths();

            // Run the actual file-system work on the thread pool — keeps the
            // dispatcher responsive when scanning very large recording trees.
            await Task.Run(() =>
                RunCleanupCore(settings, activePaths, result)).ConfigureAwait(false);

            LogCleanupCompleted(
                result.RecordingsDeleted,
                result.SnapshotsDeleted,
                result.ThumbnailsDeleted,
                result.DirectoriesRemoved,
                FormatBytes(result.BytesFreed),
                result.ErrorCount);

            OnCleanupCompleted(result);

            // Disk-space guard runs after age-based cleanup so the reclaim pass
            // benefits from any files already deleted above.
            await ReclaimBySpaceAsync().ConfigureAwait(false);

            return result;
        }
        finally
        {
            lock (lockObject)
            {
                isRunning = false;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<MediaCleanupResult> RunDiskSpaceReclaimAsync()
    {
        lock (lockObject)
        {
            if (isRunning)
            {
                LogCleanupAlreadyInProgress();
                return new MediaCleanupResult();
            }

            isRunning = true;
        }

        try
        {
            return await ReclaimBySpaceAsync().ConfigureAwait(false);
        }
        finally
        {
            lock (lockObject)
            {
                isRunning = false;
            }
        }
    }

    // The free space of every volume with media is kept by the recording service's guard, which
    // also runs every 30 seconds; this pass runs it once more after the age cleanup.
    private async Task<MediaCleanupResult> ReclaimBySpaceAsync()
    {
        var guard = await Task.Run(recordingService.ReclaimDiskSpace).ConfigureAwait(false) ?? DiskSpaceGuardResult.None;
        var result = new MediaCleanupResult
        {
            RecordingsDeleted = guard.Volumes.Sum(v => v.Run.DeletedFiles.Count),
            ThumbnailsDeleted = guard.Volumes.Sum(v => v.Run.DeletedThumbnails.Count),
            BytesFreed = guard.Volumes.Sum(v => v.Run.BytesFreed),
            ErrorCount = guard.Volumes.Sum(v => v.Run.Errors.Count),
        };

        if (result.RecordingsDeleted > 0)
        {
            OnCleanupCompleted(result);
        }

        return result;
    }

    // Every folder recordings go to: the global one (or its default) and each camera's own.
    private List<string> GetRecordingFolders()
    {
        var global = settingsService.Recording.RecordingPath;
        var folders = new List<string> { string.IsNullOrEmpty(global) ? ApplicationPaths.DefaultRecordingsPath : global };
        folders.AddRange((cameraStorage?.GetAllCameras() ?? [])
            .Select(c => c.Overrides?.Recording.RecordingPath)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f!));
        return folders;
    }

    // The same folders, each once: a folder inside another is cleaned as part of it.
    private IReadOnlyList<string> GetRecordingRoots()
        => RecordingRoots.Collect(null, GetRecordingFolders());

    private void RunCleanupCore(
        MediaCleanupSettings settings,
        HashSet<string> activePaths,
        MediaCleanupResult result)
    {
        // A camera's own folder inside the global one keeps its thumbnails in its own
        // thumbnails folder: the passes read them against that folder.
        var folders = GetRecordingFolders();
        foreach (var recordingPath in GetRecordingRoots().Where(Directory.Exists))
        {
            var run = MediaCleanupRunner.CleanDirectory(
                recordingPath,
                RecordingExtensions,
                DateTime.Now.AddDays(-settings.RecordingRetentionDays),
                activePaths,
                deleteCompanionThumbnail: true,
                nestedRoots: folders);

            ApplyRecordingRunResult(run, result);

            // Thumbnails whose recording was deleted outside the app.
            var orphans = MediaCleanupRunner.RemoveOrphanThumbnails(
                recordingPath,
                RecordingExtensions,
                activePaths,
                MediaCleanupRunner.EmptyDirectoryGracePeriod,
                excludedDirectories: [settingsService.CameraDisplay.SnapshotPath],
                nestedRoots: folders);

            ApplyRecordingRunResult(orphans, result);

            var emptyDirs = MediaCleanupRunner.RemoveEmptyDirectoriesBelow(recordingPath, MediaCleanupRunner.EmptyDirectoryGracePeriod);
            ApplyEmptyDirectoriesResult(emptyDirs, result);
        }

        // All recordings together within the size limit, the oldest first.
        if (settings.MaxRecordingStorageGb > 0)
        {
            var limited = diskSpaceGuard.EnforceSizeLimit(
                folders,
                settings.MaxRecordingStorageGb * 1024L * 1024L * 1024L,
                activePaths,
                RecordingExtensions);
            ApplyRecordingRunResult(limited, result);
        }

        if (!settings.IncludeSnapshots)
        {
            return;
        }

        var snapshotPath = settingsService.CameraDisplay.SnapshotPath;
        if (string.IsNullOrEmpty(snapshotPath))
        {
            return;
        }

        var snapshotRun = MediaCleanupRunner.CleanDirectory(
            snapshotPath,
            SnapshotExtensions,
            DateTime.Now.AddDays(-settings.SnapshotRetentionDays),
            activePaths,
            deleteCompanionThumbnail: false);

        ApplySnapshotRunResult(snapshotRun, result);

        var snapshotEmpty = MediaCleanupRunner.RemoveEmptyDirectoriesBelow(snapshotPath, MediaCleanupRunner.EmptyDirectoryGracePeriod);
        ApplyEmptyDirectoriesResult(snapshotEmpty, result);
    }

    private void ApplyRecordingRunResult(
        MediaCleanupRunResult run,
        MediaCleanupResult result)
    {
        foreach (var file in run.DeletedFiles)
        {
            LogDeletedOldMediaFile(file);
        }

        foreach (var thumb in run.DeletedThumbnails)
        {
            LogDeletedThumbnail(thumb);
        }

        result.RecordingsDeleted += run.DeletedFiles.Count;
        result.ThumbnailsDeleted += run.DeletedThumbnails.Count;
        result.BytesFreed += run.BytesFreed;
        ApplyErrors(run.Errors, result);
    }

    private void ApplySnapshotRunResult(
        MediaCleanupRunResult run,
        MediaCleanupResult result)
    {
        foreach (var file in run.DeletedFiles)
        {
            LogDeletedOldMediaFile(file);
        }

        result.SnapshotsDeleted += run.DeletedFiles.Count;
        result.BytesFreed += run.BytesFreed;
        ApplyErrors(run.Errors, result);
    }

    private void ApplyEmptyDirectoriesResult(
        MediaCleanupDirectoryResult run,
        MediaCleanupResult result)
    {
        foreach (var dir in run.RemovedDirectories)
        {
            LogRemovedEmptyDirectory(dir);
        }

        result.DirectoriesRemoved += run.RemovedDirectories.Count;
        foreach (var error in run.Errors)
        {
            switch (error.Exception)
            {
                case UnauthorizedAccessException uae:
                    LogAccessDeniedRemovingDirectory(uae, error.Path);
                    break;
                default:
                    LogCouldNotRemoveDirectory(error.Exception, error.Path);
                    break;
            }
        }

        result.ErrorCount += run.Errors.Count;
    }

    private void ApplyErrors(
        IReadOnlyList<MediaCleanupRunError> errors,
        MediaCleanupResult result)
    {
        foreach (var error in errors)
        {
            switch (error.Exception)
            {
                case UnauthorizedAccessException uae:
                    LogAccessDeniedDeletingFile(uae, error.Path);
                    break;
                case IOException ioe:
                    LogFailedToDeleteFile(ioe, error.Path);
                    break;
                default:
                    LogFailedToDeleteFile(error.Exception, error.Path);
                    break;
            }
        }

        result.ErrorCount += errors.Count;
    }

    // Builds an unordered, case-insensitive set of file paths currently being
    // written by the recording service. Used to skip in-flight files during
    // cleanup so we never call File.Delete on a handle the muxer still owns.
    private HashSet<string> GetActiveRecordingPaths()
    {
        try
        {
            var files = recordingService.GetActiveSessions().Select(s => s.CurrentFilePath);
            return new HashSet<string>(DiskSpaceGuard.ProtectedPaths(files, GetRecordingFolders()), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            // If we can't enumerate sessions, fail safe by treating no files as active
            // — but at least don't take down the cleanup pass.
            LogActiveSessionLookupFailed(ex);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void OnCleanupCompleted(MediaCleanupResult result)
    {
        CleanupCompleted?.Invoke(this, new MediaCleanupCompletedEventArgs(result));
    }

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F2} GB",
            >= MB => $"{bytes / (double)MB:F2} MB",
            >= KB => $"{bytes / (double)KB:F2} KB",
            _ => $"{bytes} bytes",
        };
    }
}