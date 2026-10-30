namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Server-side implementation of <see cref="IMotionDetectionService"/>. A few
/// times a second it takes a picture from the camera's pipeline, compares it
/// with the one before (<see cref="MotionFrameAnalyzer"/>, shared with the
/// desktop app) and reports what moved.
/// </summary>
/// <remarks>
/// A motion lasts from the first picture in which something moved until the
/// scene has been quiet for the post-motion time. While it lasts, every
/// picture with movement raises <see cref="MotionDetected"/> with the boxes
/// (for live overlays); its end raises the event once with
/// <see cref="MotionDetectedEventArgs.IsMotionActive"/> false.
/// <para>
/// A camera that is recorded is also analysed for its activity index
/// (<see cref="StartIndexing"/>), every picture raising <see cref="PictureAnalyzed"/>. A camera
/// analysed only for the index reports no motion: no badge, no webhook, no motion recording.
/// </para>
/// </remarks>
public sealed partial class ServerMotionDetectionService : IMotionDetectionService, IDisposable
{
    // A picture is captured as a PNG and decoded again, which costs far more
    // than the comparison. Four a second catches a person walking through
    // the scene and keeps a server with many cameras usable.
    private const int MaxAnalysesPerSecond = 4;

    private readonly ILogger<ServerMotionDetectionService> logger;
    private readonly IMotionFrameReader? frameReader;
    private readonly TimeProvider timeProvider;
    private readonly bool runLoops;
    private readonly Func<MotionDetectionSettings, IMotionDetector> createDetector;
    private readonly ConcurrentDictionary<Guid, DetectionContext> contexts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerMotionDetectionService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="frameReader">
    /// Turns a captured picture into a grayscale one. Optional: a host that
    /// cannot read pictures (not Windows) runs without motion detection.
    /// </param>
    /// <param name="timeProvider">The clock; the system's when not given.</param>
    /// <param name="runLoops">
    /// Whether a started detection analyses pictures on its own. Tests turn
    /// it off and call <see cref="AnalyzeOnceAsync"/> themselves.
    /// </param>
    /// <param name="createDetector">Makes a camera's detector; the built-in one when not given.</param>
    public ServerMotionDetectionService(
        ILogger<ServerMotionDetectionService> logger,
        IMotionFrameReader? frameReader = null,
        TimeProvider? timeProvider = null,
        bool runLoops = true,
        Func<MotionDetectionSettings, IMotionDetector>? createDetector = null)
    {
        this.createDetector = createDetector ?? (s => new BuiltInMotionDetector(s));
        this.logger = logger;
        this.frameReader = frameReader;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.runLoops = runLoops;
    }

    /// <inheritdoc/>
    public event EventHandler<MotionDetectedEventArgs>? MotionDetected;

    /// <inheritdoc/>
    public event EventHandler<PictureAnalyzedEventArgs>? PictureAnalyzed;

    /// <summary>
    /// Gets the time between two analysed pictures for the given settings.
    /// </summary>
    /// <param name="settings">The camera's motion settings.</param>
    /// <returns>The interval.</returns>
    public static TimeSpan GetInterval(MotionDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var perSecond = Math.Clamp(settings.AnalysisFrameRate, 1, MaxAnalysesPerSecond);
        return TimeSpan.FromMilliseconds(1000.0 / perSecond);
    }

    /// <inheritdoc/>
    public void StartDetection(
        Guid cameraId,
        IMediaPipeline pipeline,
        MotionDetectionSettings? settings = null,
        IReadOnlyList<MotionZone>? zones = null)
        => Start(cameraId, pipeline, settings, zones, reportsMotion: true);

    /// <inheritdoc/>
    public void StartIndexing(
        Guid cameraId,
        IMediaPipeline pipeline,
        MotionDetectionSettings? settings = null,
        IReadOnlyList<MotionZone>? zones = null)
        => Start(cameraId, pipeline, settings, zones, reportsMotion: false);

    /// <inheritdoc/>
    public void UseZones(
        Guid cameraId,
        IReadOnlyList<MotionZone> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        if (contexts.TryGetValue(cameraId, out var context))
        {
            lock (context.Sync)
            {
                context.Detector.UseZones(zones);
            }
        }
    }

    /// <inheritdoc/>
    public void StopIndexing(Guid cameraId)
    {
        if (!contexts.TryGetValue(cameraId, out var context))
        {
            return;
        }

        lock (context.Sync)
        {
            context.Indexes = false;
            if (context.ReportsMotion)
            {
                return;
            }
        }

        Stop(cameraId);
    }

