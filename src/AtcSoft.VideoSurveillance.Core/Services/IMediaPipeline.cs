namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Abstraction over a media playback/capture pipeline.
/// Both WPF and Server implement via AtcSoft.VideoEngine (in-process FFmpeg).
/// </summary>
public interface IMediaPipeline : IDisposable
{
    /// <summary>
    /// Opens a stream from the specified URI. Network-camera shortcut
    /// equivalent to
    /// <c>Open(new SourceLocator(streamUri), settings)</c>; default
    /// implementation forwards to the source-locator overload so
    /// implementations only need to override one method.
    /// </summary>
    void Open(
        Uri streamUri,
        StreamSettings settings)
    {
        ArgumentNullException.ThrowIfNull(streamUri);
        Open(new SourceLocator(streamUri), settings);
    }

    /// <summary>
    /// Opens a source described by a <see cref="SourceLocator"/>.
    /// Implementations that support local-device sources (USB,
    /// DirectShow, V4L2) read
    /// <see cref="SourceLocator.InputFormat"/> and
    /// <see cref="SourceLocator.RawDeviceSpec"/> from the locator;
    /// implementations that only support network sources can fall back
    /// to <see cref="SourceLocator.Uri"/>.
    /// </summary>
    void Open(
        SourceLocator locator,
        StreamSettings settings);

    /// <summary>
    /// Closes the current stream.
    /// </summary>
    void Close();

    /// <summary>
    /// Starts recording the stream to a file.
    /// </summary>
    void StartRecording(string outputFilePath);

    /// <summary>
    /// Stops the current recording.
    /// </summary>
    void StopRecording();

    /// <summary>
    /// Atomically transitions the active recording to a new output file.
    /// Implementations should perform the close+open under a single lock so
    /// that packets arriving mid-switch land in either the previous or the
    /// new segment — never the close/open gap.
    /// Default implementation falls back to <see cref="StopRecording"/>
    /// followed by <see cref="StartRecording"/>; override for atomicity.
    /// </summary>
    void SwitchRecording(string newOutputFilePath)
    {
        StopRecording();
        StartRecording(newOutputFilePath);
    }

    /// <summary>
    /// Gets a value indicating whether recording is currently active.
    /// </summary>
    bool IsRecordingActive { get; }

    /// <summary>
    /// Gets why the recording can take no more data (a full disk, a broken output), or
    /// <see langword="null"/> while it records.
    /// </summary>
    string? RecordingError => null;

    /// <summary>
    /// Gets a value indicating whether the pipeline is writing a live
    /// stream for browsers.
    /// </summary>
    bool IsLiveStreamActive => false;

    /// <summary>
    /// Starts writing a live stream for browsers (an HLS playlist with its
    /// segments next to it) from the source the pipeline has open, next to
    /// any recording.
    /// </summary>
    /// <param name="playlistPath">The playlist file (.m3u8) to write, in a folder of its own.</param>
    void StartLiveStream(string playlistPath)
        => throw new NotSupportedException("This pipeline cannot write a live stream.");

    /// <summary>
    /// Stops the live stream.
    /// </summary>
    void StopLiveStream()
    {
    }

    /// <summary>
    /// Captures a single frame from the stream as PNG image bytes.
    /// </summary>
    Task<byte[]?> CaptureFrameAsync(CancellationToken ct = default);

    /// <summary>
    /// Captures the latest picture as a grey picture for motion detection: one byte per pixel, row
    /// by row, at the size asked for, turned the way the picture is displayed. Much cheaper than a
    /// PNG from <see cref="CaptureFrameAsync"/> read back as grey.
    /// </summary>
    /// <param name="width">The width, as displayed.</param>
    /// <param name="height">The height, as displayed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pixels, or <see langword="null"/> when there is no picture or the pipeline cannot make one (then use the PNG).</returns>
    Task<byte[]?> CaptureGreyAsync(
        int width,
        int height,
        CancellationToken ct = default)
        => Task.FromResult<byte[]?>(null);

    /// <summary>
    /// Gets the size of the picture as displayed (turned by the rotation), for the size it is
    /// analysed at (<see cref="Helpers.MotionDetection.MotionAnalysisSize"/>); (0, 0) while no picture
    /// has arrived or the pipeline cannot tell.
    /// </summary>
    (int Width, int Height) DisplaySize
        => (0, 0);

    /// <summary>
    /// Sets the clockwise rotation applied to live display, snapshots, and
    /// (via container metadata) any active or future recording.
    /// </summary>
    void SetRotation(CameraRotation rotation);

    /// <summary>
    /// Gets the current frames per second.
    /// </summary>
    double CurrentFps { get; }

    /// <summary>
    /// Gets the total number of frames decoded.
    /// </summary>
    long FramesDecoded { get; }

    /// <summary>
    /// Gets the UTC timestamp of the last packet successfully read from the
    /// underlying demuxer, or <see cref="DateTime.MinValue"/> if no packet has
    /// arrived yet. Used by the server-side stream-stale watchdog to detect
    /// wedged pipelines (socket open but no packets) without waiting for the
    /// VideoEngine's consecutive-read-errors threshold.
    /// </summary>
    DateTime LastPacketUtc { get; }

    /// <summary>
    /// Occurs when the connection state changes.
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
}