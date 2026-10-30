namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Makes the preview pictures of recordings, one at a time: every recording or segment that ends
/// is queued (followed through <see cref="IActiveRecordings.RecordingStateChanged"/>), and so is one
/// asked for that has none. A recording still being written is not, as its previews would end
/// where it was then. The same in CameraWall and on the server; each app runs
/// <see cref="RunPending"/> from a hosted service of its own.
/// </summary>
public sealed partial class RecordingPreviewMaker : IRecordingPreviews
{
    private readonly ConcurrentQueue<string> queue = new();
    private readonly ConcurrentDictionary<string, byte> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger logger;
    private readonly IRecordingPreviewBuilder? builder;
    private readonly IActiveRecordings recordings;
    private bool started;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingPreviewMaker"/> class.
    /// </summary>
    /// <param name="logger">The log.</param>
    /// <param name="recordings">The recordings being made, followed for those that end.</param>
    /// <param name="builder">Makes the previews; <see langword="null"/> where they cannot be made (no GDI+), which leaves the queue empty.</param>
    public RecordingPreviewMaker(
        ILogger logger,
        IActiveRecordings recordings,
        IRecordingPreviewBuilder? builder)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(recordings);

        this.logger = logger;
        this.recordings = recordings;
        this.builder = builder;
    }

    /// <summary>
    /// Starts following the recordings, queuing each one that ends.
    /// </summary>
    public void Start()
    {
        if (!started)
        {
            started = true;
            recordings.RecordingStateChanged += OnRecordingStateChanged;
        }
    }

    /// <summary>
    /// Stops following the recordings.
    /// </summary>
    public void Stop()
    {
        if (started)
        {
            started = false;
            recordings.RecordingStateChanged -= OnRecordingStateChanged;
        }
    }

    /// <inheritdoc />
    public bool TryEnqueue(string videoPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        if (builder is null || IsBeingWritten(videoPath) || !pending.TryAdd(videoPath, 0))
        {
            return false;
        }

        queue.Enqueue(videoPath);
        return true;
    }

    /// <inheritdoc />
    public bool Contains(string videoPath)
        => pending.ContainsKey(videoPath);

    /// <summary>
    /// Makes the previews of every queued recording, one after another.
    /// </summary>
    /// <param name="cancellationToken">Stops between and inside recordings.</param>
    /// <returns>How many recordings got previews.</returns>
    public int RunPending(CancellationToken cancellationToken)
    {
        var made = 0;
        while (!cancellationToken.IsCancellationRequested && builder is not null && queue.TryDequeue(out var videoPath))
        {
            try
            {
                // Gone meanwhile, or written again (a recording restarted into the same name).
                if (!File.Exists(videoPath) || IsBeingWritten(videoPath))
                {
                    continue;
                }

                var watch = System.Diagnostics.Stopwatch.StartNew();
                if (builder.Build(videoPath, cancellationToken) is { } previews)
                {
                    made++;
                    LogPreviewsMade(logger, videoPath, previews.GridCount + previews.Events.Count, watch.Elapsed.TotalSeconds);
                }
            }
            catch (OperationCanceledException)
            {
                // The app is stopping: nothing was written.
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A damaged file or a full disk: the recording plays without previews.
                LogPreviewsFailed(logger, ex, videoPath);
            }
            finally
            {
                pending.TryRemove(videoPath, out _);
            }
        }

        return made;
    }

    private bool IsBeingWritten(string videoPath)
        => recordings.GetActiveSessions().Any(s => string.Equals(s.CurrentFilePath, videoPath, StringComparison.OrdinalIgnoreCase));

    // The event fires on the thread that raises it: a failure here is logged, never thrown into it.
    private void OnRecordingStateChanged(
        object? sender,
        RecordingStateChangedEventArgs e)
    {
        try
        {
            if (e.NewState == RecordingState.Idle && e.FilePath is { Length: > 0 } finished)
            {
                TryEnqueue(finished);
            }
        }
        catch (Exception ex)
        {
            LogPreviewsFailed(logger, ex, e.FilePath ?? string.Empty);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Made {Tiles} previews of {Path} in {Seconds:F1} s")]
    private static partial void LogPreviewsMade(ILogger logger, string path, int tiles, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not make the previews of {Path}")]
    private static partial void LogPreviewsFailed(ILogger logger, Exception ex, string path);
}