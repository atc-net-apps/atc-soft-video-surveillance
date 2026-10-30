namespace AtcSoft.VideoEngine.Capture;

/// <summary>
/// Reads small colour pictures of a recording file at chosen places, as fast as it decodes: every
/// frame is decoded, and those asked for are scaled straight to BGRA by swscale and turned the way
/// the picture is displayed. For the previews a timeline shows under the mouse.
/// </summary>
public sealed unsafe class PictureFileReader : IDisposable
{
    private readonly FrameScaler scaler = new(AVPixelFormat.Bgra);
    private RecordingFileDecoder? decoder;
    private bool disposed;

    private PictureFileReader(
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
    /// <param name="decoderThreads">How many threads decode; 0 for one per processor.</param>
    /// <returns>The reader.</returns>
    /// <exception cref="FFmpegException">The file cannot be opened or decoded.</exception>
    public static PictureFileReader Open(
        string filePath,
        int width,
        int height,
        VideoRotation rotation = VideoRotation.None,
        int decoderThreads = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var reader = new PictureFileReader(width, height, rotation);
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
    /// Reads the next picture at or after a place in the recording.
    /// </summary>
    /// <param name="notBefore">The earliest place wanted; pictures before it are decoded and passed over.</param>
    /// <returns>The picture, or <see langword="null"/> at the end of the recording.</returns>
    public PictureFrame? ReadNext(TimeSpan notBefore)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return decoder!.ReadNext(
            notBefore,
            (frame, position) => scaler.ToPixels((AVFrame*)frame, Width, Height, Rotation) is { } pixels
                ? new PictureFrame(position, Width, Height, pixels)
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