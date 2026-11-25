namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

using CoreConnectionState = AtcSoft.Video.Surveillance.Enums.ConnectionState;
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
/// <para>
/// The main stream is opened when the sub-stream plays and closed when the tile's stream stops,
/// unless a recording runs or waits: a tile that is not connected holds no connection. A
/// recording asked for before the main stream plays (the tile's stream usually plays first) waits
/// for it and starts with it. A main stream that drops is opened again after 5, 10, 20, 40 and then
/// every 60 seconds, while the tile watches or a recording needs it; the recording service starts a
/// new file once it plays (<see cref="LastPacketUtc"/> is the older of the two streams).
/// </para>
/// <para>
/// The tile owns its own player (the sub-stream) and the pipeline wrapped around it; this one owns
/// the main stream's player. The picture size it reports (<see cref="DisplaySize"/>) is the main
/// stream's, so a sub-stream squeezed to another shape is analysed as the main stream shows it.
/// </para>
/// </remarks>
public sealed class SubStreamTilePipeline : IMediaPipeline
{
    private static readonly TimeSpan DefaultReopenDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxReopenDelay = TimeSpan.FromSeconds(60);

    private readonly IMediaPipeline sub;
    private readonly IVideoPlayer mainPlayer;
    private readonly IMediaPipeline main;
    private readonly Uri mainUri;
    private readonly StreamOptions mainOptions;
    private readonly TimeSpan reopenDelay;
    private readonly CancellationTokenSource closing = new();
    private readonly Lock gate = new();
    private bool mainOpen;
    private bool subPlaying;
    private int failures;
    private int reopening;
    private string? pendingRecording;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubStreamTilePipeline"/> class. The main stream
    /// is opened once the sub-stream plays (at once when it plays already).
    /// </summary>
    /// <param name="sub">The pipeline of the tile's own player, which plays the sub-stream.</param>
    /// <param name="mainPlayer">A new player for the main stream; this pipeline owns it.</param>
    /// <param name="pipelineFactory">Wraps a player in a pipeline, as for the tile's own.</param>
    /// <param name="mainUri">The main stream's address.</param>
    /// <param name="mainOptions">The options to open the main stream with; it is not decoded.</param>
    /// <param name="reopenDelay">The first wait before a main stream that dropped is opened again (5 s).</param>
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

        // A tile that got a new pipeline for a player that plays already (its settings changed).
        if (sub.FramesDecoded > 0)
        {
            lock (gate)
            {
                subPlaying = true;
                OpenMain();
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>The tile's stream, the sub-stream: what the tile shows is what it says.</remarks>
    public event EventHandler<CoreConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    /// <remarks>Also while a recording waits for the main stream to play.</remarks>
    public bool IsRecordingActive
    {
        get
        {
            lock (gate)
            {
                return pendingRecording is not null || main.IsRecordingActive;
            }
        }
    }

    /// <inheritdoc />
    public string? RecordingError => main.RecordingError;

    /// <inheritdoc />
    public double CurrentFps => sub.CurrentFps;

    /// <inheritdoc />
    public long FramesDecoded => sub.FramesDecoded;

    /// <inheritdoc />
    /// <remarks>The older of the two streams' last packets: a recording is written only while both arrive.</remarks>
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

    /// <summary>
    /// Gets a value indicating whether the main stream plays.
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
    {
        lock (gate)
        {
            pendingRecording = null;
            CloseMain();
        }
    }

    /// <inheritdoc />
    public DateTime? FindRecordingStart(TimeSpan preRoll)
        => IsMainStreamPlaying ? main.FindRecordingStart(preRoll) : null;

    /// <inheritdoc />
    public void StartRecording(string outputFilePath)
        => StartOrWait(outputFilePath, startFromUtc: null);

    /// <inheritdoc />
    public void StartRecording(
        string outputFilePath,
        DateTime startFromUtc)
        => StartOrWait(outputFilePath, startFromUtc);

    /// <inheritdoc />
    public void StopRecording()
    {
        lock (gate)
        {
            pendingRecording = null;
            if (main.IsRecordingActive)
            {
                main.StopRecording();
            }

            // Nothing needs the main stream any more once the tile's stream has stopped too.
            if (!subPlaying)
            {
                CloseMain();
            }
        }
    }

    /// <inheritdoc />
    public void SwitchRecording(string newOutputFilePath)
        => SwitchOrWait(newOutputFilePath, startFromUtc: null);

    /// <inheritdoc />
    public void SwitchRecording(
        string newOutputFilePath,
        DateTime startFromUtc)
        => SwitchOrWait(newOutputFilePath, startFromUtc);

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
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            pendingRecording = null;
        }

        closing.Cancel();
        sub.ConnectionStateChanged -= OnSubStateChanged;
        mainPlayer.StateChanged -= OnMainStateChanged;
        main.Dispose();
        mainPlayer.Dispose();
        sub.Dispose();
        closing.Dispose();
    }

