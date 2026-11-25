namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

using CoreConnectionStateChangedEventArgs = AtcSoft.Video.Surveillance.Events.ConnectionStateChangedEventArgs;
using CoreSourceLocator = AtcSoft.Video.Surveillance.Helpers.SourceLocator;
using CoreStreamSettings = AtcSoft.Video.Surveillance.Models.Settings.StreamSettings;

/// <summary>
/// The pipeline of a CameraWall tile whose camera has a sub-stream: the tile plays the
/// sub-stream, and this pipeline adds a connection to the main stream, held without decoding it,
/// for everything that records. Motion detection, the activity index and snapshots of the
/// pipeline see the sub-stream's pictures; the recordings, their pre-roll and their files are of
/// the main stream.
/// </summary>
/// <remarks>
/// The tile owns its own player (the sub-stream) and the pipeline wrapped around it; this one owns
/// the main stream's player and opens it again, a few seconds later, when it drops. The picture
/// size it reports (<see cref="DisplaySize"/>) is the main stream's, so a sub-stream squeezed to
/// another shape is analysed as the main stream shows it.
/// </remarks>
public sealed class SubStreamTilePipeline : IMediaPipeline
{
    private static readonly TimeSpan DefaultReopenDelay = TimeSpan.FromSeconds(5);

    private readonly IMediaPipeline sub;
    private readonly IVideoPlayer mainPlayer;
    private readonly IMediaPipeline main;
    private readonly Uri mainUri;
    private readonly StreamOptions mainOptions;
    private readonly CancellationTokenSource closing = new();
    private readonly TimeSpan reopenDelay;
    private int reopening;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubStreamTilePipeline"/> class and opens the
    /// main stream.
    /// </summary>
    /// <param name="sub">The pipeline of the tile's own player, which plays the sub-stream.</param>
    /// <param name="mainPlayer">A new player for the main stream; this pipeline owns it.</param>
    /// <param name="pipelineFactory">Wraps a player in a pipeline, as for the tile's own.</param>
    /// <param name="mainUri">The main stream's address.</param>
    /// <param name="mainOptions">The options to open the main stream with; it is not decoded.</param>
    /// <param name="reopenDelay">How long after it drops the main stream is opened again (5 s).</param>
    public SubStreamTilePipeline(
        IMediaPipeline sub,
        IVideoPlayer mainPlayer,
        Func<IVideoPlayer, IMediaPipeline> pipelineFactory,
        Uri mainUri,
        StreamOptions mainOptions,
        TimeSpan? reopenDelay = null)
    {
        ArgumentNullException.ThrowIfNull(pipelineFactory);
        ArgumentNullException.ThrowIfNull(mainOptions);

        this.sub = sub ?? throw new ArgumentNullException(nameof(sub));
        this.mainPlayer = mainPlayer ?? throw new ArgumentNullException(nameof(mainPlayer));
        this.mainUri = mainUri ?? throw new ArgumentNullException(nameof(mainUri));
        main = pipelineFactory(mainPlayer);
        mainOptions.DecodeVideo = false;
        this.mainOptions = mainOptions;
        this.reopenDelay = reopenDelay ?? DefaultReopenDelay;

        sub.ConnectionStateChanged += OnSubStateChanged;
        mainPlayer.StateChanged += OnMainStateChanged;
        mainPlayer.Open(mainUri, mainOptions);
    }

    /// <inheritdoc />
    /// <remarks>The tile's stream, the sub-stream: what the tile shows is what it says.</remarks>
    public event EventHandler<CoreConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public bool IsRecordingActive => main.IsRecordingActive;

    /// <inheritdoc />
    public string? RecordingError => main.RecordingError;

    /// <inheritdoc />
    public double CurrentFps => sub.CurrentFps;

    /// <inheritdoc />
    public long FramesDecoded => sub.FramesDecoded;

    /// <inheritdoc />
    public DateTime LastPacketUtc => sub.LastPacketUtc;

    /// <inheritdoc />
    public (int Width, int Height) DisplaySize
        => main.DisplaySize is { Width: > 0, Height: > 0 } size ? size : sub.DisplaySize;

    /// <summary>
    /// Gets a value indicating whether the main stream plays, so a recording can start.
    /// </summary>
    public bool IsMainStreamPlaying => mainPlayer.State == PlayerState.Playing;

    /// <inheritdoc />
    /// <remarks>The tile opens its own player; the main stream is opened by this pipeline.</remarks>
    public void Open(
        CoreSourceLocator locator,
        CoreStreamSettings settings)
        => throw new NotSupportedException("The tile opens its own player; the main stream is opened by the pipeline.");

    /// <inheritdoc />
    public void Close()
        => mainPlayer.Close();

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
        sub.SetRotation(rotation);
        main.SetRotation(rotation);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        closing.Cancel();
        sub.ConnectionStateChanged -= OnSubStateChanged;
        mainPlayer.StateChanged -= OnMainStateChanged;
        main.Dispose();
        mainPlayer.Dispose();
        sub.Dispose();
        closing.Dispose();
    }

    private void OnSubStateChanged(
        object? sender,
        CoreConnectionStateChangedEventArgs e)
        => ConnectionStateChanged?.Invoke(this, e);

    // The main stream that drops is opened again, once at a time, while the pipeline lives: the
    // tile reconnects its own player, not this one.
    private void OnMainStateChanged(
        object? sender,
        PlayerStateChangedEventArgs e)
    {
        if (e.NewState != PlayerState.Error || disposed || Interlocked.Exchange(ref reopening, 1) != 0)
        {
            return;
        }

        var token = closing.Token;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(reopenDelay, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                    {
                        mainPlayer.Open(mainUri, mainOptions);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Closed meanwhile.
                }
                catch (ObjectDisposedException)
                {
                    // Closed meanwhile.
                }
                finally
                {
                    Volatile.Write(ref reopening, 0);
                }
            },
            CancellationToken.None);
    }
}