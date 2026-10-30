// ReSharper disable RedundantArgumentDefaultValue
namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// Service for detecting motion in camera video streams using frame differencing.
/// Uses a staggered scheduler to analyze cameras in sequence, preventing CPU spikes.
/// A camera that records is also analysed for its activity index (<see cref="StartIndexing"/>);
/// one analysed only for the index reports no motion.
/// </summary>
[Registration(Lifetime.Singleton)]
public partial class MotionDetectionService : IMotionDetectionService, IDisposable
{
    private const int MinTargetFps = 2;
    private const int MaxTargetFps = 15;
    private const int MinSchedulerIntervalMs = 16; // ~60 Hz ceiling for scheduler ticks
    private const int MaxSchedulerIntervalMs = 500; // Maximum 500ms between analyses

    // A camera analysed only for its activity index needs no more than four pictures a second,
    // whatever the motion settings ask for: it runs for every camera that records.
    private static readonly TimeSpan IndexOnlyInterval = TimeSpan.FromMilliseconds(250);

    private readonly ConcurrentDictionary<Guid, MotionDetectionContext> contexts = new();
    private readonly List<Guid> scheduledCameras = [];
    private readonly Lock schedulerLock = new();
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly ILogger<MotionDetectionService> logger;
    private readonly IMotionFrameReader frameReader;
    private DispatcherTimer? schedulerTimer;
    private int currentCameraIndex;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionDetectionService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="frameReader">Reads a captured picture as grey at the analysis size; the same reader as the server's.</param>
    public MotionDetectionService(
        ILogger<MotionDetectionService> logger,
        IMotionFrameReader frameReader)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.frameReader = frameReader ?? throw new ArgumentNullException(nameof(frameReader));
    }

    /// <inheritdoc/>
    public event EventHandler<MotionDetectedEventArgs>? MotionDetected;

    /// <inheritdoc/>
    public event EventHandler<PictureAnalyzedEventArgs>? PictureAnalyzed;

    /// <inheritdoc/>
    public void StartDetection(
        Guid cameraId,
        IMediaPipeline pipeline,
        MotionDetectionSettings? settings = null,
        IReadOnlyList<MotionZone>? zones = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        // Started again with new settings: begin anew, still feeding the index if it did.
        var indexes = contexts.TryGetValue(cameraId, out var running) && running.Indexes;
        Remove(cameraId);
        Add(cameraId, pipeline, settings, zones, reportsMotion: true, indexes);
    }

    /// <inheritdoc/>
    /// <remarks>A camera that is also analysed for its activity index goes on being analysed, without reporting motion.</remarks>
    public void StopDetection(Guid cameraId)
    {
        if (contexts.TryGetValue(cameraId, out var context) && context.Indexes)
        {
            context.ReportsMotion = false;
            context.IsMotionDetected = false;
            context.LastBoundingBoxes = [];
            return;
        }

        Remove(cameraId);
    }

    /// <inheritdoc/>
    public void StartIndexing(
        Guid cameraId,
        IMediaPipeline pipeline,
        MotionDetectionSettings? settings = null,
        IReadOnlyList<MotionZone>? zones = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        if (contexts.TryGetValue(cameraId, out var running))
        {
            running.Indexes = true;
            return;
        }

        Add(cameraId, pipeline, settings, zones, reportsMotion: false, indexes: true);
    }

    /// <inheritdoc/>
    public void UseZones(
        Guid cameraId,
        IReadOnlyList<MotionZone> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        if (contexts.TryGetValue(cameraId, out var context))
        {
            context.Detector.UseZones(zones);
        }
    }

    /// <inheritdoc/>
    public void StopIndexing(Guid cameraId)
    {
        if (!contexts.TryGetValue(cameraId, out var context))
        {
            return;
        }

        context.Indexes = false;
        if (!context.ReportsMotion)
        {
            Remove(cameraId);
        }
    }

    /// <inheritdoc/>
    public bool IsIndexing(Guid cameraId)
        => contexts.TryGetValue(cameraId, out var context) && context.Indexes;

    /// <inheritdoc/>
    public bool IsDetectionActive(Guid cameraId)
        => contexts.TryGetValue(cameraId, out var context) && context.ReportsMotion;

    /// <inheritdoc/>
    public bool IsMotionDetected(Guid cameraId)
    {
        if (contexts.TryGetValue(cameraId, out var context))
        {
            return context.IsMotionDetected;
        }

        return false;
    }

    /// <inheritdoc/>
    public IReadOnlyList<BoundingBox> GetLastBoundingBoxes(Guid cameraId)
        => contexts.TryGetValue(cameraId, out var context)
            ? context.LastBoundingBoxes
            : [];

    /// <inheritdoc/>
    public (int Width, int Height) GetAnalysisResolution(Guid cameraId)
        => contexts.TryGetValue(cameraId, out var context)
            ? AnalysisSize(context)
            : MotionAnalysisSize.For(0, 0, 0);

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
            // Stop scheduler
            lock (schedulerLock)
            {
                schedulerTimer?.Stop();
                schedulerTimer = null;
                scheduledCameras.Clear();
            }

            foreach (var cameraId in contexts.Keys.ToList())
            {
                Remove(cameraId);
            }
        }

        disposed = true;
    }

    private void Add(
        Guid cameraId,
        IMediaPipeline pipeline,
        MotionDetectionSettings? settings,
        IReadOnlyList<MotionZone>? zones,
        bool reportsMotion,
        bool indexes)
    {
        var context = new MotionDetectionContext(cameraId, pipeline, settings ?? new MotionDetectionSettings(), zones)
        {
            ReportsMotion = reportsMotion,
            Indexes = indexes,
        };

        // Mutate contexts and scheduledCameras together under the same lock so
        // a concurrent Start/Stop pair cannot leave a camera scheduled but
        // missing from contexts (which would silently stop analysis for it).
        bool added;
        lock (schedulerLock)
        {
            added = contexts.TryAdd(cameraId, context);
            if (added)
            {
                scheduledCameras.Add(cameraId);
                UpdateSchedulerInterval();
            }
        }

        if (!added)
        {
            // Someone else started the camera in between: what this start wanted is added to it.
            if (contexts.TryGetValue(cameraId, out var existing))
            {
                existing.ReportsMotion |= reportsMotion;
                existing.Indexes |= indexes;
            }

            context.Dispose();
        }
    }

    private void Remove(Guid cameraId)
    {
        // Same atomic invariant as Add: both maps move together.
        MotionDetectionContext? context;
        lock (schedulerLock)
        {
            contexts.TryRemove(cameraId, out context);
            scheduledCameras.Remove(cameraId);
            UpdateSchedulerInterval();
        }

        context?.Dispose();
    }

    /// <summary>
    /// Updates the scheduler interval based on the number of cameras.
    /// Spreads analysis evenly across time to prevent CPU spikes.
    /// </summary>
    private void UpdateSchedulerInterval()
    {
        // Must be called within schedulerLock
        if (scheduledCameras.Count == 0)
        {
            schedulerTimer?.Stop();
            schedulerTimer = null;
            return;
        }

        // Compute max AnalysisFrameRate across all active cameras (clamped to MinTargetFps..MaxTargetFps)
        var maxFps = MinTargetFps;
        foreach (var id in scheduledCameras)
        {
            if (contexts.TryGetValue(id, out var ctx))
            {
                var fps = Math.Clamp(ctx.Settings.AnalysisFrameRate, MinTargetFps, MaxTargetFps);
                if (fps > maxFps)
                {
                    maxFps = fps;
                }
            }
        }

        // Calculate interval: total analyses per second = cameras * maxFps
        // Interval = 1000ms / totalAnalysesPerSecond
        var totalAnalysesPerSecond = scheduledCameras.Count * maxFps;
        var intervalMs = Math.Max(MinSchedulerIntervalMs, Math.Min(MaxSchedulerIntervalMs, 1000.0 / totalAnalysesPerSecond));

        if (schedulerTimer is null)
        {
            // On the service's own dispatcher: a recording can start the index from another thread,
            // and a timer bound to that thread would never tick.
            schedulerTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(intervalMs),
            };

            schedulerTimer.Tick += OnSchedulerTick;
            schedulerTimer.Start();
        }
        else
        {
            schedulerTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
        }
    }

    /// <summary>
    /// Scheduler tick handler - captures a snapshot on the UI thread, then
    /// dispatches heavy image processing to a background thread.
    /// </summary>
    private void OnSchedulerTick(
        object? sender,
        EventArgs e)
    {
        Guid cameraId;

        lock (schedulerLock)
        {
            if (scheduledCameras.Count == 0)
            {
                return;
            }

            // Get next camera in round-robin fashion
            currentCameraIndex %= scheduledCameras.Count;
            cameraId = scheduledCameras[currentCameraIndex];
            currentCameraIndex++;
        }

        if (!contexts.TryGetValue(cameraId, out var context))
        {
            return;
        }

        // Skip if the previous frame is still being analyzed
        if (context.IsAnalyzing)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!context.ReportsMotion && now - context.LastAnalysisStartedUtc < IndexOnlyInterval)
        {
            return;
        }

        context.LastAnalysisStartedUtc = now;
        context.IsAnalyzing = true;
        _ = CaptureAndProcessAsync(cameraId, context);
    }

    private async Task CaptureAndProcessAsync(
        Guid cameraId,
        MotionDetectionContext context)
    {
        try
        {
            // Grey straight from the decoded picture; a PNG read back as grey where the pipeline
            // cannot make one.
            var (width, height) = AnalysisSize(context);
            var grey = await context.Pipeline.CaptureGreyAsync(width, height).ConfigureAwait(false);
            if (grey?.Length != width * height)
            {
                grey = null;
            }

            var frameBytes = grey is null ? await context.Pipeline.CaptureFrameAsync().ConfigureAwait(false) : null;
            if (grey is null && (frameBytes is null || frameBytes.Length == 0))
            {
                return;
            }

            ProcessCapturedFrame(cameraId, frameBytes, grey);
            context.ConsecutiveFails = 0;
        }
        catch (Exception ex)
        {
            context.ConsecutiveFails++;
            if (context.ConsecutiveFails == 1 || context.ConsecutiveFails % 60 == 0)
            {
                LogFrameCaptureFailed(ex, cameraId, context.ConsecutiveFails);
            }
        }
        finally
        {
            context.IsAnalyzing = false;
        }
    }

    // The longer side as set, the other following the camera's picture as displayed.
    private static (int Width, int Height) AnalysisSize(
        MotionDetectionContext context)
    {
        var (displayWidth, displayHeight) = context.Pipeline.DisplaySize;
        return MotionAnalysisSize.For(context.Settings.AnalysisWidth, displayWidth, displayHeight);
    }

    private void ProcessCapturedFrame(
        Guid cameraId,
        byte[]? frameBytes,
        byte[]? grey)
    {
        if (!contexts.TryGetValue(cameraId, out var context))
        {
            return;
        }

        try
        {
            var (analysisWidth, analysisHeight) = AnalysisSize(context);

            // Grey from the pipeline, or a PNG made grey and downscaled by the same reader the server uses.
            var currentFrame = grey ?? (frameBytes is null ? null : frameReader.ReadGrayscale(frameBytes, analysisWidth, analysisHeight));
            if (currentFrame is null)
            {
                return;
            }

            // The detector is shared with the server: the comparison, the gate and the
            // post-motion hold (ending motion on the first quiet picture made the boxes blink).
            var now = DateTimeOffset.UtcNow;
            var result = context.Detector.Process(new MotionFrame(currentFrame, analysisWidth, analysisHeight, now));

            // The activity index is written off the UI thread; its writer has its own lock.
            PictureAnalyzed?.Invoke(this, new PictureAnalyzedEventArgs(cameraId, now, result));

            // A camera analysed only for its activity index has no motion to report.
            if (!context.ReportsMotion)
            {
                return;
            }

            var changePercent = result.ChangePercent;
            var boundingBoxes = result.Boxes;
            switch (result.Change)
            {
                case MotionHoldChange.Started:
                case MotionHoldChange.Continues:
                    context.LastMotionTime = DateTime.UtcNow;
                    context.LastBoundingBoxes = boundingBoxes;

                    // Marshal to UI thread since subscribers access DependencyProperties
                    RaiseMotionDetected(new MotionDetectedEventArgs(
                        cameraId,
                        changePercent,
                        isMotionActive: true,
                        boundingBoxes,
                        analysisWidth,
                        analysisHeight));
                    break;
                case MotionHoldChange.Ended:
                    context.LastBoundingBoxes = [];
                    RaiseMotionDetected(new MotionDetectedEventArgs(
                        cameraId,
                        changePercent,
                        isMotionActive: false,
                        boundingBoxes: null,
                        analysisWidth,
                        analysisHeight));
                    break;
            }

            context.IsMotionDetected = result.IsActive;
        }
        catch (Exception ex)
        {
            LogFrameAnalysisFailed(ex, cameraId);
        }
    }

    /// <summary>
    /// Raises the MotionDetected event on the UI thread to avoid cross-thread
    /// access violations on WPF DependencyProperties in event handlers.
    /// </summary>
    private void RaiseMotionDetected(MotionDetectedEventArgs args)
    {
        if (dispatcher.CheckAccess())
        {
            MotionDetected?.Invoke(this, args);
        }
        else
        {
            _ = dispatcher.BeginInvoke(() => MotionDetected?.Invoke(this, args));
        }
    }
}