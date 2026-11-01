namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// Server-side <see cref="IMediaPipeline"/> implementation using the in-process
/// <see cref="IVideoPlayer"/> from <c>Atc.Video.Engine</c>.
/// Replaces the previous FFmpeg subprocess approach for recording and snapshots.
/// </summary>
public sealed partial class VideoEngineMediaPipeline : IMediaPipeline
{
    private readonly IVideoPlayer player;
    private readonly string source;
    private readonly ILogger<VideoEngineMediaPipeline> logger;
    private ConnectionState currentState = ConnectionState.Disconnected;
    private bool disposed;
    private CameraRotation rotation;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoEngineMediaPipeline"/> class.
    /// </summary>
    /// <param name="player">The video player to wrap.</param>
    /// <param name="source">A human-readable label (typically the camera display name) for log correlation.</param>
    /// <param name="logger">Where a subscriber that fails is written.</param>
    public VideoEngineMediaPipeline(
        IVideoPlayer player,
        string source = "",
        ILogger<VideoEngineMediaPipeline>? logger = null)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.source = source ?? string.Empty;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoEngineMediaPipeline>.Instance;
        this.player.StateChanged += OnPlayerStateChanged;
    }

    /// <inheritdoc />
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public bool IsRecordingActive => player.IsRecording;

    public string? RecordingError => player.RecordingError;

    /// <summary>
    /// Gets or sets a value indicating whether the camera is recorded as
    /// H.264 whatever it sends. Read when the pipeline is opened.
    /// </summary>
    public bool TranscodeRecordingToH264 { get; set; }

    /// <inheritdoc />
    public bool IsLiveStreamActive => player.IsStreaming;

    /// <inheritdoc />
    public double CurrentFps => player.CurrentFps;

    /// <inheritdoc />
    public long FramesDecoded => player.FramesDecoded;

    /// <inheritdoc />
    public DateTime LastPacketUtc => player.LastPacketUtc;

    /// <inheritdoc />
    public void Open(
        SourceLocator locator,
        StreamSettings settings)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(settings);

        var options = new StreamOptions
        {
            Source = source,
            UseLowLatencyMode = settings.UseLowLatencyMode,
            MaxLatencyMs = settings.MaxLatencyMs,
            RtspTransport = settings.RtspTransport,
            BufferDurationMs = settings.BufferDurationMs,
            InputFormat = MapInputFormat(locator.InputFormat),
            RawDeviceSpec = locator.RawDeviceSpec,
            RecordAudio = locator.IncludesAudio,
            RecordingPreRoll = AtcSoft.Video.Surveillance.Helpers.RecordingPreRoll.Motion,
            TranscodeRecordingToH264 = TranscodeRecordingToH264,
            VideoSize = locator.VideoSize,
            FrameRate = locator.FrameRate,
            PixelFormat = locator.PixelFormat,
        };

        player.Open(locator.Uri, options);
    }

    private static InputFormatKind MapInputFormat(string? name)
        => name switch
        {
            "dshow" => InputFormatKind.Dshow,
            "v4l2" => InputFormatKind.V4l2,
            "avfoundation" => InputFormatKind.AVFoundation,
            _ => InputFormatKind.Auto,
        };

    /// <inheritdoc />
    public void Close()
    {
        player.Close();
    }

    /// <inheritdoc />
    public DateTime? FindRecordingStart(TimeSpan preRoll)
        => player.FindRecordingStart(preRoll);

    /// <inheritdoc />
    public void StartRecording(string outputFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        player.StartRecording(outputFilePath);
    }

    /// <inheritdoc />
    public void StartRecording(
        string outputFilePath,
        DateTime startFromUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        player.StartRecording(outputFilePath, startFromUtc);
    }

    /// <inheritdoc />
    public void StopRecording()
    {
        player.StopRecording();
    }

    /// <inheritdoc />
    public void SwitchRecording(string newOutputFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newOutputFilePath);
        player.SwitchRecording(newOutputFilePath);
    }

    /// <inheritdoc />
    public void StartLiveStream(string playlistPath)
        => player.StartStreaming(playlistPath);

    /// <inheritdoc />
    public void StopLiveStream()
        => player.StopStreaming();

    /// <inheritdoc />
    public Task<byte[]?> CaptureFrameAsync(CancellationToken ct = default)
        => player.CaptureFrameAsync(ct);

    /// <inheritdoc />
    public async Task<byte[]?> CaptureGreyAsync(
        int width,
        int height,
        CancellationToken ct = default)
        => (await player.CaptureGreyAsync(width, height, ct).ConfigureAwait(false))?.Pixels;

    /// <inheritdoc />
    public (int Width, int Height) DisplaySize
        => player.StreamInfo is { Width: > 0, Height: > 0 } stream
            ? MotionBoxMapper.GetDisplaySize(stream.Width, stream.Height, rotation)
            : (0, 0);

    /// <inheritdoc />
    public void SetRotation(CameraRotation rotation)
    {
        this.rotation = rotation;
        player.SetRotation(MapRotation(rotation));
    }

    private static VideoRotation MapRotation(CameraRotation rotation)
        => rotation switch
        {
            CameraRotation.Rotate90 => VideoRotation.Rotate90,
            CameraRotation.Rotate180 => VideoRotation.Rotate180,
            CameraRotation.Rotate270 => VideoRotation.Rotate270,
            _ => VideoRotation.None,
        };

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        player.StateChanged -= OnPlayerStateChanged;

        if (player.IsRecording)
        {
            try
            {
                player.StopRecording();
            }
            catch
            {
                // Best effort during disposal
            }
        }

        player.Dispose();
        disposed = true;
    }

    private void OnPlayerStateChanged(
        object? sender,
        PlayerStateChangedEventArgs e)
    {
        var newState = MapPlayerState(e.NewState);
        if (newState == currentState)
        {
            return;
        }

        var previousState = currentState;
        currentState = newState;

        var handlers = ConnectionStateChanged;
        if (handlers is null)
        {
            return;
        }

        // Raised on the engine's own thread: a subscriber that fails must not
        // unwind into it, nor keep the others from hearing the change.
        var args = new ConnectionStateChangedEventArgs(previousState, newState);
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

    [LoggerMessage(Level = LogLevel.Error, Message = "A subscriber of camera '{Source}' going {State} failed")]
    private partial void LogSubscriberFailed(Exception ex, string source, ConnectionState state);

    private static ConnectionState MapPlayerState(PlayerState state)
        => state switch
        {
            PlayerState.Playing => ConnectionState.Connected,
            PlayerState.Opening => ConnectionState.Connecting,
            PlayerState.Stopped => ConnectionState.Disconnected,
            PlayerState.Error => ConnectionState.Error,
            _ => ConnectionState.Disconnected,
        };
}