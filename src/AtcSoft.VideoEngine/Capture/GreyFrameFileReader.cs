namespace AtcSoft.VideoEngine.Capture;

/// <summary>
/// Reads a recording file as grey pictures for analysis, as fast as it decodes: every frame is
/// decoded (a compressed picture needs the ones before it), and those asked for are scaled straight
/// to grey at the analysis size by swscale and turned the way the picture is displayed. No PNG, no
/// player. For *Analyse recording*, the storybook scorer and the sample app.
/// </summary>
public sealed unsafe class GreyFrameFileReader : IDisposable
{
    private readonly FrameScaler scaler = new();
    private RecordingFileDecoder? decoder;
    private bool disposed;

    private GreyFrameFileReader(
        int width,
        int height,
        VideoRotation rotation)
    {
        Width = width;
        Height = height;
        Rotation = rotation;
    }

    /// <summary>
    /// Gets the width of the pictures, as displayed.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the pictures, as displayed.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the clockwise rotation the pictures are turned by.
    /// </summary>
    public VideoRotation Rotation { get; }

    /// <summary>
    /// Gets how long the recording is, or <see cref="TimeSpan.Zero"/> when the file does not say.
    /// </summary>
    public TimeSpan Duration
        => decoder?.Duration ?? TimeSpan.Zero;

    /// <summary>
    /// Opens a recording file.
    /// </summary>
    /// <param name="filePath">The file.</param>
    /// <param name="width">The width of the pictures, as displayed.</param>
    /// <param name="height">The height of the pictures, as displayed.</param>
    /// <param name="rotation">The clockwise rotation the picture is displayed with.</param>
    /// <param name="decoderThreads">
    /// How many threads decode; 0 for one per processor. An app that records and shows live
    /// cameras at the same time passes a few, so the analysis does not crowd them out.
    /// </param>
    /// <returns>The reader.</returns>
    /// <exception cref="FFmpegException">The file cannot be opened or decoded.</exception>
    public static GreyFrameFileReader Open(
        string filePath,
        int width,
        int height,
        VideoRotation rotation = VideoRotation.None,
        int decoderThreads = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var reader = new GreyFrameFileReader(width, height, rotation);
        try
        {
            reader.decoder = RecordingFileDecoder.Open(filePath, decoderThreads);
            return reader;
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the pictures at most <paramref name="framesPerSecond"/> a second, from the start.
    /// </summary>
    /// <param name="framesPerSecond">How many pictures a second, at most.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The pictures, in the order they are shown.</returns>
    public IEnumerable<GreyFrame> ReadFrames(
        double framesPerSecond,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framesPerSecond);

        var interval = TimeSpan.FromSeconds(1 / framesPerSecond);
        var next = TimeSpan.Zero;
        while (!cancellationToken.IsCancellationRequested && ReadNext(next) is { } frame)
        {
            yield return frame;

            // On a fixed grid, so the rate does not drift below the one asked for; a gap in the
            // recording moves the grid on.
            next += interval;
            if (next <= frame.Position)
            {
                next = frame.Position + interval;
            }
        }
    }

    /// <summary>
    /// Reads the next picture at or after a place in the recording.
    /// </summary>
    /// <param name="notBefore">The earliest place wanted; pictures before it are decoded and passed over.</param>
    /// <returns>The picture, or <see langword="null"/> at the end of the recording.</returns>
    public GreyFrame? ReadNext(TimeSpan notBefore)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return decoder!.ReadNext(
            notBefore,
            (frame, position) => scaler.ToPixels((AVFrame*)frame, Width, Height, Rotation) is { } pixels
                ? new GreyFrame(position, Width, Height, pixels)
                : null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        decoder?.Dispose();
        scaler.Dispose();
    }
}