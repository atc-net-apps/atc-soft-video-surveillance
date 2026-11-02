namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// Background service that keeps open the cameras the server has work for,
/// and owns their pipelines:
/// a camera recorded on connect (<see cref="RecordingSettings.EnableRecordingOnConnect"/>)
/// is recorded as soon as it plays; a camera recorded on motion
/// (<see cref="RecordingSettings.EnableRecordingOnMotion"/>) is watched for
/// motion, and a motion records it on the same pipeline for as long as it
/// lasts. Both respect per-camera overrides.
/// <para>
/// It is also the <see cref="ICameraPipelineHost"/>: a USB camera can only
/// be opened once, so a live view and a recording someone starts use the
/// pipeline kept here, and the camera is closed when nothing needs it.
/// </para>
/// </summary>
public sealed partial class CameraConnectionService : BackgroundServiceBase<CameraConnectionService>, ICameraPipelineHost, ICameraRuntime
{
    // A watched camera is not recording most of the time, so "not recording"
    // cannot tell that its pipeline died. Silence can: a camera that plays
    // sends packets all the time.
    private static readonly TimeSpan WatchedCameraSilenceLimit = TimeSpan.FromSeconds(30);

    // A recording that ran this long before it failed was not failing all along.
    private static readonly TimeSpan RecordingRanWell = TimeSpan.FromMinutes(5);

    // How soon a camera that was playing and dropped is tried again.
    private static readonly TimeSpan QuickRetryDelay = TimeSpan.FromSeconds(5);

    private readonly ICameraStorageService storageService;
    private readonly IApplicationSettingsService settingsService;
    private readonly IRecordingService recordingService;
    private readonly IMediaPipelineFactory pipelineFactory;
    private readonly IUsbCameraLifecycleCoordinator usbLifecycle;
    private readonly IMotionDetectionService motionDetectionService;
    private readonly CameraConnectionTypeAutoDetector? connectionTypeAutoDetector;
    private readonly ICameraConnectionStateSink? connectionStates;
    private readonly IRecordingIntents? recordingIntents;
    private readonly ConcurrentDictionary<Guid, IMediaPipeline> managedPipelines = new();
    private readonly ConcurrentDictionary<Guid, BackoffState> backoffs = new();
    private readonly ConcurrentDictionary<Guid, Lock> startGates = new();

    // When the server last started each camera's recording on connect, and how
    // its recordings that could write nothing back off.
    private readonly ConcurrentDictionary<Guid, DateTime> recordingStarts = new();
    private readonly ConcurrentDictionary<Guid, BackoffState> writeFailures = new();

    // Cameras whose pipeline was given to the recording service by the
    // record-on-connect path. Only for those does "not recording" mean the
    // pipeline died; a pipeline that is watched, or held for a viewer, is
    // not recording most of the time.
    private readonly ConcurrentDictionary<Guid, byte> recordingOnConnect = new();

    // Cameras whose motion was not recorded yet (the cooldown after the last motion recording, a
    // disk short of space): tried again with every analysed picture while the motion lasts. Before,
    // only a picture that moved tried again, so a motion that began in the cooldown and then stood
    // still was never recorded.
    private readonly ConcurrentDictionary<Guid, byte> motionNotRecorded = new();

    // How many pictures each watched camera had decoded at the last pass. A decoder that froze
    // while packets still arrive leaves the detector comparing the same picture: motion is off
    // without a word, and the packet checks see nothing wrong.
    private readonly ConcurrentDictionary<Guid, long> watchedFrames = new();

    // A pass and a quick retry of one camera never run at once.
    private readonly Lock passGate = new();

    // Cancels the quick retries still waiting when the service stops.
    private readonly CancellationTokenSource quickRetries = new();

    // What each hosted camera's pipeline is in use for, beyond what the
    // server wants with the camera itself. One entry per Acquire: the same
    // use can be held more than once (two snapshots at the same time).
    // Guarded by pipelineGate, as is every decision to close a pipeline
    // that might be in use.
    private readonly Dictionary<Guid, List<CameraPipelineUse>> pipelineUses = [];
    private readonly Lock pipelineGate = new();

    // 1 once the quick retries were cancelled and let go (Dispose may run twice).
    private int quickRetriesReleased;

    public CameraConnectionService(
        ILogger<CameraConnectionService> logger,
        CameraConnectionServiceOptions options,
        IBackgroundServiceHealthService healthService,
        ICameraStorageService storageService,
        IApplicationSettingsService settingsService,
        IRecordingService recordingService,
        IMediaPipelineFactory pipelineFactory,
        IUsbCameraLifecycleCoordinator usbLifecycle,
        IMotionDetectionService motionDetectionService,
        CameraConnectionTypeAutoDetector? connectionTypeAutoDetector = null,
        ICameraConnectionStateSink? connectionStates = null,
        IRecordingIntents? recordingIntents = null)
        : base(logger, options, healthService)
    {
        this.storageService = storageService;
        this.settingsService = settingsService;
        this.recordingService = recordingService;
        this.pipelineFactory = pipelineFactory;
        this.usbLifecycle = usbLifecycle;
        this.motionDetectionService = motionDetectionService;
        this.connectionTypeAutoDetector = connectionTypeAutoDetector;
        this.connectionStates = connectionStates;
        this.recordingIntents = recordingIntents;

        this.motionDetectionService.MotionDetected += OnMotionDetected;
        this.motionDetectionService.PictureAnalyzed += OnPictureAnalyzed;
        this.recordingService.RecordingStateChanged += OnRecordingStateChanged;

        // Subscribe before Start so we never miss the first event the
        // watcher emits after subscription. Start is idempotent.
        this.usbLifecycle.StateChanged += OnUsbLifecycleChanged;
        this.usbLifecycle.Start();

        healthService.SetMaxStalenessInSeconds(
            ServiceName,
            BackgroundServiceHealthHelper.StalenessFor(options));
    }

