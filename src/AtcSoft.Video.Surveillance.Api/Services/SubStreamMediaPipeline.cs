namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The pipeline of a camera with a sub-stream: two connections to the camera, used as one.
/// The sub-stream (low resolution, decoded) gives the pictures: motion detection, the activity
/// index and the live grid. The main stream (full resolution, not decoded) records: the
/// recordings, their pre-roll and their files are of the main stream.
/// </summary>
/// <remarks>
/// The picture size it reports (<see cref="DisplaySize"/>) is the main stream's, which the
/// stream reports without being decoded: the motion analysis takes its grey pictures in that
/// shape, so a sub-stream squeezed to another shape (640×480 under 1920×1080) is analysed as
/// the main stream shows it, and the zones and boxes land where they are on the main stream.
/// <para>
/// Its state is the main stream's, because that is what records. A sub-stream that fails puts
/// the whole pipeline in error and is noted in <see cref="SubStreamAvailability"/>: the camera is
/// then opened again on its main stream alone, for a while.
/// </para>
/// </remarks>
public sealed partial class SubStreamMediaPipeline : IMediaPipeline
{
    private readonly VideoEngineMediaPipeline main;
    private readonly VideoEngineMediaPipeline sub;
    private readonly Action<string> subStreamFailed;
    private readonly Action subStreamPlayed;
    private readonly string source;
    private readonly Microsoft.Extensions.Logging.ILogger logger;
    private ConnectionState state = ConnectionState.Disconnected;
    private int subFailed;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubStreamMediaPipeline"/> class. Both
    /// pipelines are opened by the caller; this one owns and disposes them.
    /// </summary>
    /// <param name="main">The main stream, opened without decoding.</param>
    /// <param name="sub">The sub-stream, decoded.</param>
    /// <param name="subStreamFailed">Called once when the sub-stream fails, with the reason.</param>
    /// <param name="subStreamPlayed">Called when the sub-stream plays.</param>
    /// <param name="source">The camera's name, for the log.</param>
    /// <param name="logger">Where a subscriber that fails is written.</param>
    public SubStreamMediaPipeline(
        VideoEngineMediaPipeline main,
        VideoEngineMediaPipeline sub,
        Action<string> subStreamFailed,
        Action subStreamPlayed,
        string source = "",
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        this.main = main ?? throw new ArgumentNullException(nameof(main));
        this.sub = sub ?? throw new ArgumentNullException(nameof(sub));
        this.subStreamFailed = subStreamFailed ?? throw new ArgumentNullException(nameof(subStreamFailed));
        this.subStreamPlayed = subStreamPlayed ?? throw new ArgumentNullException(nameof(subStreamPlayed));
        this.source = source ?? string.Empty;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        this.main.ConnectionStateChanged += OnMainStateChanged;
        this.sub.ConnectionStateChanged += OnSubStateChanged;
    }

    /// <inheritdoc />
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public bool IsRecordingActive => main.IsRecordingActive;

    /// <inheritdoc />
    public string? RecordingError => main.RecordingError;

    /// <inheritdoc />
    public bool IsLiveStreamActive => sub.IsLiveStreamActive;

    /// <inheritdoc />
    public double CurrentFps => sub.CurrentFps;

    /// <inheritdoc />
    public long FramesDecoded => sub.FramesDecoded;

    /// <inheritdoc />
    /// <remarks>The older of the two streams' last packets: either going silent is a dead pipeline.</remarks>
    public DateTime LastPacketUtc
    {
        get
        {
            var mainPacket = main.LastPacketUtc;
            var subPacket = sub.LastPacketUtc;
            return mainPacket == DateTime.MinValue || subPacket == DateTime.MinValue
                ? DateTime.MinValue
                : mainPacket < subPacket ? mainPacket : subPacket;
        }
    }

    /// <inheritdoc />
    public (int Width, int Height) DisplaySize
        => main.DisplaySize is { Width: > 0, Height: > 0 } size ? size : sub.DisplaySize;

