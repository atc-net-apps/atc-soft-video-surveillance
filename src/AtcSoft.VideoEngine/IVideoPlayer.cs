namespace AtcSoft.VideoEngine;

/// <summary>
/// High-level interface for a video player capable of opening streams,
/// recording, and capturing frames. Implementations may use GPU-accelerated
/// decoding/rendering (WPF) or CPU-only decoding (headless server).
/// </summary>
public interface IVideoPlayer : IDisposable
{
    /// <summary>
    /// Gets the current state of the player.
    /// </summary>
    PlayerState State { get; }

    /// <summary>
    /// Gets information about the current video stream, or <c>null</c> if no stream is open.
    /// </summary>
    VideoStreamInfo? StreamInfo { get; }

    /// <summary>
    /// Gets the current frames per second being decoded/displayed.
    /// </summary>
    double CurrentFps { get; }

    /// <summary>
    /// Gets the total number of frames decoded since the stream was opened.
    /// </summary>
    long FramesDecoded { get; }

    /// <summary>
    /// Gets the UTC timestamp of the last packet successfully read from the
    /// demuxer, or <see cref="DateTime.MinValue"/> if no packet has arrived
    /// since the most recent <see cref="Open"/>. Ticks at packet rate (typically
    /// 25–60 Hz on RTSP), making it suitable for stream-stale detection by the
    /// server reaper without waiting for the slower keyframe cadence implied by
    /// <see cref="FramesDecoded"/>.
    /// </summary>
    DateTime LastPacketUtc { get; }

    /// <summary>
    /// Gets a value indicating whether recording is currently active.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Gets why the recording can take no more packets (a full disk, a broken output), or
    /// <see langword="null"/> while it records.
    /// </summary>
    string? RecordingError => null;

    /// <summary>
    /// Gets the GPU accelerator used by this player, or <c>null</c> if CPU-only.
    /// </summary>
    IGpuAccelerator? GpuAccelerator { get; }

    /// <summary>
    /// Opens a stream from the specified URI.
    /// </summary>
    /// <param name="streamUri">The URI of the stream to open (rtsp://, http://, etc.).</param>
    /// <param name="options">Stream options controlling latency, transport, and decoding.</param>
    void Open(
        Uri streamUri,
        StreamOptions? options = null);

    /// <summary>
    /// Closes the current stream and releases resources.
    /// </summary>
    void Close();

    /// <summary>
    /// Starts recording the current stream to a file.
    /// </summary>
    /// <param name="outputFilePath">The output file path (extension determines container format).</param>
    void StartRecording(string outputFilePath);

    /// <summary>
    /// Stops the current recording.
    /// </summary>
    void StopRecording();

    /// <summary>
    /// Atomically transitions the active recording to a new file.
    /// Packets arriving from the demux thread between Close and Open are
    /// not silently dropped — they land in either the previous or the new
    /// segment. If no recording is currently active, behaves like
    /// <see cref="StartRecording"/>.
    /// </summary>
    void SwitchRecording(string newOutputFilePath);

    /// <summary>
    /// Gets a value indicating whether the player is writing a live stream
    /// for browsers.
    /// </summary>
    bool IsStreaming => false;

    /// <summary>
    /// Starts writing a live stream for browsers: an HLS playlist at
    /// <paramref name="playlistPath"/> with its segments next to it. It runs
    /// next to a recording, from the same open source. A source that
    /// delivers uncompressed video is encoded to H.264; any other is passed
    /// on as it is.
    /// </summary>
    /// <param name="playlistPath">The playlist file (.m3u8) to write, in a folder of its own.</param>
    void StartStreaming(string playlistPath)
        => throw new NotSupportedException("This player cannot write a live stream.");

    /// <summary>
    /// Stops the live stream.
    /// </summary>
    void StopStreaming()
    {
    }

    /// <summary>
    /// Captures a single frame from the stream as PNG image bytes.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>PNG-encoded bytes, or <c>null</c> if capture failed.</returns>
    Task<byte[]?> CaptureFrameAsync(CancellationToken ct = default);

    /// <summary>
    /// Captures the latest picture as a grey picture for analysis: scaled straight to grey at the
    /// size asked for and turned the way the picture is displayed, with no PNG between. Much cheaper
    /// than <see cref="CaptureFrameAsync"/> for motion detection.
    /// </summary>
    /// <param name="width">The width of the grey picture, as displayed.</param>
    /// <param name="height">The height of the grey picture, as displayed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The picture, or <see langword="null"/> when there is none or this player cannot make one.</returns>
    Task<Capture.GreyFrame?> CaptureGreyAsync(
        int width,
        int height,
        CancellationToken ct = default)
        => Task.FromResult<Capture.GreyFrame?>(null);

    /// <summary>
    /// Sets the clockwise rotation applied to the live video, snapshots, and
    /// (via container metadata) any active or future recording.
    /// Safe to call before <see cref="Open"/> or while playing; the next
    /// processed frame and any subsequent <see cref="StartRecording"/> reflect
    /// the new value.
    /// </summary>
    void SetRotation(VideoRotation rotation);

    /// <summary>
    /// Occurs when the player state changes.
    /// </summary>
    event EventHandler<PlayerStateChangedEventArgs>? StateChanged;
}