namespace AtcSoft.CameraWall.Wpf.Services;

using CoreConnectionState = AtcSoft.VideoSurveillance.Enums.ConnectionState;
using CoreConnectionStateChangedEventArgs = AtcSoft.VideoSurveillance.Events.ConnectionStateChangedEventArgs;
using CoreStreamSettings = AtcSoft.VideoSurveillance.Models.Settings.StreamSettings;

/// <summary>
/// VideoEngine-based implementation of <see cref="IMediaPipeline"/>.
/// Wraps an <see cref="IVideoPlayer"/> to provide recording
/// and frame capture for platform-agnostic services.
/// </summary>
public sealed class VideoEngineMediaPipeline : IMediaPipeline
{
    private readonly IVideoPlayer player;
    private readonly string source;
    private CoreConnectionState currentState = CoreConnectionState.Disconnected;
    private bool disposed;
    private CameraRotation rotation;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoEngineMediaPipeline"/> class.
    /// </summary>
    /// <param name="player">The video player to wrap.</param>
    /// <param name="source">A human-readable label (typically the camera display name) for log correlation.</param>
    public VideoEngineMediaPipeline(
        IVideoPlayer player,
        string source = "")
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.source = source ?? string.Empty;
        this.player.StateChanged += OnPlayerStateChanged;
    }

    /// <inheritdoc />
    public event EventHandler<CoreConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public bool IsRecordingActive => player.IsRecording;

    public string? RecordingError => player.RecordingError;

    /// <inheritdoc />
    public double CurrentFps => player.CurrentFps;

    /// <inheritdoc />
    public long FramesDecoded => player.FramesDecoded;

    /// <inheritdoc />
    public DateTime LastPacketUtc => player.LastPacketUtc;

    /// <inheritdoc />
    public void Open(
        AtcSoft.VideoSurveillance.Helpers.SourceLocator locator,
        CoreStreamSettings settings)
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
    public void StartRecording(string outputFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        player.StartRecording(outputFilePath);
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

        // Note: Player lifecycle is managed by CameraTile, not by this wrapper
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
        ConnectionStateChanged?.Invoke(
            this,
            new CoreConnectionStateChangedEventArgs(previousState, newState));
    }

    private static CoreConnectionState MapPlayerState(PlayerState state)
        => state switch
        {
            PlayerState.Playing => CoreConnectionState.Connected,
            PlayerState.Opening => CoreConnectionState.Connecting,
            PlayerState.Stopped => CoreConnectionState.Disconnected,
            PlayerState.Error => CoreConnectionState.Error,
            _ => CoreConnectionState.Disconnected,
        };
}