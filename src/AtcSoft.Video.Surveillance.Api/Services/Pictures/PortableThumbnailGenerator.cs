namespace AtcSoft.Video.Surveillance.Api.Services.Pictures;

/// <summary>
/// Makes recording thumbnails where GDI+ is not there (Linux, macOS, the Docker image): the same
/// pictures as the Windows generator, one or four frames of the recording's first seconds in a
/// grid, read and written with FFmpeg (<see cref="PictureCodec"/>).
/// </summary>
public sealed partial class PortableThumbnailGenerator : IThumbnailGeneratorService
{
    private const int FrameWidth = 320;
    private const int FrameHeight = 240;

    // Attempts beyond one per tile, for captures that return nothing because the stream has not
    // produced a picture yet.
    private const int ExtraAttempts = 6;

    private readonly ILogger<PortableThumbnailGenerator> logger;
    private readonly ConcurrentDictionary<Guid, CaptureContext> captures = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PortableThumbnailGenerator"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PortableThumbnailGenerator(
        ILogger<PortableThumbnailGenerator> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets or sets the pause between two frame captures.
    /// </summary>
    public TimeSpan CaptureInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The context is disposed by Write, which runs exactly once per capture.")]
    [SuppressMessage("Reliability", "CA2025:Do not pass 'IDisposable' instances into unawaited tasks", Justification = "The context is disposed by Write, which the capture task or StopCapture runs exactly once.")]
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

        if (tileCount != 1 && tileCount != 4)
        {
            tileCount = 4;
        }

        StopCapture(cameraId);

        var thumbnailPath = string.IsNullOrEmpty(thumbnailFilePath)
            ? Path.ChangeExtension(videoFilePath, ".png")
            : thumbnailFilePath;
        var context = new CaptureContext(cameraName, pipeline, videoFilePath, thumbnailPath, tileCount);
        if (!captures.TryAdd(cameraId, context))
        {
            context.Dispose();
            return;
        }

        LogStartingCapture(cameraName, thumbnailPath, tileCount);
        _ = Task.Run(() => RunCaptureAsync(cameraId, context));
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Write disposes the context on the pool thread it runs on.")]
    public void StopCapture(Guid cameraId)
    {
        if (captures.TryRemove(cameraId, out var context))
        {
            var frames = context.Complete();
            _ = Task.Run(() => Write(context, frames));
        }
    }

    /// <inheritdoc />
    public bool IsCaptureActive(Guid cameraId)
        => captures.ContainsKey(cameraId);

    /// <inheritdoc />
    public void StopAllCaptures()
    {
        foreach (var cameraId in captures.Keys.ToList())
        {
            StopCapture(cameraId);
        }
    }

    /// <summary>
    /// Draws the thumbnail of the frames, one or four, as a PNG.
    /// </summary>
    /// <param name="frames">The frames, in order: top left, top right, bottom left, bottom right.</param>
    /// <param name="tileCount">The number of tiles: 1 or 4.</param>
    /// <returns>The PNG, or <see langword="null"/> when it cannot be made.</returns>
    public static byte[]? Draw(
        IReadOnlyList<PictureFrame> frames,
        int tileCount)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var (width, height) = tileCount == 1
            ? (FrameWidth, FrameHeight)
            : (FrameWidth * 2, FrameHeight * 2);

        // Black where a tile has no frame.
        var canvas = new byte[width * height * 4];
        for (var i = 3; i < canvas.Length; i += 4)
        {
            canvas[i] = 255;
        }

        (int X, int Y)[] cells = [(0, 0), (FrameWidth, 0), (0, FrameHeight), (FrameWidth, FrameHeight)];
        for (var i = 0; i < Math.Min(tileCount, frames.Count); i++)
        {
            var frame = frames[i];
            var x = cells[i].X + ((FrameWidth - frame.Width) / 2);
            var y = cells[i].Y + ((FrameHeight - frame.Height) / 2);
            for (var row = 0; row < frame.Height; row++)
            {
                Buffer.BlockCopy(frame.Pixels, row * frame.Width * 4, canvas, (((y + row) * width) + x) * 4, frame.Width * 4);
            }
        }