    /// <inheritdoc/>
    /// <remarks>A camera that is also analysed for its activity index goes on being analysed, without reporting motion.</remarks>
    public void StopDetection(Guid cameraId)
    {
        if (!contexts.TryGetValue(cameraId, out var context))
        {
            return;
        }

        bool indexes;
        bool wasActive;
        lock (context.Sync)
        {
            indexes = context.Indexes;
            wasActive = indexes && context.IsMotionActive;
            if (indexes)
            {
                context.ReportsMotion = false;
                context.IsMotionActive = false;
                context.LastBoundingBoxes = [];
            }
        }

        if (!indexes)
        {
            Stop(cameraId);
        }
        else if (wasActive)
        {
            RaiseMotionEnded(cameraId, context);
        }
    }

    /// <inheritdoc/>
    public bool IsDetectionActive(Guid cameraId)
        => contexts.TryGetValue(cameraId, out var ctx) && ctx.ReportsMotion;

    /// <inheritdoc/>
    public bool IsIndexing(Guid cameraId)
        => contexts.TryGetValue(cameraId, out var ctx) && ctx.Indexes;

    /// <inheritdoc/>
    public bool IsMotionDetected(Guid cameraId)
        => contexts.TryGetValue(cameraId, out var ctx) && ctx.IsMotionActive;

    /// <inheritdoc/>
    public IReadOnlyList<BoundingBox> GetLastBoundingBoxes(Guid cameraId)
        => contexts.TryGetValue(cameraId, out var ctx)
            ? ctx.LastBoundingBoxes
            : [];

    /// <inheritdoc/>
    public (int Width, int Height) GetAnalysisResolution(Guid cameraId)
        => contexts.TryGetValue(cameraId, out var ctx)
            ? GetAnalysisSize(ctx)
            : MotionAnalysisSize.For(0, 0, 0);

    /// <summary>
    /// Takes one picture from the camera and compares it with the one before.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>A task that completes when the picture has been analysed.</returns>
    public async Task AnalyzeOnceAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        if (frameReader is null || !contexts.TryGetValue(cameraId, out var context))
        {
            return;
        }

        byte[]? current;
        var (width, height) = GetAnalysisSize(context);
        try
        {
            // Grey straight from the decoded picture; a PNG read back as grey where the pipeline
            // cannot make one.
            current = await context.Pipeline.CaptureGreyAsync(width, height, cancellationToken).ConfigureAwait(false);
            if (current?.Length != width * height)
            {
                var picture = await context.Pipeline.CaptureFrameAsync(cancellationToken).ConfigureAwait(false);
                current = picture is { Length: > 0 }
                    ? frameReader.ReadGrayscale(picture, width, height)
                    : null;
            }

            context.ConsecutiveFailures = 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException)
        {
            // Whoever owns the pipeline closed it. There will be no more
            // pictures, so the detection ends, and any motion with it.
            LogPipelineClosed(cameraId);
            Stop(cameraId);
            return;
        }
        catch (Exception ex)
        {
            // A camera that dropped out is the pipeline owner's business;
            // here it only means there is no picture to look at.
            context.ConsecutiveFailures++;
            if (context.ConsecutiveFailures == 1 || context.ConsecutiveFailures % 240 == 0)
            {
                LogFrameCaptureFailed(ex, cameraId, context.ConsecutiveFailures);
            }

            current = null;
        }

