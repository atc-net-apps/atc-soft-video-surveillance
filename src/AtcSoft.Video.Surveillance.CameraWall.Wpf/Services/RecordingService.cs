// ReSharper disable RedundantArgumentDefaultValue
namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// Service for managing camera recording sessions.
/// </summary>
[Registration(Lifetime.Singleton)]
public partial class RecordingService : IRecordingService, IDisposable
{
    // Cooldown entries older than this are stale — no realistic cooldown
    // setting exceeds a few minutes, so 24h is a wide safety margin.
    private static readonly TimeSpan CooldownEntryMaxAge = TimeSpan.FromHours(24);

    // A camera whose last packet is older than this does not send pictures now.
    private static readonly TimeSpan PacketsAreFresh = TimeSpan.FromSeconds(10);

    // A recording the service stopped by itself is tried again after this, doubling up to the maximum.
    private static readonly TimeSpan RestartFirstDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RestartMaxDelay = TimeSpan.FromMinutes(5);

    private static readonly string[] RecordingFileExtensions = [".mp4", ".mkv", ".avi"];
    private static readonly string[] SnapshotFileExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    private readonly ILogger<RecordingService> logger;
    private readonly IApplicationSettingsService settingsService;
    private readonly IThumbnailGeneratorService thumbnailService;
    private readonly ICameraStorageService cameraStorageService;
    private readonly DiskSpaceGuard diskSpaceGuard;
    private readonly ConcurrentDictionary<Guid, RecordingSession> sessions = new();
    private readonly ConcurrentDictionary<Guid, IMediaPipeline> pipelines = new();

    // One timer per motion recording, on the thread pool: it ends the recording once motion has
    // stayed away, whichever thread started it (a DispatcherTimer only ticks on the UI thread).
    private readonly ConcurrentDictionary<Guid, Timer> postMotionTimers = new();
    private readonly ConcurrentDictionary<Guid, DateTime> recordingCooldowns = new();

    // Recordings this service stopped by itself (a write that failed, a full disk): they start
    // again, with back-off, while their camera sends pictures, until a person stops them.
    private readonly ConcurrentDictionary<Guid, StoppedRecording> stoppedForNow = new();

    // One gate per camera: starting, stopping and switching a camera's recording happen one at a
    // time (the segment worker, the layout synchronizer, the motion timer and the UI all do them),
    // so a stop cannot land between reading a session and switching its file.
    private readonly ConcurrentDictionary<Guid, Lock> cameraGates = new();
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="settingsService">The application settings service.</param>
    /// <param name="thumbnailService">The thumbnail generator service.</param>
    /// <param name="cameraStorageService">The camera storage service.</param>
    /// <param name="diskSpaceGuard">Keeps free space on the volumes that hold media; shared with the cleanup.</param>
    public RecordingService(
        ILogger<RecordingService> logger,
        IApplicationSettingsService settingsService,
        IThumbnailGeneratorService thumbnailService,
        ICameraStorageService cameraStorageService,
        DiskSpaceGuard? diskSpaceGuard = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
        this.cameraStorageService = cameraStorageService ?? throw new ArgumentNullException(nameof(cameraStorageService));
        this.diskSpaceGuard = diskSpaceGuard ?? new DiskSpaceGuard(FreeSpaceReader.Default);
    }

    /// <summary>
    /// Gets the clock the restart back-off and the freshness of a camera's pictures are measured with.
    /// </summary>
    internal TimeProvider Time { get; init; } = TimeProvider.System;

    /// <inheritdoc/>
    public event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;

    /// <inheritdoc/>
    public RecordingState GetRecordingState(Guid cameraId)
    {
        if (sessions.TryGetValue(cameraId, out var session))
        {
            return session.State;
        }

        return RecordingState.Idle;
    }

    /// <inheritdoc/>
    public RecordingSession? GetSession(Guid cameraId)
    {
        sessions.TryGetValue(cameraId, out var session);
        return session;
    }

    /// <inheritdoc/>
    public IMediaPipeline? FindPipeline(Guid cameraId)
        => pipelines.GetValueOrDefault(cameraId);

