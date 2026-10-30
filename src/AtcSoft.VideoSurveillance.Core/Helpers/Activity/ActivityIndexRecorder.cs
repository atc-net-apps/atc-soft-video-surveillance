namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Writes the activity index beside every recording an app makes, so a timeline can show where
/// in it something happened. While a camera records, its pictures are analysed (for the index
/// only, when the camera is not watched for motion) and every analysed second goes into
/// <c>{video}.activity.jsonl</c>. A new segment gets an index of its own. The same in CameraWall
/// and on the server; each app starts and stops it with its hosted services.
/// </summary>
public sealed partial class ActivityIndexRecorder : IDisposable
{
    private readonly ILogger logger;
    private readonly IActiveRecordings recordingService;
    private readonly IMotionDetectionService motionDetectionService;
    private readonly Func<Guid, MotionDetectionSettings?> settingsFor;
    private readonly Func<bool> isEnabled;
    private readonly Func<Guid, IReadOnlyList<MotionZone>> zonesFor;
    private readonly ConcurrentDictionary<Guid, IndexFile> files = new();
    private bool started;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityIndexRecorder"/> class.
    /// </summary>
    /// <param name="logger">The log.</param>
    /// <param name="recordingService">The recordings, and the pipelines they are made on.</param>
    /// <param name="motionDetectionService">The analysis of the pictures.</param>
    /// <param name="settingsFor">
    /// A camera's motion settings, its own overrides applied; <see langword="null"/> for a camera
    /// that is not known, which is then not analysed.
    /// </param>
    /// <param name="isEnabled">
    /// Whether indexes are kept (the <c>EnableActivityIndex</c> setting), asked as each recording or
    /// segment starts; always when <see langword="null"/>.
    /// </param>
    /// <param name="zonesFor">A camera's motion zones; none when <see langword="null"/>.</param>
    public ActivityIndexRecorder(
        ILogger logger,
        IActiveRecordings recordingService,
        IMotionDetectionService motionDetectionService,
        Func<Guid, MotionDetectionSettings?> settingsFor,
        Func<bool>? isEnabled = null,
        Func<Guid, IReadOnlyList<MotionZone>>? zonesFor = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(recordingService);
        ArgumentNullException.ThrowIfNull(motionDetectionService);
        ArgumentNullException.ThrowIfNull(settingsFor);

        this.logger = logger;
        this.recordingService = recordingService;
        this.motionDetectionService = motionDetectionService;
        this.settingsFor = settingsFor;
        this.isEnabled = isEnabled ?? (() => true);
        this.zonesFor = zonesFor ?? (_ => []);
    }

    /// <summary>
    /// Raised when activity on a recorded camera starts or ends, so an open timeline can grow.
    /// </summary>
    public event EventHandler<CameraActivityChangedEventArgs>? ActivityChanged;

    /// <summary>
    /// Gets the path of the index being written for a camera.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <returns>The path, or <see langword="null"/> when none is being written.</returns>
    public string? GetIndexPath(Guid cameraId)
        => files.TryGetValue(cameraId, out var file) ? file.Path : null;

    /// <summary>
    /// Starts following the recordings, including those already running.
    /// </summary>
    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        recordingService.RecordingStateChanged += OnRecordingStateChanged;
        motionDetectionService.PictureAnalyzed += OnPictureAnalyzed;