    /// <inheritdoc />
    /// <remarks>Both streams are opened by the factory, each with its own address.</remarks>
    public void Open(
        SourceLocator locator,
        StreamSettings settings)
        => throw new NotSupportedException("A sub-stream pipeline is opened by its factory.");

    /// <inheritdoc />
    public void Close()
    {
        main.Close();
        sub.Close();
    }

    /// <inheritdoc />
    public DateTime? FindRecordingStart(TimeSpan preRoll)
        => main.FindRecordingStart(preRoll);

    /// <inheritdoc />
    public void StartRecording(string outputFilePath)
        => main.StartRecording(outputFilePath);

    /// <inheritdoc />
    public void StartRecording(
        string outputFilePath,
        DateTime startFromUtc)
        => main.StartRecording(outputFilePath, startFromUtc);

    /// <inheritdoc />
    public void StopRecording()
        => main.StopRecording();

    /// <inheritdoc />
    public void SwitchRecording(string newOutputFilePath)
        => main.SwitchRecording(newOutputFilePath);

    /// <inheritdoc />
    public void SwitchRecording(
        string newOutputFilePath,
        DateTime startFromUtc)
        => main.SwitchRecording(newOutputFilePath, startFromUtc);

    /// <inheritdoc />
    /// <remarks>The live grid's stream: the sub-stream, as it is.</remarks>
    public void StartLiveStream(string playlistPath)
        => sub.StartLiveStream(playlistPath);

    /// <inheritdoc />
    public void StopLiveStream()
        => sub.StopLiveStream();

    /// <inheritdoc />
    /// <remarks>The sub-stream's picture: the main stream is not decoded.</remarks>
    public Task<byte[]?> CaptureFrameAsync(CancellationToken ct = default)
        => sub.CaptureFrameAsync(ct);

    /// <inheritdoc />
    public Task<byte[]?> CaptureGreyAsync(
        int width,
        int height,
        CancellationToken ct = default)
        => sub.CaptureGreyAsync(width, height, ct);

    /// <inheritdoc />
    public void SetRotation(CameraRotation rotation)
    {
        main.SetRotation(rotation);
        sub.SetRotation(rotation);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        main.ConnectionStateChanged -= OnMainStateChanged;
        sub.ConnectionStateChanged -= OnSubStateChanged;
        sub.Dispose();
        main.Dispose();
    }

    private void OnMainStateChanged(
        object? sender,
        ConnectionStateChangedEventArgs e)
    {
        // A failed sub-stream has put the pipeline in error already; the main stream's own
        // changes speak for it again only once a new pipeline is made.
        if (Volatile.Read(ref subFailed) == 0)
        {
            Raise(e.NewState);
        }
    }

    private void OnSubStateChanged(
        object? sender,
        ConnectionStateChangedEventArgs e)
    {
        if (e.NewState == ConnectionState.Connected)
        {
            subStreamPlayed();
            return;
        }

        if (e.NewState != ConnectionState.Error || Interlocked.Exchange(ref subFailed, 1) != 0)
        {
            return;
        }

        LogSubStreamFailed(source);
        subStreamFailed("The sub-stream could not be opened, or stopped.");
        Raise(ConnectionState.Error);
    }

    // Raised on an engine thread: a subscriber that fails must not unwind into it.
    private void Raise(ConnectionState newState)
    {
        ConnectionState previous;
        lock (sub)
        {
            if (state == newState)
            {
                return;
            }

            previous = state;
            state = newState;
        }

        var handlers = ConnectionStateChanged;
        if (handlers is null)
        {
            return;
        }

        var args = new ConnectionStateChangedEventArgs(previous, newState);
        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<ConnectionStateChangedEventArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex)
            {
                LogSubscriberFailed(ex, source, newState);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The sub-stream of camera '{Source}' failed: the camera is opened on its main stream alone for a while")]
    private partial void LogSubStreamFailed(string source);

    [LoggerMessage(Level = LogLevel.Error, Message = "A subscriber of camera '{Source}' going {State} failed")]
    private partial void LogSubscriberFailed(Exception ex, string source, ConnectionState state);
}