    /// <inheritdoc/>
    public bool StartRecording(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(camera);

        lock (GateOf(camera.Id))
        {
            return StartRecordingCore(camera, pipeline);
        }
    }

    private bool StartRecordingCore(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(pipeline);

        // Check if already recording
        if (sessions.ContainsKey(camera.Id))
        {
            return false;
        }

        if (!MayRecordInto(GetEffectiveRecordingPath(camera)))
        {
            LogDiskSpaceGateFailed(camera.Display.DisplayName);
            return false;
        }

        var format = GetEffectiveRecordingFormat(camera);
        var startUtc = pipeline.FindRecordingStart(TimeSpan.Zero);
        var filePath = GenerateRecordingFilename(camera, format, startUtc ?? DateTime.UtcNow);

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Start recording via media pipeline
            RecordingPreRoll.Start(pipeline, filePath, startUtc);

            // Create session
            var session = new RecordingSession(camera.Id, camera.Display.DisplayName, filePath, isManualRecording: true, startUtc);
            if (!sessions.TryAdd(camera.Id, session))
            {
                pipeline.StopRecording();
                return false;
            }

            // Store pipeline reference for later stop
            pipelines[camera.Id] = pipeline;

            LogRecordingStarted(camera.Display.DisplayName, filePath);

            // Raise event
            OnRecordingStateChanged(camera.Id, RecordingState.Idle, RecordingState.Recording, filePath);

            // Start thumbnail capture with configured tile count
            var tileCount = GetEffectiveThumbnailTileCount(camera);
            thumbnailService.StartCapture(
                camera.Id,
                camera.Display.DisplayName,
                pipeline,
                filePath,
                tileCount,
                GetThumbnailPath(camera, filePath));

            return true;
        }
        catch (Exception ex)
        {
            LogRecordingStartFailed(ex, camera.Display.DisplayName);
            return false;
        }
    }

    /// <inheritdoc/>
    public void StopRecording(Guid cameraId)
    {
        lock (GateOf(cameraId))
        {
            StopRecordingCore(cameraId);
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Pipeline is owned and disposed by CameraTile; this service only holds a non-owning reference for the recording lifetime.")]
    private void StopRecordingCore(Guid cameraId)
    {
        // Stopped on purpose (a person, the tile going, the app closing): not started again.
        stoppedForNow.TryRemove(cameraId, out _);

        // Stop thumbnail capture first (generates thumbnail with captured frames)
        thumbnailService.StopCapture(cameraId);

        if (!sessions.TryRemove(cameraId, out var session))
        {
            return;
        }

        var oldState = session.State;

        // Stop post-motion timer if running
        StopPostMotionTimer(cameraId);

        // Stop recording via media pipeline
        if (pipelines.TryRemove(cameraId, out var pipeline))
        {
            try
            {
                pipeline.StopRecording();
                LogRecordingStopped(session.CameraName, session.CurrentFilePath);
            }
            catch (Exception ex)
            {
                LogRecordingStopError(ex, session.CameraName);
            }
        }

        // Track cooldown for motion-triggered recordings
        if (!session.IsManualRecording)
        {
            recordingCooldowns[cameraId] = DateTime.UtcNow;
            PruneStaleCooldowns();
        }

        // Raise event
        OnRecordingStateChanged(cameraId, oldState, RecordingState.Idle, session.CurrentFilePath);
    }

    /// <inheritdoc/>
    public bool IsRecording(Guid cameraId)
        => sessions.ContainsKey(cameraId);

    /// <inheritdoc/>
    public bool IsRecordingOrResuming(Guid cameraId)
        => sessions.ContainsKey(cameraId) || stoppedForNow.ContainsKey(cameraId);

    /// <inheritdoc/>
    public bool TriggerMotionRecording(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(camera);

        lock (GateOf(camera.Id))
        {
            return TriggerMotionRecordingCore(camera, pipeline);
        }
    }

    private bool TriggerMotionRecordingCore(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(pipeline);

        // If already recording manually, don't interfere
        if (sessions.TryGetValue(camera.Id, out var existingSession) && existingSession.IsManualRecording)
        {
            return true;
        }

        // If already recording motion, just update the timestamp (no cooldown check)
        if (existingSession is not null)
        {
            UpdateMotionTimestamp(camera.Id);
            return true;
        }

        // Check recording cooldown before starting a NEW recording
        if (recordingCooldowns.TryGetValue(camera.Id, out var lastStopTime))
        {
            var cooldownSeconds = GetEffectiveRecordingCooldown(camera);
            if (cooldownSeconds > 0 && (DateTime.UtcNow - lastStopTime).TotalSeconds < cooldownSeconds)
            {
                return false;
            }
        }

        if (!MayRecordInto(GetEffectiveRecordingPath(camera)))
        {
            LogDiskSpaceGateFailed(camera.Display.DisplayName);
            return false;
        }

        var format = GetEffectiveRecordingFormat(camera);

        // From a few seconds before the motion: the moment it was seen is in the file.
        var startUtc = pipeline.FindRecordingStart(RecordingPreRoll.Motion);
        var filePath = GenerateRecordingFilename(camera, format, startUtc ?? DateTime.UtcNow);

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Start recording via media pipeline
            RecordingPreRoll.Start(pipeline, filePath, startUtc);

            // Create session
            var session = new RecordingSession(camera.Id, camera.Display.DisplayName, filePath, isManualRecording: false, startUtc)
            {
                LastMotionTime = DateTime.UtcNow,
            };

            if (!sessions.TryAdd(camera.Id, session))
            {
                pipeline.StopRecording();
                return false;
            }

            // Store pipeline reference for later stop
            pipelines[camera.Id] = pipeline;

            // Start post-motion timer
            StartPostMotionTimer(camera);

            LogMotionRecordingStarted(camera.Display.DisplayName, filePath);

            // Raise event
            OnRecordingStateChanged(camera.Id, RecordingState.Idle, RecordingState.RecordingMotion, filePath);

            // Start thumbnail capture with configured tile count
            var tileCount = GetEffectiveThumbnailTileCount(camera);
            thumbnailService.StartCapture(
                camera.Id,
                camera.Display.DisplayName,
                pipeline,
                filePath,
                tileCount,
                GetThumbnailPath(camera, filePath));

            return true;
        }
        catch (Exception ex)
        {
            LogMotionRecordingStartFailed(ex, camera.Display.DisplayName);
            return false;
        }
    }

    /// <inheritdoc/>
    public void UpdateMotionTimestamp(Guid cameraId)
    {
        lock (GateOf(cameraId))
        {
            UpdateMotionTimestampCore(cameraId);
        }
    }

    private void UpdateMotionTimestampCore(Guid cameraId)
    {
        if (!sessions.TryGetValue(cameraId, out var session) || session.IsManualRecording)
        {
            return;
        }

        session.LastMotionTime = DateTime.UtcNow;

        // If in post-motion state, transition back to recording motion
        if (session.State == RecordingState.RecordingPostMotion)
        {
            var oldState = session.State;
            session.State = RecordingState.RecordingMotion;
            OnRecordingStateChanged(cameraId, oldState, RecordingState.RecordingMotion, session.CurrentFilePath);
        }
    }

    /// <inheritdoc/>
    public string GenerateRecordingFilename(
        CameraConfiguration camera,
        string format)
        => GenerateRecordingFilename(camera, format, DateTime.UtcNow);

    // Named by the time (UTC) of the file's first picture, as local time.
    private string GenerateRecordingFilename(
        CameraConfiguration camera,
        string format,
        DateTime startUtc)
    {
        ArgumentNullException.ThrowIfNull(camera);

        return UniqueFilename.EnsureUnique(RecordingPathHelper.BuildRecordingPath(
            GetEffectiveRecordingPath(camera),
            RecordingPathHelper.GetCameraFolderName(camera.Display.DisplayName, camera.Id),
            startUtc.ToLocalTime(),
            format,
            RecordingLayoutOptions.From(settingsService.Recording)));
    }

    /// <summary>
    /// Gets where the thumbnail of a recording is written under the
    /// configured layout.
    /// </summary>
    private string GetThumbnailPath(
        CameraConfiguration camera,
        string videoFilePath)
        => RecordingPathHelper.BuildThumbnailPath(
            videoFilePath,
            GetEffectiveRecordingPath(camera),
            RecordingLayoutOptions.From(settingsService.Recording));

    /// <inheritdoc/>
    public void StopAllRecordings()
    {
        // Get all camera IDs to stop
        var cameraIds = sessions.Keys.ToList();

        if (cameraIds.Count > 0)
        {
            LogStoppingAllRecordings(cameraIds.Count);
        }

        foreach (var cameraId in cameraIds)
        {
            StopRecording(cameraId);
        }
    }

    /// <inheritdoc/>
    public bool SegmentRecording(Guid cameraId)
    {
        lock (GateOf(cameraId))
        {
            return SegmentRecordingCore(cameraId);
        }
    }

    private bool SegmentRecordingCore(Guid cameraId)
    {
        // Get the current session
        if (!sessions.TryGetValue(cameraId, out var session))
        {
            LogNoActiveSessionForSegment(cameraId);
            return false;
        }

        // Get the pipeline
        if (!pipelines.TryGetValue(cameraId, out var pipeline))
        {
            LogNoPipelineForSegment(session.CameraName);
            return false;
        }

        // Get camera configuration
        var camera = cameraStorageService.GetCameraById(cameraId);
        if (camera is null)
        {
            LogCameraNotFoundForSegment(cameraId);
            return false;
        }

        // Preserve recording type and motion state
        var isManualRecording = session.IsManualRecording;
        var lastMotionTime = session.LastMotionTime;
        var oldState = session.State;
        var oldFilePath = session.CurrentFilePath;

        LogSegmentingRecording(camera.Display.DisplayName, oldFilePath);

        try
        {
            // 1. Stop thumbnail capture for the old segment
            thumbnailService.StopCapture(cameraId);

            // 2. The next file starts at the last key frame the player has, and is named by it
            var format = GetEffectiveRecordingFormat(camera);
            var startUtc = pipeline.FindRecordingStart(TimeSpan.Zero);
            var newFilePath = UniqueFilename.EnsureDifferentFrom(
                GenerateRecordingFilename(camera, format, startUtc ?? DateTime.UtcNow),
                oldFilePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(newFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 2.5. Reclaim disk space before opening the next segment. A volume that is still
            // short gets no new segment: the recording stops instead of filling it.
            if (ReclaimDiskSpace().StateOf(newFilePath) == DiskSpaceVolumeState.StillShort)
            {
                LogDiskSpaceGuardStoppingRecordings(1);
                StopForNow(cameraId, "the disk is short of space");
                return false;
            }

            // 3. Atomically switch the recording so packets arriving from
            // the demux thread between the two file boundaries land in
            // either the previous segment or the new one — never dropped
            // in the close/open gap.
            RecordingPreRoll.Switch(pipeline, newFilePath, startUtc);

            // 4. Atomically replace the session so IsRecording never dips
            // to false during the segment boundary.
            var newState = isManualRecording ? RecordingState.Recording : RecordingState.RecordingMotion;
            var newSession = new RecordingSession(cameraId, camera.Display.DisplayName, newFilePath, isManualRecording, startUtc)
            {
                LastMotionTime = lastMotionTime,
                State = newState,
            };

            // 4. Atomically replace the session using TryUpdate so the swap
            // is conditional on the old session still being present.
            // If TryUpdate returns false, StopRecording won the race and
            // already removed the entry — don't re-insert an orphaned session.
            if (!sessions.TryUpdate(cameraId, newSession, session))
            {
                LogSegmentRaceLost(camera.Display.DisplayName);
                return false;
            }

            LogRecordingSegmented(camera.Display.DisplayName, newFilePath);

            // 5/8. Fire state-change events: old segment complete, then new segment start.
            OnRecordingStateChanged(cameraId, oldState, RecordingState.Idle, oldFilePath);
            OnRecordingStateChanged(cameraId, RecordingState.Idle, newState, newFilePath);

            // 9. Start thumbnail capture for new segment with configured tile count
            var tileCount = GetEffectiveThumbnailTileCount(camera);
            thumbnailService.StartCapture(
                cameraId,
                camera.Display.DisplayName,
                pipeline,
                newFilePath,
                tileCount,
                GetThumbnailPath(camera, newFilePath));

            return true;
        }
        catch (Exception ex)
        {
            LogSegmentRecordingFailed(ex, camera.Display.DisplayName);
            return false;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<RecordingSession> GetActiveSessions()
        => sessions.Values.ToList();

    /// <summary>
    /// No-op on WPF — the tile lifecycle (RecreatePlayer / Dispose, see
    /// CameraTile) already reaps dead pipelines per-camera and a server-
    /// style sweeper would race with the dispatcher-driven cleanup.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Required interface implementation; cannot be static.")]
    [SuppressMessage("Minor Code Smell", "S3400:Methods should not return constants", Justification = "Intentional interface no-op; tile lifecycle handles dead-pipeline reaping on WPF.")]
    public int ReapInactiveSessions() => 0;

    /// <inheritdoc/>
    public bool EnforceDiskSpaceGuard()
    {
        // A recording whose writes fail (a disk another program filled, a share that is gone)
        // stops here: the recorder drops what it cannot write, so it must not go on claiming
        // to record.
        var failed = StopRecordingsThatCannotWrite();

        var result = ReclaimDiskSpace();

        // Only the recordings on a volume that is still short stop; one whose space cannot be
        // read goes on (a share that hiccups must not end 24/7 recording; a write that fails
        // stops it anyway).
        var shortSessions = sessions.Values
            .Where(s => !string.IsNullOrEmpty(s.CurrentFilePath) &&
                        result.StateOf(s.CurrentFilePath) == DiskSpaceVolumeState.StillShort)
            .ToList();
        if (shortSessions.Count == 0)
        {
            return failed;
        }

        LogDiskSpaceGuardStoppingRecordings(shortSessions.Count);
        foreach (var session in shortSessions)
        {
            StopForNow(session.CameraId, "the disk is short of space");
        }

        return true;
    }

    /// <inheritdoc/>
    public int ResumeRecordings()
    {
        var resumed = 0;

        // A recording whose file ended while its camera sends pictures again (the stream dropped
        // and came back, a write failed): a new file, now, not at the next segment.
        foreach (var session in sessions.Values.ToList())
        {
            if (pipelines.TryGetValue(session.CameraId, out var pipeline) &&
                !pipeline.IsRecordingActive &&
                IsSendingPictures(pipeline) &&
                ResumeRecording(session.CameraId))
            {
                resumed++;
            }
        }

        // A recording this service stopped by itself starts again once it may.
        var now = Time.GetUtcNow().UtcDateTime;
        foreach (var cameraId in stoppedForNow.Keys.ToList())
        {
            if (TryRestart(cameraId, now))
            {
                resumed++;
            }
        }

        return resumed;
    }

    // Under the camera's gate, and read again there: a person who stopped the camera meanwhile
    // took it off the list, and it is not started against their wish.
    private bool TryRestart(
        Guid cameraId,
        DateTime now)
    {
        lock (GateOf(cameraId))
        {
            if (!stoppedForNow.TryGetValue(cameraId, out var stopped))
            {
                return false;
            }

            if (sessions.ContainsKey(cameraId))
            {
                // Started again by something else (the tile reconnecting, a person).
                stoppedForNow.TryRemove(cameraId, out _);
                return false;
            }

            if (now < stopped.NextAttemptUtc || !IsSendingPictures(stopped.Pipeline))
            {
                return false;
            }

            var camera = cameraStorageService.GetCameraById(cameraId);
            if (camera is null)
            {
                stoppedForNow.TryRemove(cameraId, out _);
                return false;
            }

            if (StartRecording(camera, stopped.Pipeline))
            {
                stoppedForNow.TryRemove(cameraId, out _);
                LogRecordingRestarted(stopped.CameraName, stopped.Reason, stopped.Attempts + 1);
                return true;
            }

            var attempts = stopped.Attempts + 1;
            var delay = TimeSpan.FromSeconds(Math.Min(
                RestartMaxDelay.TotalSeconds,
                RestartFirstDelay.TotalSeconds * Math.Pow(2, Math.Min(attempts, 10))));
            stoppedForNow[cameraId] = stopped with { Attempts = attempts, NextAttemptUtc = now + delay };
            LogRecordingRestartFailed(stopped.CameraName, stopped.Reason, attempts, delay.TotalSeconds);

            return false;
        }
    }

    /// <inheritdoc/>
    public bool ResumeRecording(Guid cameraId)
    {
        lock (GateOf(cameraId))
        {
            return ResumeRecordingCore(cameraId);
        }
    }

    private bool ResumeRecordingCore(Guid cameraId)
    {
        if (!sessions.TryGetValue(cameraId, out var session) ||
            !pipelines.TryGetValue(cameraId, out var pipeline) ||
            pipeline.IsRecordingActive)
        {
            return false;
        }

        // The engine ends the file when the stream ends; the session goes on. The next segment
        // starts a new file (the switch starts one when none is open).
        LogRecordingFileEnded(session.CameraName, session.CurrentFilePath);
        return SegmentRecording(cameraId);
    }

    private Lock GateOf(Guid cameraId)
        => cameraGates.GetOrAdd(cameraId, _ => new Lock());

    // Pictures arrive: the stream is up and a recording can be written.
    private bool IsSendingPictures(IMediaPipeline pipeline)
        => pipeline.LastPacketUtc > DateTime.MinValue &&
           Time.GetUtcNow().UtcDateTime - pipeline.LastPacketUtc < PacketsAreFresh;

    // Stops a recording the service has to stop (a write that failed, a full disk), and keeps it
    // to start again: only a person, the tile or the app closing ends a recording for good.
    private void StopForNow(
        Guid cameraId,
        string reason)
    {
        lock (GateOf(cameraId))
        {
            StopForNowCore(cameraId, reason);
        }
    }

    private void StopForNowCore(
        Guid cameraId,
        string reason)
    {
        var session = GetSession(cameraId);
        var pipeline = FindPipeline(cameraId);
        StopRecording(cameraId);

        // A motion recording is not started again: the next motion starts one.
        if (session is { IsManualRecording: true } && pipeline is not null)
        {
            stoppedForNow[cameraId] = new StoppedRecording(
                session.CameraName,
                pipeline,
                reason,
                Attempts: 0,
                NextAttemptUtc: Time.GetUtcNow().UtcDateTime + RestartFirstDelay);
            LogRecordingStoppedForNow(session.CameraName, reason, RestartFirstDelay.TotalSeconds);
        }
    }

    private bool StopRecordingsThatCannotWrite()
    {
        var stopped = false;
        foreach (var (cameraId, pipeline) in pipelines.ToList())
        {
            if (pipeline.RecordingError is { Length: > 0 } error && sessions.TryGetValue(cameraId, out var session))
            {
                LogRecordingWriteFailed(session.CameraName, error);
                StopForNow(cameraId, "a write failed: " + error);
                stopped = true;
            }
        }

        return stopped;
    }

    /// <inheritdoc/>
    public DiskSpaceGuardResult ReclaimDiskSpace()
    {
        var cleanup = settingsService.Recording.Cleanup;
        if (!cleanup.EnableDiskSpaceGuard)
        {
            return DiskSpaceGuardResult.None;
        }

        try
        {
            var folders = GetRecordingFolders();
            var request = new DiskSpaceGuardRequest(
                folders,
                settingsService.CameraDisplay.SnapshotPath,
                (long)cleanup.MinFreeSpaceMb * 1024L * 1024L,
                DiskSpaceGuard.ProtectedPaths(sessions.Values.Select(s => s.CurrentFilePath), folders),
                RecordingFileExtensions,
                SnapshotFileExtensions);
            var result = diskSpaceGuard.Enforce(request);
            DiskSpaceGuardLog.Write(logger, result, cleanup.MinFreeSpaceMb);
            return result;
        }
        catch (Exception ex)
        {
            LogDiskSpaceReclaimFailed(ex);
            return DiskSpaceGuardResult.None;
        }
    }

    /// <summary>
    /// Disposes of the service resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            StopAllRecordings();
        }

        disposed = true;
    }

    // A recording may start when the volume of its folder has the free space the guard keeps.
    // When that space cannot be read (a share that is gone), it may not: better no recording
    // than one that fills a disk nobody can see.
    private bool MayRecordInto(string folder)
    {
        if (!settingsService.Recording.Cleanup.EnableDiskSpaceGuard)
        {
            return true;
        }

        // A start runs on the UI thread (a tile connecting, a click). A volume with room is told by
        // one cheap read; only a volume that is short gets the guard, which may walk the archive.
        var minimum = (long)settingsService.Recording.Cleanup.MinFreeSpaceMb * 1024L * 1024L;
        if (ReadFreeBytes(folder) is { } free && free >= minimum)
        {
            return true;
        }

        var result = ReclaimDiskSpace();
        if (result == DiskSpaceGuardResult.None)
        {
            return false;
        }

        return result.StateOf(folder) is DiskSpaceVolumeState.Ok or DiskSpaceVolumeState.Reclaimed;
    }

    // The free bytes on the folder's volume, or null when they cannot be read (then the guard decides).
    private long? ReadFreeBytes(string folder)
    {
        try
        {
            var reader = diskSpaceGuard.Reader;
            var volume = reader.GetVolume(folder);
            return volume is null ? null : reader.GetFreeBytes(volume);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    // Every folder recordings go to: the global one (or its default) and each camera's own.
    private List<string> GetRecordingFolders()
    {
        var global = settingsService.Recording.RecordingPath;
        var folders = new List<string> { string.IsNullOrEmpty(global) ? ApplicationPaths.DefaultRecordingsPath : global };
        folders.AddRange(cameraStorageService
            .GetAllCameras()
            .Select(c => c.Overrides?.Recording.RecordingPath)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f!));
        return folders;
    }

    private string GetEffectiveRecordingPath(CameraConfiguration camera)
    {
        var overridePath = camera.Overrides?.Recording.RecordingPath;
        if (!string.IsNullOrEmpty(overridePath))
        {
            return overridePath;
        }

        var appPath = settingsService.Recording.RecordingPath;
        return !string.IsNullOrEmpty(appPath)
            ? appPath
            : ApplicationPaths.DefaultRecordingsPath;
    }

    private string GetEffectiveRecordingFormat(CameraConfiguration camera)
    {
        var overrideFormat = camera.Overrides?.Recording.RecordingFormat;
        if (!string.IsNullOrEmpty(overrideFormat))
        {
            return overrideFormat;
        }

        var appFormat = settingsService.Recording.RecordingFormat;
        return !string.IsNullOrEmpty(appFormat)
            ? appFormat
            : DropDownItemsFactory.DefaultRecordingFormat;
    }

    private int GetEffectiveRecordingCooldown(CameraConfiguration camera)
    {
        var overrideCooldown = camera.Overrides?.MotionDetection.CooldownSeconds;
        if (overrideCooldown.HasValue)
        {
            return overrideCooldown.Value;
        }

        return settingsService.MotionDetection.CooldownSeconds;
    }

    private int GetEffectivePostMotionDuration(CameraConfiguration camera)
    {
        var overrideDuration = camera.Overrides?.MotionDetection.PostMotionDurationSeconds;
        if (overrideDuration.HasValue)
        {
            return overrideDuration.Value;
        }

        return settingsService.MotionDetection.PostMotionDurationSeconds;
    }

    private int GetEffectiveThumbnailTileCount(CameraConfiguration camera)
    {
        var overrideTileCount = camera.Overrides?.Recording.ThumbnailTileCount;
        if (overrideTileCount.HasValue)
        {
            return overrideTileCount.Value;
        }

        return settingsService.Recording.ThumbnailTileCount;
    }

    private void StartPostMotionTimer(CameraConfiguration camera)
    {
        var postMotionSeconds = GetEffectivePostMotionDuration(camera);
        var cameraId = camera.Id;

        // Stop existing timer if any
        StopPostMotionTimer(cameraId);

        // Catches all exceptions: one escaping a timer callback ends the process.
        var timer = new Timer(
            _ =>
            {
                try
                {
                    CheckPostMotionState(cameraId, postMotionSeconds);
                }
                catch (Exception ex)
                {
                    LogPostMotionTickFailed(ex, ResolveCameraName(cameraId));
                }
            },
            state: null,
            dueTime: TimeSpan.FromSeconds(1),
            period: TimeSpan.FromSeconds(1));

        postMotionTimers[cameraId] = timer;
    }

    private void StopPostMotionTimer(Guid cameraId)
    {
        if (postMotionTimers.TryRemove(cameraId, out var timer))
        {
            timer.Dispose();
        }
    }

    private void CheckPostMotionState(
        Guid cameraId,
        int postMotionSeconds)
    {
        lock (GateOf(cameraId))
        {
            CheckPostMotionStateCore(cameraId, postMotionSeconds);
        }
    }

    private void CheckPostMotionStateCore(
        Guid cameraId,
        int postMotionSeconds)
    {
        if (!sessions.TryGetValue(cameraId, out var session) || session.IsManualRecording)
        {
            StopPostMotionTimer(cameraId);
            return;
        }

        var lastMotion = session.LastMotionTime ?? session.StartTime;
        var elapsed = (DateTime.UtcNow - lastMotion).TotalSeconds;

        if (elapsed >= postMotionSeconds)
        {
            // Post-motion period elapsed, stop recording
            LogPostMotionPeriodElapsed(session.CameraName);
            StopRecording(cameraId);
        }
        else if (session.State == RecordingState.RecordingMotion && elapsed >= 1)
        {
            // Motion stopped (no update for 1+ second), transition to post-motion state
            var oldState = session.State;
            session.State = RecordingState.RecordingPostMotion;
            LogTransitioningToPostMotion(
                session.CameraName,
                (postMotionSeconds - elapsed).ToString("F0", CultureInfo.InvariantCulture));
            OnRecordingStateChanged(cameraId, oldState, RecordingState.RecordingPostMotion, session.CurrentFilePath);
        }
    }

    private void OnRecordingStateChanged(
        Guid cameraId,
        RecordingState oldState,
        RecordingState newState,
        string? filePath)
    {
        // Invoke each subscriber individually so a single failing handler
        // (e.g. cross-thread DependencyProperty access from a tile that was
        // not marshaled to the dispatcher) cannot abort segmentation for
        // the remaining cameras in the same tick.
        var handlers = RecordingStateChanged?.GetInvocationList();
        if (handlers is null)
        {
            return;
        }

        var args = new RecordingStateChangedEventArgs(cameraId, oldState, newState, filePath);
        foreach (var handler in handlers)
        {
            try
            {
                ((EventHandler<RecordingStateChangedEventArgs>)handler).Invoke(this, args);
            }
            catch (Exception ex)
            {
                LogRecordingStateChangedSubscriberFailed(ex, ResolveCameraName(cameraId));
            }
        }
    }

    private string ResolveCameraName(Guid cameraId)
    {
        if (sessions.TryGetValue(cameraId, out var session))
        {
            return session.CameraName;
        }

        var camera = cameraStorageService.GetCameraById(cameraId);
        return camera?.Display.DisplayName ?? cameraId.ToString();
    }

    // Bounds recordingCooldowns over weeks of motion events. Iterating
    // ConcurrentDictionary is safe; TryRemove is atomic.
    private sealed record StoppedRecording(
        string CameraName,
        IMediaPipeline Pipeline,
        string Reason,
        int Attempts,
        DateTime NextAttemptUtc);

    private void PruneStaleCooldowns()
    {
        var cutoff = DateTime.UtcNow - CooldownEntryMaxAge;
        foreach (var entry in recordingCooldowns)
        {
            if (entry.Value < cutoff)
            {
                recordingCooldowns.TryRemove(entry.Key, out _);
            }
        }
    }
}