    /// <summary>
    /// Gets the pipeline this service keeps open for a camera.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <returns>The pipeline, or <see langword="null"/> when the camera is not kept open.</returns>
    public IMediaPipeline? FindPipeline(Guid cameraId)
        => managedPipelines.GetValueOrDefault(cameraId);

    /// <inheritdoc />
    public bool Hosts(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return camera.Connection.Source == CameraSource.Usb;
    }

    /// <inheritdoc />
    public IMediaPipeline? Acquire(
        CameraConfiguration camera,
        CameraPipelineUse use)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (usbLifecycle.IsUnplugged(camera.Id))
        {
            return null;
        }

        // A network camera is shared only while the server already has it open (watched for
        // motion or recorded) and it plays: one connection to the camera instead of two, which
        // cameras that take one or two viewers need. It is not opened here for a viewer alone.
        if (!Hosts(camera))
        {
            lock (pipelineGate)
            {
                if (!managedPipelines.TryGetValue(camera.Id, out var open) || HasStoppedPlaying(open))
                {
                    return null;
                }

                AddUse(camera.Id, use);
                LogSharedForUse(camera.Id, camera.Display.DisplayName, use);
                return open;
            }
        }

        lock (pipelineGate)
        {
            if (!managedPipelines.TryGetValue(camera.Id, out var pipeline))
            {
                // A camera the server keeps open itself is opened on the
                // server's schedule (it may be waiting to retry). Opening
                // it from here as well could open it twice.
                if (ServerKeepsOpen(camera))
                {
                    return null;
                }

                LogOpeningForUse(camera.Id, camera.Display.DisplayName, use);

                try
                {
                    pipeline = OpenManagedPipeline(camera);
                }
                catch (Exception ex)
                {
                    LogOpenForUseFailed(ex, camera.Id, camera.Display.DisplayName, use);
                    RemoveAndDisposePipeline(camera.Id);
                    return null;
                }
            }

            AddUse(camera.Id, use);
            return pipeline;
        }
    }

    // Called under pipelineGate.
    private void AddUse(
        Guid cameraId,
        CameraPipelineUse use)
    {
        if (!pipelineUses.TryGetValue(cameraId, out var uses))
        {
            uses = [];
            pipelineUses[cameraId] = uses;
        }

        uses.Add(use);
    }

    /// <inheritdoc />
    public void Release(
        Guid cameraId,
        CameraPipelineUse use)
    {
        lock (pipelineGate)
        {
            if (!pipelineUses.TryGetValue(cameraId, out var uses) || !uses.Remove(use))
            {
                return;
            }

            if (uses.Count > 0)
            {
                return;
            }

            pipelineUses.Remove(cameraId);
            CloseWhenNothingNeedsIt(cameraId);
        }
    }

    /// <inheritdoc />
    public void Forget(Guid cameraId)
    {
        LogForgettingCamera(cameraId);
        recordingIntents?.Drop(cameraId);
        CloseEverything(cameraId);
        startGates.TryRemove(cameraId, out _);
        recordingStarts.TryRemove(cameraId, out _);
        writeFailures.TryRemove(cameraId, out _);
    }

    /// <summary>
    /// Gets when a camera that could not be opened is tried next.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <returns>The time, in UTC; <c>null</c> when the camera is not waiting.</returns>
    public DateTime? GetNextAttemptUtc(Guid cameraId)
        => backoffs.TryGetValue(cameraId, out var state) ? state.NextAttemptUtc : null;

    /// <inheritdoc />
    public IMediaPipeline? FindPlayingPipeline(Guid cameraId)
        => managedPipelines.TryGetValue(cameraId, out var pipeline) && !HasStoppedPlaying(pipeline)
            ? pipeline
            : null;

    /// <inheritdoc />
    public void Reopen(Guid cameraId)
    {
        LogReopeningCamera(cameraId);
        CloseEverything(cameraId);
    }

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
    {
        // Reap any session whose pipeline has died — including ones started
        // via REST, which CCM doesn't track in managedPipelines.
        // Without this, a single network blip on an API-started recording
        // leaves the session "Recording" forever with no recovery.
        // Why a recording on connect stopped writing is read first: the
        // reaper closes the pipeline that knows.
        var writeErrors = ReadWriteErrors();
        recordingService.ReapInactiveSessions();

        var cameras = storageService.GetAllCameras();
        var appDefault = settingsService.Recording.EnableRecordingOnConnect;

        ForgetDeletedCameras(cameras);

        lock (passGate)
        {
            foreach (var camera in cameras)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                ConnectCamera(camera, appDefault, writeErrors);
            }
        }

        return Task.CompletedTask;
    }

    // One camera's part of a pass: opened, watched, recorded or closed as its settings ask.
    // Called under passGate.
    private void ConnectCamera(
        CameraConfiguration camera,
        bool appDefault,
        Dictionary<Guid, string> writeErrors)
    {
        // The pipeline was rotated when it was created. A rotation set
        // through the API afterwards reaches it here, so snapshots follow
        // at once and recordings from the next segment.
        if (managedPipelines.TryGetValue(camera.Id, out var running))
        {
            running.SetRotation(camera.Display.Rotation);
        }

        // Skip USB cameras whose physical device is currently
        // unplugged. Reconnecting is impossible until the watcher
        // reports the device back, so polling here would just
        // reproduce the ~1 M attempts/year anti-pattern in a more
        // expensive way (we'd actually try to open the dshow
        // source and burn FFmpeg time on each tick).
        if (usbLifecycle.IsUnplugged(camera.Id))
        {
            if (managedPipelines.ContainsKey(camera.Id))
            {
                if (recordingService.IsRecording(camera.Id))
                {
                    try
                    {
                        recordingService.StopRecording(camera.Id);
                    }
                    catch (Exception ex)
                    {
                        LogStopRecordingShutdownError(ex, camera.Id);
                    }
                }

                ScheduleDeferredDisposal(camera.Id);
            }

            return;
        }

        // Skip cameras still inside their backoff window so a
        // persistently dead camera doesn't generate ~1 M failed attempts
        // per year.
        if (backoffs.TryGetValue(camera.Id, out var bs)
            && DateTime.UtcNow < bs.NextAttemptUtc)
        {
            return;
        }

        try
        {
            var recordOnConnect = ShouldRecordContinuously(camera, appDefault);
            var watchForMotion = ShouldWatchForMotion(camera);

            // Motion recording was turned off: the camera is no longer watched.
            if (!watchForMotion && motionDetectionService.IsDetectionActive(camera.Id))
            {
                StopWatching(camera.Id);
            }

            if (!recordOnConnect)
            {
                StopRecordingOnConnect(camera);

                if (watchForMotion)
                {
                    EnsureWatching(camera);
                }
                else
                {
                    // The server itself wants nothing with the camera. A
                    // recording someone started on it is theirs and is
                    // left alone, and so is a live view.
                    CloseUnusedOrSilent(camera);
                    LogRecordingOnConnectDisabled(camera.Id, camera.Display.DisplayName);
                }

                return;
            }

            if (managedPipelines.TryGetValue(camera.Id, out var existingPipeline))
            {
                if (recordingOnConnect.ContainsKey(camera.Id))
                {
                    // The pipeline was given to the recording service and
                    // no longer produces output. Either the recording
                    // service still has the session (it is told to stop,
                    // which closes the pipeline), or the session is gone
                    // already (reaped at the top of this pass, or stopped
                    // from outside) and only the reference here lingers.
                    if (!existingPipeline.IsRecordingActive)
                    {
                        NoteRecordingEnded(camera, writeErrors.GetValueOrDefault(camera.Id) ?? ReadWriteError(existingPipeline));

                        if (recordingService.IsRecording(camera.Id))
                        {
                            recordingService.StopRecording(camera.Id);
                        }

                        recordingOnConnect.TryRemove(camera.Id, out _);

                        if (!Hosts(camera))
                        {
                            // The recording owned the pipeline and closed it.
                            LogDeadPipelineDetected(camera.Id, camera.Display.DisplayName);
                            managedPipelines.TryRemove(camera.Id, out _);
                        }
                        else if (HasStoppedPlaying(existingPipeline))
                        {
                            LogDeadPipelineDetected(camera.Id, camera.Display.DisplayName);
                            ScheduleDeferredDisposal(camera.Id);
                            connectionStates?.Report(camera.Id, ConnectionState.Disconnected);
                        }

                        // Otherwise the recording of a hosted camera was
                        // only stopped: its pipeline plays on (someone
                        // may be watching), and is recorded again below.
                    }
                }
                else if (HasStoppedPlaying(existingPipeline) && !recordingService.IsRecording(camera.Id))
                {
                    // Opened a whole pass ago and still without a
                    // picture, or silent since: it is closed, and opened
                    // again below.
                    LogDeadPipelineDetected(camera.Id, camera.Display.DisplayName);
                    ScheduleDeferredDisposal(camera.Id);
                }
            }

            if (recordingService.IsRecording(camera.Id))
            {
                return;
            }

            // Its recording could write nothing (a share gone, a disk
            // failing) lately: it waits, rather than start a new near-empty
            // file every pass.
            if (IsWaitingAfterWriteFailure(camera.Id))
            {
                return;
            }

            // Pipeline already created — try to start recording if player is now playing
            if (managedPipelines.TryGetValue(camera.Id, out var pending))
            {
                TryStartRecordingForPipeline(camera, pending);
                return;
            }

            LogStartingRecordingOnConnect(camera.Id, camera.Display.DisplayName);

            // Open() is non-blocking — recording starts when the player reaches Playing state
            OpenManagedPipeline(camera);
        }
        catch (Exception ex)
        {
            LogStartRecordingFailed(ex, camera.Id, camera.Display.DisplayName);
            RecordFailure(camera);
            RemoveAndDisposePipeline(camera.Id);
        }
    }

    /// <summary>
    /// Gracefully stops all managed recordings before the service shuts down.
    /// This runs during the hosted-service shutdown phase which has the full
    /// shutdown timeout (30 s by default), unlike Dispose which runs during
    /// DI container teardown when time may be limited.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref quickRetriesReleased) == 0)
        {
            await quickRetries.CancelAsync().ConfigureAwait(false);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        StopAllManagedRecordings();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        usbLifecycle.StateChanged -= OnUsbLifecycleChanged;
        motionDetectionService.MotionDetected -= OnMotionDetected;
        motionDetectionService.PictureAnalyzed -= OnPictureAnalyzed;
        recordingService.RecordingStateChanged -= OnRecordingStateChanged;

        // Pipelines should already be stopped by StopAsync; dispose cleans up remaining resources
        foreach (var kvp in managedPipelines)
        {
            motionDetectionService.StopDetection(kvp.Key);

            try
            {
                kvp.Value.Dispose();
            }
            catch (Exception ex)
            {
                LogDisposePipelineError(ex, kvp.Key);
            }
        }

        // Dispose may be called more than once (the host and the container).
        if (Interlocked.Exchange(ref quickRetriesReleased, 1) == 0)
        {
            quickRetries.Cancel();
            quickRetries.Dispose();
        }

        managedPipelines.Clear();
        base.Dispose();
    }

    // Stops the camera's recording and closes its pipeline, whoever uses it.
    private void CloseEverything(Guid cameraId)
    {
        motionDetectionService.StopDetection(cameraId);

        try
        {
            recordingService.StopRecording(cameraId);
        }
        catch (Exception ex)
        {
            LogStopRecordingShutdownError(ex, cameraId);
        }

        lock (pipelineGate)
        {
            pipelineUses.Remove(cameraId);
        }

        // Closes the pipeline whoever used it; a recording that owned it has
        // closed it already, and closing it again does no harm.
        ScheduleDeferredDisposal(cameraId);
        recordingOnConnect.TryRemove(cameraId, out _);
        backoffs.TryRemove(cameraId, out _);
    }

    // A camera deleted while a pass was running, or by a path that did not
    // say so, is let go here: a recording of a camera that is gone would
    // never be segmented, and grow until its disk is full.
    private void ForgetDeletedCameras(
        IReadOnlyList<CameraConfiguration> cameras)
    {
        var known = cameras.Select(c => c.Id).ToHashSet();
        var gone = managedPipelines.Keys
            .Concat(recordingService.GetActiveSessions().Select(s => s.CameraId))
            .Concat(backoffs.Keys)
            .Where(id => !known.Contains(id))
            .Distinct()
            .ToList();

        foreach (var cameraId in gone)
        {
            Forget(cameraId);
        }
    }

    // Recorded for as long as it plays: on connect by the settings, or
    // because someone started a recording of it and has not stopped it.
    private bool ShouldRecordContinuously(CameraConfiguration camera)
        => ShouldRecordContinuously(camera, settingsService.Recording.EnableRecordingOnConnect);

    private bool ShouldRecordContinuously(
        CameraConfiguration camera,
        bool appDefault)
        => RecordingPolicyHelper.ShouldRecordOnConnect(camera, appDefault) ||
           recordingIntents?.IsWanted(camera.Id) == true;

    // A recording the server started because the camera was to be recorded
    // on connect stops when it no longer is (the setting was turned off, or
    // the camera is now recorded on motion only). One someone started on
    // their own is left alone.
    private void StopRecordingOnConnect(CameraConfiguration camera)
    {
        if (!recordingOnConnect.TryRemove(camera.Id, out _))
        {
            return;
        }

        LogRecordingOnConnectTurnedOff(camera.Id, camera.Display.DisplayName);
        if (recordingService.IsRecording(camera.Id))
        {
            recordingService.StopRecording(camera.Id);
        }

        // The recording owned the pipeline and closed it; a hosted camera's
        // pipeline plays on for whoever watches it.
        if (!Hosts(camera))
        {
            ScheduleDeferredDisposal(camera.Id);
        }
    }

    private bool ShouldWatchForMotion(CameraConfiguration camera)
        => MotionDetectionPolicy.ShouldRecordOnMotion(camera, settingsService.Recording.EnableRecordingOnMotion);

    // Whether the server wants the camera open for its own work, whoever
    // else uses it.
    private bool ServerKeepsOpen(CameraConfiguration camera)
        => ShouldWatchForMotion(camera) ||
           ShouldRecordContinuously(camera);

    // A hosted camera's pipeline stays here whoever records on it, so that
    // stopping the recording does not close it under a viewer.
    private bool StartRecordingOnConnect(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
        => Hosts(camera)
            ? recordingService.StartRecordingOnSharedPipeline(camera, pipeline)
            : recordingService.StartRecording(camera, pipeline);

    // A recording that ran a while before it ended starts its count afresh;
    // one that ended because it could write nothing waits before the next,
    // longer each time (30 seconds up to 15 minutes).
    private void NoteRecordingEnded(
        CameraConfiguration camera,
        string? error)
    {
        if (recordingStarts.TryRemove(camera.Id, out var startedAt) &&
            DateTime.UtcNow - startedAt > RecordingRanWell)
        {
            writeFailures.TryRemove(camera.Id, out _);
        }

        if (string.IsNullOrEmpty(error))
        {
            return;
        }

        var failures = writeFailures.TryGetValue(camera.Id, out var previous) ? previous.ConsecutiveFailures + 1 : 1;
        var delay = ReconnectBackoff.ComputeDelay(failures);
        writeFailures[camera.Id] = new BackoffState
        {
            ConsecutiveFailures = failures,
            NextAttemptUtc = DateTime.UtcNow + delay,
        };
        LogRecordingWriteBackoff(camera.Id, camera.Display.DisplayName, failures, (int)delay.TotalSeconds);
    }

    private Dictionary<Guid, string> ReadWriteErrors()
    {
        var errors = new Dictionary<Guid, string>();
        foreach (var (cameraId, pipeline) in managedPipelines)
        {
            if (recordingOnConnect.ContainsKey(cameraId) && ReadWriteError(pipeline) is { Length: > 0 } error)
            {
                errors[cameraId] = error;
            }
        }

        return errors;
    }

    private static string? ReadWriteError(IMediaPipeline pipeline)
    {
        try
        {
            return pipeline.RecordingError;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    private bool IsWaitingAfterWriteFailure(Guid cameraId)
        => writeFailures.TryGetValue(cameraId, out var state) && DateTime.UtcNow < state.NextAttemptUtc;

    private static bool HasStoppedPlaying(IMediaPipeline pipeline)
    {
        var lastPacket = pipeline.LastPacketUtc;
        return pipeline.FramesDecoded <= 0 ||
               (lastPacket != DateTime.MinValue && DateTime.UtcNow - lastPacket > WatchedCameraSilenceLimit);
    }

    private IMediaPipeline OpenManagedPipeline(CameraConfiguration camera)
    {
        // Opened as the camera is now: an edit made while a pass was running
        // (which reads the cameras once) is not opened with the old address.
        camera = storageService.GetCameraById(camera.Id) ?? camera;
        var pipeline = pipelineFactory.Create(camera);
        managedPipelines[camera.Id] = pipeline;

        var cameraId = camera.Id;
        pipeline.ConnectionStateChanged += (_, e) =>
        {
            // Runs on the pipeline's own thread: nothing may unwind into it.
            try
            {
                // A pipeline that was replaced or closed meanwhile (the camera
                // was edited or deleted) speaks for nobody any more; the camera
                // is read as it is now, not as it was when it was opened.
                if (!managedPipelines.TryGetValue(cameraId, out var current) ||
                    !ReferenceEquals(current, pipeline) ||
                    storageService.GetCameraById(cameraId) is not { } latest)
                {
                    return;
                }

                OnManagedPipelineConnected(latest, e);
            }
            catch (Exception ex)
            {
                LogPipelineStateHandlerFailed(ex, cameraId, e.NewState);
            }
        };

        return pipeline;
    }

    /// <summary>
    /// A recording someone started on a hosted camera has ended: that use
    /// of the camera's pipeline is over. A recording that only goes on in a
    /// new file reports a stop too, while the camera is recording still.
    /// </summary>
    private void OnRecordingStateChanged(
        object? sender,
        RecordingStateChangedEventArgs e)
    {
        if (e.NewState == RecordingState.Idle && !recordingService.IsRecording(e.CameraId))
        {
            Release(e.CameraId, CameraPipelineUse.Recording);
        }
    }

    /// <summary>
    /// Closes a camera the server wants nothing with itself, unless someone
    /// uses it; and whoever uses it, when it has stopped sending.
    /// </summary>
    private void CloseUnusedOrSilent(CameraConfiguration camera)
    {
        if (!managedPipelines.TryGetValue(camera.Id, out var open))
        {
            return;
        }

        var lastPacket = open.LastPacketUtc;
        if (lastPacket != DateTime.MinValue && DateTime.UtcNow - lastPacket > WatchedCameraSilenceLimit)
        {
            LogDeadPipelineDetected(camera.Id, camera.Display.DisplayName);

            if (recordingService.IsRecording(camera.Id))
            {
                recordingService.StopRecording(camera.Id);
            }

            ReleaseWatchedCamera(camera.Id);
            return;
        }

        lock (pipelineGate)
        {
            if (!pipelineUses.ContainsKey(camera.Id))
            {
                CloseWhenNothingNeedsIt(camera.Id);
            }
        }
    }

    // Called with pipelineGate held, so that nobody is handed a pipeline
    // that is being closed.
    private void CloseWhenNothingNeedsIt(Guid cameraId)
    {
        if (!managedPipelines.ContainsKey(cameraId) || recordingService.IsRecording(cameraId))
        {
            return;
        }

        var camera = storageService.GetCameraById(cameraId);
        if (camera is not null && ServerKeepsOpen(camera))
        {
            return;
        }

        LogClosingUnusedCamera(cameraId);
        ReleaseWatchedCamera(cameraId);
    }

    /// <summary>
    /// Keeps a camera that is recorded on motion open. Its pipeline is not
    /// recording most of the time, so it is checked by whether the camera
    /// still sends anything.
    /// </summary>
    private void EnsureWatching(CameraConfiguration camera)
    {
        if (managedPipelines.TryGetValue(camera.Id, out var watching))
        {
            var lastPacket = watching.LastPacketUtc;
            if (lastPacket != DateTime.MinValue && DateTime.UtcNow - lastPacket > WatchedCameraSilenceLimit)
            {
                LogDeadPipelineDetected(camera.Id, camera.Display.DisplayName);
                ReleaseWatchedCamera(camera.Id);
                return;
            }

            var frames = watching.FramesDecoded;
            if (frames > 0 &&
                watchedFrames.TryGetValue(camera.Id, out var before) &&
                frames == before)
            {
                LogPicturesStopped(camera.Id, camera.Display.DisplayName);
                ReleaseWatchedCamera(camera.Id);
                return;
            }

            watchedFrames[camera.Id] = frames;

            // The state change was missed (it can come before the handler is
            // attached): a camera that plays is watched from here.
            if (watching.FramesDecoded > 0 && !motionDetectionService.IsDetectionActive(camera.Id))
            {
                StartWatching(camera, watching);
            }
            else if (motionDetectionService.IsDetectionActive(camera.Id))
            {
                // Settings changed since the watching started (PUT /settings, a camera's
                // overrides): the running detection takes them at its next quiet moment.
                motionDetectionService.UseSettings(camera.Id, MotionDetectionPolicy.Resolve(camera, settingsService.MotionDetection));
            }

            return;
        }

        LogStartingMotionWatch(camera.Id, camera.Display.DisplayName);
        OpenManagedPipeline(camera);
    }

    private void StartWatching(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
    {
        try
        {
            motionDetectionService.StartDetection(
                camera.Id,
                pipeline,
                MotionDetectionPolicy.Resolve(camera, settingsService.MotionDetection),
                camera.MotionZones);
        }
        catch (Exception ex)
        {
            LogStartMotionWatchFailed(ex, camera.Id, camera.Display.DisplayName);
        }
    }

    /// <summary>
    /// Stops watching a camera for motion, and the recording a motion
    /// started. A recording started any other way is left running.
    /// </summary>
    private void StopWatching(Guid cameraId)
    {
        motionDetectionService.StopDetection(cameraId);
        StopMotionRecording(cameraId);
    }

    private void StopMotionRecording(Guid cameraId)
    {
        try
        {
            if (recordingService.GetSession(cameraId) is { IsManualRecording: false })
            {
                recordingService.StopRecording(cameraId);
            }
        }
        catch (Exception ex)
        {
            LogStopRecordingShutdownError(ex, cameraId);
        }
    }

    /// <summary>
    /// Stops watching a camera and closes its pipeline.
    /// </summary>
    private void ReleaseWatchedCamera(Guid cameraId)
    {
        StopWatching(cameraId);
        ScheduleDeferredDisposal(cameraId);
        connectionStates?.Report(cameraId, ConnectionState.Disconnected);
    }

    /// <summary>
    /// A motion on a camera that is only recorded on motion starts a
    /// recording on the pipeline that watches it; the end of the motion
    /// stops it. Runs on the detector's thread.
    /// </summary>
    private void OnMotionDetected(
        object? sender,
        MotionDetectedEventArgs e)
    {
        // An end is handled even when the pipeline is already gone: closing a camera
        // removes its pipeline first, then stops the detection, which ends the motion.
        if (!e.IsMotionActive)
        {
            motionNotRecorded.TryRemove(e.CameraId, out _);
            StopMotionRecording(e.CameraId);
            return;
        }

        RecordMotion(e.CameraId);
    }

    // A motion that could not be recorded yet is tried again with every analysed picture, moving
    // or not, until it is recorded or ends. Runs on the detector's thread.
    private void OnPictureAnalyzed(
        object? sender,
        PictureAnalyzedEventArgs e)
    {
        if (motionNotRecorded.ContainsKey(e.CameraId))
        {
            RecordMotion(e.CameraId);
        }
    }

    private void RecordMotion(Guid cameraId)
    {
        if (!managedPipelines.TryGetValue(cameraId, out var pipeline))
        {
            motionNotRecorded.TryRemove(cameraId, out _);
            return;
        }

        try
        {
            var camera = storageService.GetCameraById(cameraId);
            if (camera is null ||
                !ShouldWatchForMotion(camera) ||
                ShouldRecordContinuously(camera))
            {
                motionNotRecorded.TryRemove(cameraId, out _);
                return;
            }

            if (recordingService.TriggerMotionRecording(camera, pipeline))
            {
                motionNotRecorded.TryRemove(cameraId, out _);
            }
            else
            {
                motionNotRecorded[cameraId] = 0;
            }
        }
        catch (Exception ex)
        {
            motionNotRecorded.TryRemove(cameraId, out _);
            LogStartMotionRecordingFailed(ex, cameraId);
        }
    }

    /// <summary>
    /// Reacts to USB hot-plug transitions surfaced by
    /// <see cref="IUsbCameraLifecycleCoordinator"/>. Unplug → tear
    /// down the active pipeline + recording so resources are freed
    /// promptly. Replug → clear the backoff entry so the next
    /// <see cref="DoWorkAsync"/> tick attempts the camera fresh, no
    /// matter how long it was gone.
    /// </summary>
    private void OnUsbLifecycleChanged(
        object? sender,
        UsbCameraLifecycleChangedEventArgs e)
    {
        switch (e.Phase)
        {
            case UsbCameraLifecyclePhase.Unplugged:
                LogUsbCameraUnplugged(e.CameraId, e.Device.FriendlyName);

                if (recordingService.IsRecording(e.CameraId))
                {
                    try
                    {
                        recordingService.StopRecording(e.CameraId);
                    }
                    catch (Exception ex)
                    {
                        LogStopRecordingShutdownError(ex, e.CameraId);
                    }
                }

                if (managedPipelines.ContainsKey(e.CameraId))
                {
                    ScheduleDeferredDisposal(e.CameraId);
                }

                break;

            case UsbCameraLifecyclePhase.Replugged:
                // Clearing the backoff is the trigger that lets the
                // next DoWorkAsync tick re-attempt the camera. Without
                // this, an unplug while in the middle of a long
                // backoff window would survive the replug.
                backoffs.TryRemove(e.CameraId, out _);
                LogUsbCameraReplugged(e.CameraId, e.Device.FriendlyName);
                break;
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The pipeline is disposed right after it is taken out of the dictionary.")]
    private void StopAllManagedRecordings()
    {
        foreach (var cameraId in managedPipelines.Keys.ToList())
        {
            motionDetectionService.StopDetection(cameraId);

            try
            {
                LogStoppingManagedRecording(cameraId);
                recordingService.StopRecording(cameraId);
            }
            catch (Exception ex)
            {
                LogStopRecordingShutdownError(ex, cameraId);
            }

            // recordingService.StopRecording closes the pipeline of a
            // recording that owns it, but not the one a motion recording
            // borrowed, nor that of a camera that was only watched. Closing
            // it again does no harm.
            if (managedPipelines.TryRemove(cameraId, out var pipeline))
            {
                try
                {
                    pipeline.Dispose();
                }
                catch (Exception ex)
                {
                    LogDisposePipelineError(ex, cameraId);
                }
            }
        }
    }

    private void TryStartRecordingForPipeline(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
    {
        // FramesDecoded > 0 means the player is playing and decoding — safe to start recording
        if (pipeline.FramesDecoded <= 0)
        {
            return;
        }

        try
        {
            if (TryStartOnConnect(camera, pipeline) == OnConnectStart.Started)
            {
                LogRecordingOnConnectStartedFallback(camera.Id, camera.Display.DisplayName);
            }
        }
        catch (Exception ex)
        {
            LogStartRecordingFallbackFailed(ex, camera.Id, camera.Display.DisplayName);
        }
    }

    // Runs on the thread pool once a pipeline recorded on connect plays.
    private void StartRecordingAfterConnect(
        Guid cameraId,
        IMediaPipeline pipeline)
    {
        // The camera may have been edited or deleted meanwhile.
        if (storageService.GetCameraById(cameraId) is not { } camera ||
            !ShouldRecordContinuously(camera))
        {
            return;
        }

        try
        {
            if (TryStartOnConnect(camera, pipeline) == OnConnectStart.Started)
            {
                LogRecordingOnConnectStarted(camera.Id, camera.Display.DisplayName);
            }
        }
        catch (Exception ex)
        {
            LogStartRecordingAfterConnectFailed(ex, camera.Id, camera.Display.DisplayName);
            ScheduleDeferredDisposal(camera.Id);
        }
    }

    // Starts the recording of a camera recorded on connect on the pipeline
    // kept for it. One start at a time per camera, so the periodic pass and a
    // connect never both start it (the loser would close the winner's pipeline).
    private OnConnectStart TryStartOnConnect(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
    {
        lock (startGates.GetOrAdd(camera.Id, _ => new Lock()))
        {
            if (!managedPipelines.TryGetValue(camera.Id, out var current) ||
                !ReferenceEquals(current, pipeline))
            {
                // Replaced or closed meanwhile.
                return OnConnectStart.Gone;
            }

            if (recordingService.IsRecording(camera.Id))
            {
                return OnConnectStart.RecordedAlready;
            }

            if (IsWaitingAfterWriteFailure(camera.Id))
            {
                return OnConnectStart.Refused;
            }

            if (StartRecordingOnConnect(camera, pipeline))
            {
                recordingOnConnect[camera.Id] = 0;
                recordingStarts[camera.Id] = DateTime.UtcNow;
                return OnConnectStart.Started;
            }

            if (!recordingService.IsRecording(camera.Id))
            {
                // Refused (no room on the disk): the pipeline stays open, and
                // the next pass tries again without opening the camera anew.
                return OnConnectStart.Refused;
            }

            // Someone else (a recording started through the API) got there
            // first, on a pipeline of its own: this one is not needed. A
            // hosted camera has only this pipeline, whoever records on it.
            if (!Hosts(camera))
            {
                ScheduleDeferredDisposal(camera.Id);
            }

            return OnConnectStart.RecordedAlready;
        }
    }

    /// <summary>
    /// Handles pipeline state changes. Runs on the demux thread — must NOT
    /// call <see cref="RemoveAndDisposePipeline"/> synchronously because
    /// Dispose → Join would deadlock (self-join on the demux thread).
    /// </summary>
    private void OnManagedPipelineConnected(
        CameraConfiguration camera,
        ConnectionStateChangedEventArgs e)
    {
        if (e.NewState == ConnectionState.Connected)
        {
            // Successful connect — clear any prior backoff so the next
            // disconnect starts fresh from the base delay. If there was a
            // prior failure streak, surface it so soak operators can see how
            // many attempts the recovery took.
            if (backoffs.TryRemove(camera.Id, out var clearedBackoff))
            {
                LogReconnected(camera.Display.DisplayName, camera.Id, clearedBackoff.ConsecutiveFailures);
            }

            LogCameraConnected(camera.Id, camera.Display.DisplayName);

            // Implicit connection-type detection (once per camera, fills "not set" only).
            _ = connectionTypeAutoDetector?.OnCameraConnectedAsync(camera);

            if (!managedPipelines.TryGetValue(camera.Id, out var pipeline))
            {
                return;
            }

            if (ShouldWatchForMotion(camera))
            {
                StartWatching(camera, pipeline);
            }

            // A camera that is only watched is not recorded until it moves.
            if (!ShouldRecordContinuously(camera))
            {
                connectionStates?.Report(camera.Id, ConnectionState.Connected);
                return;
            }

            // A recording on a hosted camera's pipeline does not report the
            // camera's connection: the pipeline is not its own.
            if (Hosts(camera))
            {
                connectionStates?.Report(camera.Id, ConnectionState.Connected);
            }

            if (recordingService.IsRecording(camera.Id))
            {
                return;
            }

            // Not on this thread: it reads the camera's packets, and starting a
            // recording may wait for the disk-space guard (which may walk the
            // archive on a full disk). Packets read late are packets lost.
            var cameraId = camera.Id;
            ThreadPool.QueueUserWorkItem(_ => StartRecordingAfterConnect(cameraId, pipeline));
        }
        else if (e.NewState == ConnectionState.Disconnected)
        {
            LogCameraDisconnected(camera.Id, camera.Display.DisplayName);
        }
        else if (e.NewState == ConnectionState.Error)
        {
            LogPipelineConnectionFailed(camera.Id, camera.Display.DisplayName);
            RecordFailure(camera);

            // A camera that was playing is tried again in a few seconds, not at a pass up to a
            // minute later: until then it is neither watched nor recorded.
            if (e.PreviousState == ConnectionState.Connected)
            {
                ScheduleQuickRetry(camera);
            }

            StopWatching(camera.Id);

            // A recording on a hosted camera's pipeline does not own it and
            // would be left recording on a closed pipeline.
            if (Hosts(camera) && recordingService.IsRecording(camera.Id))
            {
                try
                {
                    recordingService.StopRecording(camera.Id);
                }
                catch (Exception ex)
                {
                    LogStopRecordingShutdownError(ex, camera.Id);
                }
            }

            ScheduleDeferredDisposal(camera.Id);

            // A recorded camera's state is reported by its recording. One
            // that was only watched, or is hosted, has nobody else to say
            // it is gone.
            if (Hosts(camera) ||
                !ShouldRecordContinuously(camera))
            {
                connectionStates?.Report(camera.Id, ConnectionState.Disconnected);
            }
        }
    }

    // The first failure after playing is tried again after QuickRetryDelay instead of the usual
    // back-off; a camera that fails again waits as before.
    private void ScheduleQuickRetry(CameraConfiguration camera)
    {
        if (!backoffs.TryGetValue(camera.Id, out var state) || state.ConsecutiveFailures != 1)
        {
            return;
        }

        backoffs[camera.Id] = new BackoffState
        {
            ConsecutiveFailures = 1,
            NextAttemptUtc = DateTime.UtcNow + QuickRetryDelay,
        };

        CancellationToken token;
        try
        {
            token = quickRetries.Token;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        var cameraId = camera.Id;

        // A little after the time set above: a timer may end a few milliseconds early, and the
        // camera would then still be waiting for its back-off.
        _ = Task.Delay(QuickRetryDelay + TimeSpan.FromMilliseconds(500), token).ContinueWith(
            t =>
            {
                if (!t.IsCanceled)
                {
                    RetryCamera(cameraId);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void RetryCamera(Guid cameraId)
    {
        var camera = storageService.GetCameraById(cameraId);
        if (camera is null || usbLifecycle.IsUnplugged(cameraId))
        {
            return;
        }

        LogQuickRetry(camera.Display.DisplayName, cameraId);
        lock (passGate)
        {
            ConnectCamera(camera, settingsService.Recording.EnableRecordingOnConnect, writeErrors: []);
        }
    }

    // Bumps the camera's consecutive-failure count and schedules the next
    // attempt using capped exponential backoff; called from the demux
    // thread on Error events. The camera reference (rather than just the ID)
    // is needed so the log line can include the friendly name without an
    // extra storage lookup on the hot path.
    private void RecordFailure(CameraConfiguration camera)
    {
        // A camera is tried at least every two minutes: every minute it is
        // not, nothing is recorded or watched.
        var maxDelay = ReconnectBackoff.CameraMaxDelay;
        var updated = backoffs.AddOrUpdate(
            camera.Id,
            _ => new BackoffState
            {
                ConsecutiveFailures = 1,
                NextAttemptUtc = DateTime.UtcNow + ReconnectBackoff.ComputeDelay(1, maxDelay: maxDelay),
            },
            (_, prev) =>
            {
                var next = prev.ConsecutiveFailures + 1;
                return new BackoffState
                {
                    ConsecutiveFailures = next,
                    NextAttemptUtc = DateTime.UtcNow + ReconnectBackoff.ComputeDelay(next, maxDelay: maxDelay),
                };
            });

        var backoffSeconds = (int)(updated.NextAttemptUtc - DateTime.UtcNow).TotalSeconds;
        LogBackoffScheduled(
            camera.Display.DisplayName,
            camera.Id,
            updated.ConsecutiveFailures,
            backoffSeconds,
            updated.NextAttemptUtc);
    }

    private enum OnConnectStart
    {
        Started,
        RecordedAlready,
        Refused,
        Gone,
    }

    private sealed class BackoffState
    {
        public int ConsecutiveFailures { get; init; }

        public DateTime NextAttemptUtc { get; init; }
    }

    [SuppressMessage(
        "Usage",
        "CA2000:Dispose objects before losing scope",
        Justification = "Pipeline is disposed immediately after removal from the dictionary, which ensures no further references and prevents self-join deadlocks on the demux thread.")]
    private void RemoveAndDisposePipeline(Guid cameraId)
    {
        if (managedPipelines.TryRemove(cameraId, out var pipeline))
        {
            recordingOnConnect.TryRemove(cameraId, out _);

            // The detector must not be left asking a closed pipeline for pictures.
            motionDetectionService.StopDetection(cameraId);

            try
            {
                pipeline.Dispose();
            }
            catch (Exception ex)
            {
                LogDisposeDeadPipelineError(ex, cameraId);
            }
        }
    }

    /// <summary>
    /// Removes the pipeline from the managed dictionary and disposes it on a
    /// ThreadPool thread. This prevents self-join deadlocks when called from
    /// the pipeline's demux thread (via the ConnectionStateChanged event).
    /// </summary>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Pipeline is unconditionally disposed asynchronously on the ThreadPool inside the queued work item.")]
    private void ScheduleDeferredDisposal(Guid cameraId)
    {
        watchedFrames.TryRemove(cameraId, out _);
        if (managedPipelines.TryRemove(cameraId, out var pipeline))
        {
            recordingOnConnect.TryRemove(cameraId, out _);

            // The detector must not be left asking a closed pipeline for pictures.
            motionDetectionService.StopDetection(cameraId);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    pipeline.Dispose();
                }
                catch (Exception ex)
                {
                    LogDisposePipelineDeferredError(ex, cameraId);
                }
            });
        }
    }
}