        foreach (var session in recordingService.GetActiveSessions())
        {
            try
            {
                Open(session.CameraId, session.CurrentFilePath);
            }
            catch (Exception ex)
            {
                LogIndexFailed(logger, ex, session.CameraId);
            }
        }
    }

    /// <summary>
    /// Stops following the recordings and closes every index.
    /// </summary>
    public void Stop()
    {
        if (!started)
        {
            return;
        }

        started = false;
        recordingService.RecordingStateChanged -= OnRecordingStateChanged;
        motionDetectionService.PictureAnalyzed -= OnPictureAnalyzed;

        foreach (var cameraId in files.Keys.ToList())
        {
            Close(cameraId);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
        => Stop();

    // The events fire on the threads that raise them (the demux thread, the detection loop):
    // a failure here is logged, never thrown into them.
    private void OnRecordingStateChanged(
        object? sender,
        RecordingStateChangedEventArgs e)
    {
        try
        {
            if (e.NewState == RecordingState.Idle)
            {
                Close(e.CameraId);

                // A recording that goes on in a new segment reports a stop too.
                if (!recordingService.IsRecording(e.CameraId))
                {
                    motionDetectionService.StopIndexing(e.CameraId);
                }
            }
            else if (e.FilePath is { Length: > 0 } filePath)
            {
                Open(e.CameraId, filePath);
            }
        }
        catch (Exception ex)
        {
            LogIndexFailed(logger, ex, e.CameraId);
        }
    }

    private void OnPictureAnalyzed(
        object? sender,
        PictureAnalyzedEventArgs e)
    {
        if (!files.TryGetValue(e.CameraId, out var file))
        {
            return;
        }

        CameraActivityChangedEventArgs? change = null;
        try
        {
            lock (file.Sync)
            {
                if (!file.IsClosed)
                {
                    change = file.Activity.Add(e.Time, e.Result);
                    file.Writer.Add(e.Time, e.Result);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // A full disk or a file taken away: this segment's index ends here, the recording goes on.
            LogIndexFailed(logger, ex, e.CameraId);
            Close(e.CameraId);
        }

        Raise(change);
    }

    // A subscriber that throws must not end the index of the camera.
    private void Raise(CameraActivityChangedEventArgs? change)
    {
        if (change is null)
        {
            return;
        }

        try
        {
            ActivityChanged?.Invoke(this, change);
        }
        catch (Exception ex)
        {
            LogIndexFailed(logger, ex, change.CameraId);
        }
    }

    private void Open(
        Guid cameraId,
        string videoPath)
    {
        if (files.TryGetValue(cameraId, out var open) &&
            string.Equals(open.VideoPath, videoPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Close(cameraId);

        // Turned off: no index, and no analysis for one.
        if (!isEnabled())
        {
            motionDetectionService.StopIndexing(cameraId);
            return;
        }

        var start = recordingService.GetSession(cameraId) is { } session
            ? ToUtc(session.StartTime)
            : DateTimeOffset.UtcNow;
        var path = RecordingPathHelper.BuildActivityPath(videoPath);
        ActivityLogWriter? writer = null;
        try
        {
            writer = ActivityLogWriter.Create(path, start);
            files[cameraId] = new IndexFile(videoPath, path, writer, new ActivityTracker(cameraId, videoPath, start));
            writer = null;
        }
        finally
        {
            writer?.Dispose();
        }

        if (settingsFor(cameraId) is { } settings &&
            recordingService.FindPipeline(cameraId) is { } pipeline)
        {
            motionDetectionService.StartIndexing(cameraId, pipeline, settings, zonesFor(cameraId));
        }

        LogIndexStarted(logger, cameraId, path);
    }

    private void Close(Guid cameraId)
    {
        if (!files.TryRemove(cameraId, out var file))
        {
            return;
        }

        // Activity going on when its recording ends ends with it.
        CameraActivityChangedEventArgs? ended = null;
        try
        {
            lock (file.Sync)
            {
                file.IsClosed = true;
                ended = file.Activity.End();
                file.Writer.Dispose();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogIndexFailed(logger, ex, cameraId);
        }

        Raise(ended);
    }

    private static DateTimeOffset ToUtc(DateTime time)
        => time.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(time)
            : new DateTimeOffset(time.ToUniversalTime());

    [LoggerMessage(Level = LogLevel.Debug, Message = "Activity index started for camera {CameraId}: {Path}")]
    private static partial void LogIndexStarted(ILogger logger, Guid cameraId, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Activity index failed for camera {CameraId}")]
    private static partial void LogIndexFailed(ILogger logger, Exception ex, Guid cameraId);

    private sealed class IndexFile(
        string videoPath,
        string path,
        ActivityLogWriter writer,
        ActivityTracker activity)
    {
        public ActivityTracker Activity { get; } = activity;

        public Lock Sync { get; } = new();

        public string VideoPath { get; } = videoPath;

        public string Path { get; } = path;

        public ActivityLogWriter Writer { get; } = writer;

        public bool IsClosed { get; set; }
    }
}