        return PictureCodec.EncodePng(canvas, width, height);
    }

    /// <summary>
    /// Gets the size of a frame in its cell. A landscape frame fills the cell. A portrait frame,
    /// which a camera turned a quarter produces, is fitted so it is not squashed.
    /// </summary>
    public static (int Width, int Height) FitIntoCell(
        int width,
        int height)
    {
        if (width >= height || width <= 0)
        {
            return (FrameWidth, FrameHeight);
        }

        var scale = Math.Min((double)FrameWidth / width, (double)FrameHeight / height);
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private async Task RunCaptureAsync(
        Guid cameraId,
        CaptureContext context)
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

        // Whoever removes the entry writes the thumbnail, so it is written once.
        if (captures.TryRemove(new KeyValuePair<Guid, CaptureContext>(cameraId, context)))
        {
            Write(context, context.Complete());
        }
    }

    private async Task CaptureFrameAsync(
        CaptureContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var png = await context.Pipeline.CaptureFrameAsync(cancellationToken).ConfigureAwait(false);
            if (png is null || png.Length == 0)
            {
                LogCaptureNoData(context.CameraName);
                return;
            }

            if (PictureCodec.Decode(png, FitIntoCell) is { } frame)
            {
                context.TryAddFrame(frame);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCaptureFailed(ex, context.CameraName);
        }
    }

    private void Write(
        CaptureContext context,
        PictureFrame[] frames)
    {
        try
        {
            if (frames.Length == 0)
            {
                LogNoFrames(context.CameraName);
                return;
            }

            // A recording that ended before its first picture has no video (the recorder removes
            // it): it gets no thumbnail. Looked at before and after writing, as the video is closed
            // while this runs.
            if (!File.Exists(context.VideoPath))
            {
                LogSkippedNoVideo(context.CameraName, context.VideoPath);
                return;
            }

            if (Draw(frames, context.TileCount) is not { } png)
            {
                LogDrawFailed(context.CameraName);
                return;
            }

            var directory = Path.GetDirectoryName(context.ThumbnailPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Written beside it and moved into place: a reader never sees half a picture.
            var temporary = context.ThumbnailPath + ".tmp";
            File.WriteAllBytes(temporary, png);
            File.Move(temporary, context.ThumbnailPath, overwrite: true);
            if (!File.Exists(context.VideoPath))
            {
                File.Delete(context.ThumbnailPath);
                LogSkippedNoVideo(context.CameraName, context.VideoPath);
                return;
            }

            LogGenerated(context.CameraName, frames.Length, context.TileCount, context.ThumbnailPath);
        }
        catch (Exception ex)
        {
            LogGenerationFailed(ex, context.CameraName);
        }
        finally
        {
            context.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "[{CameraName}] Capturing a thumbnail of {TileCount} frames for {ThumbnailPath}")]
    private partial void LogStartingCapture(string cameraName, string thumbnailPath, int tileCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[{CameraName}] No picture yet for the thumbnail")]
    private partial void LogCaptureNoData(string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[{CameraName}] A frame for the thumbnail could not be captured")]
    private partial void LogCaptureFailed(Exception exception, string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[{CameraName}] No frame was captured; the recording gets no thumbnail")]
    private partial void LogNoFrames(string cameraName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[{CameraName}] The recording {VideoPath} has no video; it gets no thumbnail")]
    private partial void LogSkippedNoVideo(string cameraName, string videoPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[{CameraName}] The thumbnail could not be drawn")]
    private partial void LogDrawFailed(string cameraName);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{CameraName}] Thumbnail of {FrameCount} of {TileCount} frames written: {ThumbnailPath}")]
    private partial void LogGenerated(string cameraName, int frameCount, int tileCount, string thumbnailPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{CameraName}] The thumbnail could not be written")]
    private partial void LogGenerationFailed(Exception exception, string cameraName);

    private sealed class CaptureContext : IDisposable
    {
        private readonly Lock framesLock = new();
        private readonly List<PictureFrame> frames = [];
        private readonly CancellationTokenSource cancellation = new();
        private bool completed;

        public CaptureContext(
            string cameraName,
            IMediaPipeline pipeline,
            string videoPath,
            string thumbnailPath,
            int tileCount)
        {
            CameraName = cameraName;
            Pipeline = pipeline;
            VideoPath = videoPath;
            ThumbnailPath = thumbnailPath;
            TileCount = tileCount;
            CancellationToken = cancellation.Token;
        }

        public string CameraName { get; }

        public IMediaPipeline Pipeline { get; }

        public string VideoPath { get; }

        public string ThumbnailPath { get; }

        public int TileCount { get; }

        public CancellationToken CancellationToken { get; }

        public int FrameCount
        {
            get
            {
                lock (framesLock)
                {
                    return frames.Count;
                }
            }
        }

        public void TryAddFrame(PictureFrame frame)
        {
            lock (framesLock)
            {
                if (!completed)
                {
                    frames.Add(frame);
                }
            }
        }

        public PictureFrame[] Complete()
        {
            cancellation.Cancel();

            lock (framesLock)
            {
                completed = true;
                PictureFrame[] taken = [.. frames];
                frames.Clear();
                return taken;
            }
        }

        public void Dispose()
            => cancellation.Dispose();
    }
}