namespace AtcSoft.VideoEngine.Windows.Thumbnails;

using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

/// <summary>
/// Service for generating recording thumbnails by capturing frames and creating a grid layout.
/// Shared by the standalone app and the server; it draws with GDI+, so it is Windows-only.
/// </summary>
public partial class ThumbnailGeneratorService : IThumbnailGeneratorService
{
    private const int FrameWidth = 320;
    private const int FrameHeight = 240;

    // Attempts beyond one per tile, for captures that return nothing because
    // the stream has not produced a picture yet.
    private const int ExtraAttempts = 6;

    private readonly ILogger<ThumbnailGeneratorService> logger;
    private readonly ConcurrentDictionary<Guid, ThumbnailCaptureContext> captures = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ThumbnailGeneratorService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ThumbnailGeneratorService(ILogger<ThumbnailGeneratorService> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets or sets the pause between two frame captures.
    /// </summary>
    internal TimeSpan CaptureInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
    [SuppressMessage("Reliability", "CA2025:Do not pass 'IDisposable' instances into unawaited tasks", Justification = "The context is disposed by Finish, which the capture task or StopCapture runs exactly once; the task only reads the token afterwards.")]
    public void StartCapture(
        Guid cameraId,
        string cameraName,
        IMediaPipeline pipeline,
        string videoFilePath,
        int tileCount = 4,
        string? thumbnailFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(cameraName);

        // Validate tile count (must be 1 or 4)
        if (tileCount != 1 && tileCount != 4)
        {
            tileCount = 4;
        }

        // Stop any existing capture for this camera
        StopCapture(cameraId);

        var thumbnailPath = string.IsNullOrEmpty(thumbnailFilePath)
            ? Path.ChangeExtension(videoFilePath, ".png")
            : thumbnailFilePath;
        var context = new ThumbnailCaptureContext(cameraName, pipeline, thumbnailPath, tileCount);

        if (!captures.TryAdd(cameraId, context))
        {
            context.Dispose();
            return;
        }

        LogStartingThumbnailCapture(cameraName, thumbnailPath, tileCount);

        _ = Task.Run(() => RunCaptureAsync(cameraId, context));
    }

    /// <inheritdoc/>
    public void StopCapture(Guid cameraId)
    {
        if (captures.TryRemove(cameraId, out var context))
        {
            Finish(context);
        }
    }

    /// <inheritdoc/>
    public bool IsCaptureActive(Guid cameraId)
        => captures.ContainsKey(cameraId);

    /// <inheritdoc/>
    public void StopAllCaptures()
    {
        var cameraIds = captures.Keys.ToList();

        foreach (var cameraId in cameraIds)
        {
            StopCapture(cameraId);
        }
    }

    /// <summary>
    /// Gets how many frames the running capture of a camera holds so far.
    /// </summary>
    internal int CapturedFrameCount(Guid cameraId)
        => captures.TryGetValue(cameraId, out var context)
            ? context.FrameCount
            : 0;

    /// <summary>
    /// Captures one frame per interval until every tile has a frame, then
    /// writes the thumbnail. A recording starts the moment its stream opens,
    /// before the first picture is decoded, so an attempt that returns
    /// nothing is simply tried again a little later instead of leaving its
    /// tile black.
    /// </summary>
    private async Task RunCaptureAsync(
        Guid cameraId,
        ThumbnailCaptureContext context)
    {
        var cancellationToken = context.CancellationToken;
        var maxAttempts = context.TileCount + ExtraAttempts;

        try
        {
            for (var attempt = 0; attempt < maxAttempts && context.FrameCount < context.TileCount; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(CaptureInterval, cancellationToken).ConfigureAwait(false);
                }

                await CaptureFrameAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // StopCapture took over and writes the thumbnail.
            return;
        }

        // Finished on its own. Whoever removes the entry writes the thumbnail,
        // so a StopCapture racing with this cannot write it twice.
        if (captures.TryRemove(new KeyValuePair<Guid, ThumbnailCaptureContext>(cameraId, context)))
        {
            Finish(context);
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The bitmap is handed to the context, which gives it to Finish for disposal; it is disposed here when the context no longer takes it.")]
    private async Task CaptureFrameAsync(
        ThumbnailCaptureContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var pngBytes = await context.Pipeline.CaptureFrameAsync(cancellationToken).ConfigureAwait(false);
            if (pngBytes is null || pngBytes.Length == 0)
            {
                LogSnapshotCaptureNoData(context.CameraName);
                return;
            }

            using var memoryStream = new MemoryStream(pngBytes);
            var bitmap = new Drawing.Bitmap(memoryStream);
            var frameIndex = context.TryAddFrame(bitmap);
            if (frameIndex < 0)
            {
                // The capture was stopped while this frame was on its way.
                bitmap.Dispose();
                return;
            }

            LogCapturedFrame(frameIndex, context.CameraName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFrameCaptureFailed(ex, context.CameraName);
        }
    }

    /// <summary>
    /// Stops the capture, writes the thumbnail from the frames it has, and
    /// releases them. Called once per capture, by whoever removed it.
    /// </summary>
    private void Finish(ThumbnailCaptureContext context)
    {
        var frames = context.Complete();
        try
        {
            GenerateThumbnail(context, frames);
        }
        finally
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }

            context.Dispose();
        }
    }

    /// <summary>
    /// Gets where a frame is drawn inside its thumbnail cell. A landscape
    /// frame fills the cell. A portrait frame, which a camera rotated a
    /// quarter turn produces, is fitted and centred so it is not squashed.
    /// </summary>
    private static Drawing.Rectangle FitIntoCell(
        Drawing.Image frame,
        int cellX,
        int cellY)
    {
        if (frame.Width >= frame.Height || frame.Width <= 0)
        {
            return new Drawing.Rectangle(cellX, cellY, FrameWidth, FrameHeight);
        }

        var scale = Math.Min((double)FrameWidth / frame.Width, (double)FrameHeight / frame.Height);
        var width = Math.Max(1, (int)Math.Round(frame.Width * scale));
        var height = Math.Max(1, (int)Math.Round(frame.Height * scale));

        return new Drawing.Rectangle(
            cellX + ((FrameWidth - width) / 2),
            cellY + ((FrameHeight - height) / 2),
            width,
            height);
    }

    private void GenerateThumbnail(
        ThumbnailCaptureContext context,
        Drawing.Bitmap[] frames)
    {
        if (frames.Length == 0)
        {
            LogNoFramesCaptured(context.CameraName);
            return;
        }

        try
        {
            // Determine thumbnail dimensions based on tile count
            // 1 tile = 320x240 (single frame), 4 tiles = 640x480 (2x2 grid)
            var (thumbnailWidth, thumbnailHeight) = context.TileCount == 1
                ? (FrameWidth, FrameHeight)
                : (FrameWidth * 2, FrameHeight * 2);

            using var thumbnail = new Drawing.Bitmap(
                thumbnailWidth,
                thumbnailHeight,
                DrawingImaging.PixelFormat.Format24bppRgb);
            using var graphics = Drawing.Graphics.FromImage(thumbnail);

            // Fill background with black (for missing frames)
            graphics.Clear(Drawing.Color.Black);

            // Configure high-quality rendering
            graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality;

            if (context.TileCount == 1)
            {
                // Single tile: draw first frame scaled to fit
                var frame = frames.Length > 0 ? frames[0] : null;
                if (frame is not null)
                {
                    graphics.DrawImage(frame, FitIntoCell(frame, 0, 0));
                }
            }
            else
            {
                // Draw frames in 2x2 grid
                // Position mapping:
                // [0] Top-Left (0,0)     [1] Top-Right (320,0)
                // [2] Bottom-Left (0,240) [3] Bottom-Right (320,240)
                var positions = new[]
                {
                    (X: 0, Y: 0),
                    (X: FrameWidth, Y: 0),
                    (X: 0, Y: FrameHeight),
                    (X: FrameWidth, Y: FrameHeight),
                };

                for (var i = 0; i < context.TileCount; i++)
                {
                    var frame = i < frames.Length ? frames[i] : null;

                    if (frame is not null)
                    {
                        graphics.DrawImage(frame, FitIntoCell(frame, positions[i].X, positions[i].Y));
                    }

                    // Missing frames stay black (already filled)
                }
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(context.ThumbnailPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save thumbnail atomically: write to *.tmp then move into
            // place. The recordings browser may read the same path
            // concurrently; without atomic replace it can observe a
            // partially-written PNG (corrupt image or GDI+ IOException).
            var tempPath = context.ThumbnailPath + ".tmp";
            thumbnail.Save(tempPath, DrawingImaging.ImageFormat.Png);
            File.Move(tempPath, context.ThumbnailPath, overwrite: true);

            LogGeneratedThumbnail(
                context.CameraName,
                frames.Count(f => f is not null),
                context.TileCount,
                context.TileCount,
                context.ThumbnailPath);
        }
        catch (Exception ex)
        {
            LogThumbnailGenerationFailed(ex, context.CameraName);
        }
    }

    /// <summary>
    /// Context for tracking thumbnail capture state for a single camera.
    /// </summary>
    private sealed class ThumbnailCaptureContext : IDisposable
    {
        private readonly Lock framesLock = new();
        private readonly List<Drawing.Bitmap> capturedFrames = [];
        private readonly CancellationTokenSource cancellation = new();
        private bool completed;

        public ThumbnailCaptureContext(
            string cameraName,
            IMediaPipeline pipeline,
            string thumbnailPath,
            int tileCount = 4)
        {
            CameraName = cameraName;
            Pipeline = pipeline;
            ThumbnailPath = thumbnailPath;
            TileCount = tileCount;
            CancellationToken = cancellation.Token;
        }

        public string CameraName { get; }

        public IMediaPipeline Pipeline { get; }

        public string ThumbnailPath { get; }

        public int TileCount { get; }

        public CancellationToken CancellationToken { get; }

        public int FrameCount
        {
            get
            {
                lock (framesLock)
                {
                    return capturedFrames.Count;
                }
            }
        }

        /// <summary>
        /// Adds a captured frame and returns its index, or -1 when the
        /// capture is already complete and the caller keeps ownership.
        /// </summary>
        public int TryAddFrame(Drawing.Bitmap frame)
        {
            lock (framesLock)
            {
                if (completed)
                {
                    return -1;
                }

                capturedFrames.Add(frame);
                return capturedFrames.Count - 1;
            }
        }

        /// <summary>
        /// Ends the capture and hands its frames to the caller, who disposes them.
        /// </summary>
        public Drawing.Bitmap[] Complete()
        {
            cancellation.Cancel();

            lock (framesLock)
            {
                completed = true;
                Drawing.Bitmap[] frames = [.. capturedFrames];
                capturedFrames.Clear();
                return frames;
            }
        }

        public void Dispose()
            => cancellation.Dispose();
    }
}