    // A recording asked for before the main stream plays waits for it (without the pre-roll: the
    // main stream has nothing kept yet), and starts with it.
    private void StartOrWait(
        string outputFilePath,
        DateTime? startFromUtc)
    {
        lock (gate)
        {
            if (IsMainStreamPlaying)
            {
                if (startFromUtc is { } from)
                {
                    main.StartRecording(outputFilePath, from);
                }
                else
                {
                    main.StartRecording(outputFilePath);
                }

                return;
            }

            pendingRecording = outputFilePath;
            OpenMain();
        }
    }

    private void SwitchOrWait(
        string newOutputFilePath,
        DateTime? startFromUtc)
    {
        lock (gate)
        {
            if (!IsMainStreamPlaying)
            {
                pendingRecording = newOutputFilePath;
                OpenMain();
                return;
            }

            if (startFromUtc is { } from)
            {
                main.SwitchRecording(newOutputFilePath, from);
            }
            else
            {
                main.SwitchRecording(newOutputFilePath);
            }
        }
    }

    // Under the gate.
    private void OpenMain()
    {
        if (mainOpen || disposed)
        {
            return;
        }

        mainOpen = true;
        mainPlayer.Open(mainUri, mainOptions);
    }

    // Under the gate.
    private void CloseMain()
    {
        if (!mainOpen)
        {
            return;
        }

        mainOpen = false;
        failures = 0;
        mainPlayer.Close();
    }

    private void OnSubStateChanged(
        object? sender,
        CoreConnectionStateChangedEventArgs e)
    {
        lock (gate)
        {
            switch (e.NewState)
            {
                case CoreConnectionState.Connected:
                    subPlaying = true;
                    OpenMain();
                    break;
                case CoreConnectionState.Disconnected:
                    // The tile's stream was stopped: the main stream goes too, unless a recording needs it.
                    subPlaying = false;
                    if (pendingRecording is null && !main.IsRecordingActive)
                    {
                        CloseMain();
                    }

                    break;
                case CoreConnectionState.Error:
                    subPlaying = false;
                    break;
            }
        }

        ConnectionStateChanged?.Invoke(this, e);
    }

    private void OnMainStateChanged(
        object? sender,
        PlayerStateChangedEventArgs e)
    {
        if (e.NewState == PlayerState.Playing)
        {
            lock (gate)
            {
                failures = 0;
                if (pendingRecording is { } path)
                {
                    pendingRecording = null;
                    try
                    {
                        main.StartRecording(path);
                    }
                    catch (InvalidOperationException)
                    {
                        // Dropped again at once: the recording service starts a new file later.
                    }
                }
            }

            return;
        }

        if (e.NewState == PlayerState.Error)
        {
            ScheduleReopen();
        }
    }

    // The main stream that dropped is opened again, once at a time, while the tile watches or a
    // recording needs it; longer waits each time, up to a minute.
    private void ScheduleReopen()
    {
        TimeSpan delay;
        lock (gate)
        {
            if (disposed || !mainOpen || !(subPlaying || pendingRecording is not null || main.IsRecordingActive) ||
                Interlocked.Exchange(ref reopening, 1) != 0)
            {
                return;
            }

            failures++;
            var seconds = reopenDelay.TotalSeconds * Math.Pow(2, Math.Min(failures - 1, 10));
            delay = TimeSpan.FromSeconds(Math.Min(seconds, Math.Max(MaxReopenDelay.TotalSeconds, reopenDelay.TotalSeconds)));
        }

        var token = closing.Token;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                    lock (gate)
                    {
                        if (!token.IsCancellationRequested && mainOpen)
                        {
                            mainPlayer.Open(mainUri, mainOptions);
                        }
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