        Analyze(cameraId, context, current, width, height);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var cameraId in contexts.Keys.ToList())
        {
            Stop(cameraId);
        }
    }

    private void Start(
        Guid cameraId,
        IMediaPipeline pipeline,
        MotionDetectionSettings? settings,
        IReadOnlyList<MotionZone>? zones,
        bool reportsMotion)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        if (frameReader is null)
        {
            LogMotionDetectionNotAvailable(cameraId);
            return;
        }

        if (contexts.TryGetValue(cameraId, out var running))
        {
            lock (running.Sync)
            {
                running.ReportsMotion |= reportsMotion;
                running.Indexes |= !reportsMotion;
            }

            return;
        }

        CancellationTokenSource? cts = null;
        try
        {
            cts = new CancellationTokenSource();
            var cameraSettings = settings ?? new MotionDetectionSettings();
            var detector = createDetector(cameraSettings);
            if (zones is { Count: > 0 })
            {
                detector.UseZones(zones);
            }

            var context = new DetectionContext(pipeline, cameraSettings, detector, cts)
            {
                ReportsMotion = reportsMotion,
                Indexes = !reportsMotion,
            };

            if (contexts.TryAdd(cameraId, context))
            {
                cts = null;
                if (runLoops)
                {
                    _ = RunDetectionLoopAsync(cameraId, context);
                }

                LogMotionDetectionStarted(cameraId);
            }
            else if (contexts.TryGetValue(cameraId, out var existing))
            {
                // Someone else started the camera in between: what this start wanted is added to it.
                lock (existing.Sync)
                {
                    existing.ReportsMotion |= reportsMotion;
                    existing.Indexes |= !reportsMotion;
                }
            }
        }
        finally
        {
            cts?.Dispose();
        }
    }

    // Ends the analysis of a camera altogether, and any motion with it.
    private void Stop(Guid cameraId)
    {
        if (!contexts.TryRemove(cameraId, out var context))
        {
            return;
        }

        context.Cts.Cancel();
        context.Cts.Dispose();

        // Whoever acted on the motion (a recording) has to hear that it is over.
        bool wasActive;
        lock (context.Sync)
        {
            wasActive = context.IsMotionActive;
            context.IsMotionActive = false;
            context.LastBoundingBoxes = [];
        }

        if (wasActive)
        {
            RaiseMotionEnded(cameraId, context);
        }

        LogMotionDetectionStopped(cameraId);
    }

    // The longer side as set, the other following the camera's picture as displayed.
    private static (int Width, int Height) GetAnalysisSize(
        DetectionContext context)
    {
        var (displayWidth, displayHeight) = context.Pipeline.DisplaySize;
        return MotionAnalysisSize.For(context.Settings.AnalysisWidth, displayWidth, displayHeight);
    }

    private void Analyze(
        Guid cameraId,
        DetectionContext context,
        byte[]? current,
        int width,
        int height)
    {
        MotionDetectedEventArgs? toRaise = null;
        PictureAnalyzedEventArgs? analyzed = null;
        var now = timeProvider.GetUtcNow();

        lock (context.Sync)
        {
            // Stopped while the picture was being captured.
            if (!contexts.ContainsKey(cameraId))
            {
                return;
            }

            // The detector is shared with the desktop: the comparison, the gate and the hold.
            // Without a picture, time still passes, so motion can end.
            var result = current is not null
                ? context.Detector.Process(new MotionFrame(current, width, height, now))
                : context.Detector.Idle(now);

            if (current is not null)
            {
                analyzed = new PictureAnalyzedEventArgs(cameraId, now, result);
            }

            // A camera analysed only for its activity index has no motion to report.
            if (context.ReportsMotion && result.Change is MotionHoldChange.Started or MotionHoldChange.Continues)
            {
                context.IsMotionActive = true;
                context.LastMotionAt = now;
                context.LastBoundingBoxes = result.Boxes;
                toRaise = new MotionDetectedEventArgs(
                    cameraId,
                    result.ChangePercent,
                    isMotionActive: true,
                    result.Boxes,
                    width,
                    height);
            }
            else if (context.ReportsMotion && result.Change == MotionHoldChange.Ended)
            {
                context.IsMotionActive = false;
                context.LastBoundingBoxes = [];
                toRaise = new MotionDetectedEventArgs(
                    cameraId,
                    result.ChangePercent,
                    isMotionActive: false,
                    boundingBoxes: null,
                    width,
                    height);
            }
        }

        if (toRaise is not null)
        {
            Raise(cameraId, toRaise);
        }

        if (analyzed is not null)
        {
            try
            {
                PictureAnalyzed?.Invoke(this, analyzed);
            }
            catch (Exception ex)
            {
                LogMotionSubscriberFailed(ex, cameraId);
            }
        }
    }

    private void RaiseMotionEnded(
        Guid cameraId,
        DetectionContext context)
    {
        var (width, height) = GetAnalysisSize(context);
        Raise(cameraId, new MotionDetectedEventArgs(cameraId, 0, isMotionActive: false, boundingBoxes: null, width, height));
    }

    // A subscriber that throws must not end the detection of the camera.
    private void Raise(
        Guid cameraId,
        MotionDetectedEventArgs args)
    {
        try
        {
            MotionDetected?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            LogMotionSubscriberFailed(ex, cameraId);
        }
    }

    private async Task RunDetectionLoopAsync(
        Guid cameraId,
        DetectionContext context)
    {
        try
        {
            var token = context.Cts.Token;
            using var timer = new PeriodicTimer(GetInterval(context.Settings), timeProvider);

            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await AnalyzeOnceAsync(cameraId, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping detection
        }
        catch (ObjectDisposedException)
        {
            // The detection was stopped between two pictures.
        }
        catch (Exception ex)
        {
            LogMotionDetectionLoopError(ex, cameraId);
        }
    }

    private sealed class DetectionContext(
        IMediaPipeline pipeline,
        MotionDetectionSettings settings,
        IMotionDetector detector,
        CancellationTokenSource cts)
    {
        public Lock Sync { get; } = new();

        public IMediaPipeline Pipeline { get; } = pipeline;

        public MotionDetectionSettings Settings { get; } = settings;

        public CancellationTokenSource Cts { get; } = cts;

        public bool IsMotionActive { get; set; }

        // Whether motion is reported (watched for motion), and whether the pictures go to the
        // activity index. At least one of them, or the camera is not analysed.
        public bool ReportsMotion { get; set; }

        public bool Indexes { get; set; }

        // The same detector as the desktop's, behind the shared seam.
        public IMotionDetector Detector { get; } = detector;

        public DateTimeOffset LastMotionAt { get; set; }

        public int ConsecutiveFailures { get; set; }

        public IReadOnlyList<BoundingBox> LastBoundingBoxes { get; set; } = [];
    }
}