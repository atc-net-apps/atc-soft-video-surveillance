namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Manages the HLS streams browsers watch. A network camera gets an FFmpeg
/// process of its own that turns its stream into segments. A USB camera is
/// streamed from the one pipeline the <see cref="ICameraPipelineHost"/> keeps
/// for it, because Windows gives a USB camera to one program at a time: a
/// process of its own would shut out every recording of the camera.
/// </summary>
public sealed partial class StreamingService : ILiveStreamService, IDisposable
{
    // A viewer that goes without giving its lease back would leave the
    // viewer count > 0 and the FFmpeg transcoder running. With CPU-heavy
    // libx264 transcoding per camera, even a few orphaned streams can
    // saturate a multi-camera server.
    //
    // 60s inactivity with a 30s client heartbeat gives 30s of margin — a
    // single dropped heartbeat (Wi-Fi blip, GC pause) won't kill the stream.
    // The sweep cadence lives in HlsStreamReaperService (the hosted worker that
    // drives ReapIdleSessions); worst-case lag from drop to reap is ~70s.
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromSeconds(60);

    // A viewer renews its lease this often; a lease not renewed for twice as
    // long has run out (one renewal may be lost to a blip).
    private static readonly TimeSpan LeaseRenewInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LeaseTimeout = LeaseRenewInterval * 2;

    // How long a viewer waits for the playlist and its first segment. 60s
    // leaves room for cameras with long GOPs and slow RTSP handshakes.
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StartPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly ICameraStorageService storage;
    private readonly ILogger<StreamingService> logger;
    private readonly IServiceProvider? services;
    private readonly ConcurrentDictionary<Guid, StreamSession> sessions = new();

    // leaseId -> the viewer's lease, on the session it was taken on.
    private readonly ConcurrentDictionary<Guid, StreamLeaseEntry> leases = new();
    private readonly TimeProvider time;

    private readonly string hlsOutputRoot;
    private ICameraPipelineHost? pipelineHost;
    private bool disposed;

    public StreamingService(
        ICameraStorageService storage,
        ILogger<StreamingService> logger,
        ICameraPipelineHost pipelineHost,
        TimeProvider? time = null)
        : this(storage, logger, time)
        => this.pipelineHost = pipelineHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingService"/> class
    /// that looks the pipeline host up when it first needs it, so that the
    /// host and its own dependencies are not created before the first stream.
    /// </summary>
    public StreamingService(
        ICameraStorageService storage,
        ILogger<StreamingService> logger,
        IServiceProvider services)
        : this(storage, logger, time: null)
        => this.services = services;

    private StreamingService(
        ICameraStorageService storage,
        ILogger<StreamingService> logger,
        TimeProvider? time)
    {
        this.storage = storage;
        this.logger = logger;
        this.time = time ?? TimeProvider.System;

        hlsOutputRoot = Path.Combine(Path.GetTempPath(), "linksoft-hls");
        Directory.CreateDirectory(hlsOutputRoot);
    }

    /// <summary>
    /// Gets the HLS output directory root path.
    /// </summary>
    public string HlsOutputRoot => hlsOutputRoot;

    /// <inheritdoc />
    public TimeSpan RenewInterval => LeaseRenewInterval;

    /// <inheritdoc />
    public async Task<LiveStreamStart> StartAsync(
        Guid cameraId,
        CancellationToken cancellationToken)
    {
        if (storage.GetCameraById(cameraId) is null)
        {
            return LiveStreamStart.CameraNotFound(cameraId);
        }

        StreamSession session;
        try
        {
            session = AddViewer(cameraId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            // The camera cannot be opened right now (a USB camera that is
            // unplugged, FFmpeg missing); nothing was started.
            return LiveStreamStart.Failed(ex.Message);
        }

        var started = time.GetUtcNow();
        try
        {
            while (!IsReadyToPlay(session.PlaylistPath))
            {
                // Waiting is watching: the reaper leaves the stream alone.
                session.TouchActivity();

                if (session.HasExited)
                {
                    var errors = session.GetRecentErrors();
                    RemoveViewer(cameraId, session);
                    return LiveStreamStart.Failed(string.IsNullOrWhiteSpace(errors)
                        ? "The camera could not be opened."
                        : $"The stream ended before it had started. Output: {errors}");
                }

                if (time.GetUtcNow() - started > StartTimeout)
                {
                    var errors = session.GetRecentErrors();
                    RemoveViewer(cameraId, session);
                    return LiveStreamStart.Failed(
                        $"The stream did not produce its HLS playlist within {StartTimeout.TotalSeconds:F0}s. " +
                        "The most common cause is a very long camera GOP (keyframe interval); " +
                        "reduce it in the camera's encoder settings if possible. " +
                        $"Last output: {errors}");
                }

                await Task.Delay(StartPollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The viewer left before it could watch.
            RemoveViewer(cameraId, session);
            throw;
        }

        var leaseId = Guid.NewGuid();
        leases[leaseId] = new StreamLeaseEntry(cameraId, session, time.GetUtcNow());

        // The lease is in the address: the playlist and its segments are
        // served to whoever has it, for as long as it lives.
        return LiveStreamStart.Started(leaseId, $"/streams/{leaseId:N}/{Path.GetFileName(session.PlaylistPath)}");
    }

    /// <summary>
    /// Gets the folder of the stream a lease is on, while the lease and its
    /// stream live; the stream's files are served from it.
    /// </summary>
    public bool TryGetLeaseFolder(
        Guid leaseId,
        [NotNullWhen(true)] out string? folder)
    {
        folder = null;
        if (!leases.TryGetValue(leaseId, out var lease) ||
            !sessions.TryGetValue(lease.CameraId, out var current) ||
            !ReferenceEquals(current, lease.Session))
        {
            return false;
        }

        folder = Path.GetDirectoryName(lease.Session.PlaylistPath);
        return folder is not null;
    }

    /// <inheritdoc />
    public bool Renew(
        Guid cameraId,
        Guid leaseId)
    {
        if (!leases.TryGetValue(leaseId, out var lease) || lease.CameraId != cameraId)
        {
            return false;
        }

        lease.Renew(time.GetUtcNow());
        lease.Session.TouchActivity();
        return true;
    }

    /// <inheritdoc />
    public void Release(
        Guid cameraId,
        Guid leaseId)
    {
        if (!leases.TryGetValue(leaseId, out var lease) ||
            lease.CameraId != cameraId ||
            !leases.TryRemove(leaseId, out _))
        {
            return;
        }

        RemoveViewer(cameraId, lease.Session);
    }

    private static bool IsReadyToPlay(string playlistPath)
        => File.Exists(playlistPath) &&
           Directory.EnumerateFiles(Path.GetDirectoryName(playlistPath)!, "*.ts").Any();

    private StreamSession AddViewer(Guid cameraId)
    {
        var session = sessions.GetOrAdd(cameraId, id => CreateSession(id));
        session.IncrementViewers();
        session.TouchActivity();
        return session;
    }

    // A viewer of this session goes; one of a session that has since ended
    // (reaped, or replaced by a later one) changes nothing.
    private void RemoveViewer(
        Guid cameraId,
        StreamSession session)
    {
        if (!sessions.TryGetValue(cameraId, out var current) || !ReferenceEquals(current, session))
        {
            return;
        }

        if (session.DecrementViewers() <= 0 &&
            sessions.TryRemove(new KeyValuePair<Guid, StreamSession>(cameraId, session)))
        {
            session.Dispose();
            LogHlsStreamStopped(cameraId);
        }
    }

    /// <summary>
    /// Adds a viewer of a camera's stream without a lease, starting the stream
    /// when it is the first. Returns the playlist path.
    /// </summary>
    public string StartStream(Guid cameraId)
        => AddViewer(cameraId).PlaylistPath;

    /// <summary>
    /// Removes a viewer added by <see cref="StartStream"/>; the stream stops
    /// with its last viewer.
    /// </summary>
    public void StopStream(Guid cameraId)
    {
        if (sessions.TryGetValue(cameraId, out var session))
        {
            RemoveViewer(cameraId, session);
        }
    }

    /// <summary>
    /// Gets whether a camera is currently streaming.
    /// </summary>
    public bool IsStreaming(Guid cameraId)
        => sessions.ContainsKey(cameraId);

    /// <summary>
    /// Gets the current viewer count for a camera stream.
    /// </summary>
    public int GetViewerCount(Guid cameraId)
        => sessions.TryGetValue(cameraId, out var session)
            ? session.ViewerCount
            : 0;

    /// <summary>
    /// Gets the HLS playlist path for a camera, or null if not streaming.
    /// </summary>
    public string? GetPlaylistPath(Guid cameraId)
        => sessions.TryGetValue(cameraId, out var session)
            ? session.PlaylistPath
            : null;

    /// <summary>
    /// Gets whether a camera's stream has ended by itself: its FFmpeg
    /// process exited, or the camera's pipeline failed or was closed.
    /// </summary>
    public bool HasProcessExited(Guid cameraId)
        => sessions.TryGetValue(cameraId, out var session) && session.HasExited;

    /// <summary>
    /// Gets recent FFmpeg stderr output for a camera session.
    /// </summary>
    public string GetProcessError(Guid cameraId)
        => sessions.TryGetValue(cameraId, out var session)
            ? session.GetRecentErrors()
            : string.Empty;

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        foreach (var session in sessions.Values)
        {
            session.Dispose();
        }

        sessions.Clear();
    }

    /// <summary>
    /// Disposes any stream session whose last client activity is older than the
    /// inactivity timeout. Driven periodically by <see cref="HlsStreamReaperService"/>
    /// so an orphaned FFmpeg transcoder (client dropped its socket without
    /// calling StopStream) is torn down instead of running forever.
    /// </summary>
    public void ReapIdleSessions()
    {
        if (disposed)
        {
            return;
        }

        // A lease that was not renewed has run out: its viewer went without a word.
        var now = time.GetUtcNow();
        foreach (var (leaseId, lease) in leases)
        {
            if (now - lease.RenewedUtc > LeaseTimeout && leases.TryRemove(leaseId, out _))
            {
                RemoveViewer(lease.CameraId, lease.Session);
            }
        }

        var cutoff = now.UtcDateTime - InactivityTimeout;
        foreach (var (cameraId, session) in sessions)
        {
            if (session.LastActivityUtc < cutoff)
            {
                ReapSession(cameraId);
            }
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "removed is unconditionally disposed inside the try block.")]
    private void ReapSession(Guid cameraId)
    {
        if (!sessions.TryRemove(cameraId, out var removed))
        {
            return;
        }

        try
        {
            removed.Dispose();
            LogHlsStreamReaped(cameraId);
        }
        catch (Exception ex)
        {
            LogHlsStreamReapFailed(ex, cameraId);
        }
    }

    private StreamSession CreateSession(Guid cameraId)
    {
        var camera = storage.GetCameraById(cameraId)
            ?? throw new InvalidOperationException($"Camera {cameraId} not found.");

        var outputDir = Path.Combine(hlsOutputRoot, cameraId.ToString("N"));

        // Clean stale files from previous sessions (e.g. after Aspire restart)
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }

        Directory.CreateDirectory(outputDir);

        var playlistPath = Path.Combine(outputDir, "stream.m3u8");

        if (camera.Connection.Source == CameraSource.Usb)
        {
            pipelineHost ??= services?.GetService<ICameraPipelineHost>();

            var pipeline = pipelineHost?.Acquire(camera, CameraPipelineUse.Viewing)
                ?? throw new InvalidOperationException(
                    "This USB camera cannot be opened right now: it is unplugged, or the server is still connecting to it. Try again in a moment.");

            var host = pipelineHost!;
            LogHlsStreamStartedFromPipeline(cameraId, playlistPath);
            return new StreamSession(
                time,
                pipeline,
                () => host.Release(cameraId, CameraPipelineUse.Viewing),
                playlistPath,
                outputDir);
        }

        var args = HlsStreamCommand.BuildArguments(camera, playlistPath);

        var psi = new ProcessStartInfo
        {
            FileName = HlsStreamCommand.FindTool(VideoEngineBootstrap.FFmpegFolder ?? VideoEngineBootstrap.FindFFmpegPath()),
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
        };

        // The command line and FFmpeg's own output both hold the camera's
        // password; neither goes into the log, or to a client, as it is.
        LogStartingFfmpeg(cameraId, HlsStreamCommand.Redact(args, camera));

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start FFmpeg HLS process.");

        var session = new StreamSession(time, process, playlistPath, outputDir);

        // Log stderr asynchronously for diagnostics
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var line = HlsStreamCommand.Redact(e.Data, camera);
                session.AddErrorLine(line);
                LogFfmpegOutput(cameraId, line);
            }
        };
        process.BeginErrorReadLine();

        LogHlsStreamStarted(cameraId, playlistPath);

        return session;
    }

    private sealed class StreamSession : IDisposable
    {
        // Only the last lines are ever asked for; FFmpeg writes several a
        // second for as long as the stream runs.
        private const int MaxErrorLines = 50;

        // How often a pipeline that was just opened is asked whether it
        // delivers pictures yet.
        private static readonly TimeSpan PipelinePollInterval = TimeSpan.FromMilliseconds(200);

        private readonly TimeProvider time;
        private readonly Process? process;
        private readonly IMediaPipeline? pipeline;
        private readonly Action? releasePipeline;
        private readonly string outputDir;
        private readonly Queue<string> errorLines = new();
        private readonly Lock pipelineStreamLock = new();
        private bool pipelineStreamStarted;
        private bool pipelineFailed;
        private bool closed;
        private int viewerCount;
        private long lastActivityUtcTicks;

        public StreamSession(
            TimeProvider time,
            Process process,
            string playlistPath,
            string outputDir)
        {
            this.time = time;
            this.process = process;
            this.outputDir = outputDir;
            PlaylistPath = playlistPath;
            lastActivityUtcTicks = time.GetUtcNow().UtcTicks;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamSession"/> class
        /// for a stream written by the camera's shared pipeline. The pipeline
        /// is its host's: it is told to stop the stream and given back,
        /// never closed. A pipeline that was opened just now cannot stream
        /// yet; the stream starts when it delivers pictures.
        /// </summary>
        public StreamSession(
            TimeProvider time,
            IMediaPipeline pipeline,
            Action releasePipeline,
            string playlistPath,
            string outputDir)
        {
            this.time = time;
            this.pipeline = pipeline;
            this.releasePipeline = releasePipeline;
            this.outputDir = outputDir;
            PlaylistPath = playlistPath;
            lastActivityUtcTicks = time.GetUtcNow().UtcTicks;

            pipeline.ConnectionStateChanged += OnPipelineStateChanged;
            if (!TryStartPipelineStream())
            {
                _ = StartPipelineStreamWhenPlayingAsync();
            }
        }

        public string PlaylistPath { get; }

        public int ViewerCount => Volatile.Read(ref viewerCount);

        public bool HasExited
        {
            get
            {
                if (process is not null)
                {
                    return process.HasExited;
                }

                lock (pipelineStreamLock)
                {
                    // Not started yet is not ended: the camera is coming up.
                    return pipeline is null ||
                           pipelineFailed ||
                           (pipelineStreamStarted && !IsPipelineStreaming(pipeline));
                }
            }
        }

        public DateTime LastActivityUtc
            => new(Interlocked.Read(ref lastActivityUtcTicks), DateTimeKind.Utc);

        public void TouchActivity()
            => Interlocked.Exchange(ref lastActivityUtcTicks, time.GetUtcNow().UtcTicks);

        public void AddErrorLine(string line)
        {
            lock (errorLines)
            {
                errorLines.Enqueue(line);
                while (errorLines.Count > MaxErrorLines)
                {
                    errorLines.Dequeue();
                }
            }
        }

        public string GetRecentErrors()
        {
            lock (errorLines)
            {
                return string.Join(Environment.NewLine, errorLines.TakeLast(10));
            }
        }

        public void IncrementViewers()
            => Interlocked.Increment(ref viewerCount);

        public int DecrementViewers()
            => Interlocked.Decrement(ref viewerCount);

        public void Dispose()
        {
            StopPipelineStream();
            StopProcess();

            try
            {
                if (Directory.Exists(outputDir))
                {
                    Directory.Delete(outputDir, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best effort cleanup
            }
        }

        // A pipeline its owner has closed meanwhile no longer streams.
        private static bool IsPipelineStreaming(IMediaPipeline pipeline)
        {
            try
            {
                return pipeline.IsLiveStreamActive;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        // Runs on the pipeline's own thread: it only takes a note.
        private void OnPipelineStateChanged(
            object? sender,
            ConnectionStateChangedEventArgs e)
        {
            if (e.NewState == ConnectionState.Error)
            {
                lock (pipelineStreamLock)
                {
                    pipelineFailed = true;
                }
            }
        }

        /// <summary>
        /// Starts the stream when the pipeline plays.
        /// </summary>
        /// <returns><see langword="false"/> when it does not play yet and has to be asked again.</returns>
        private bool TryStartPipelineStream()
        {
            lock (pipelineStreamLock)
            {
                if (closed || pipelineFailed || pipelineStreamStarted)
                {
                    return true;
                }

                try
                {
                    if (pipeline!.FramesDecoded <= 0)
                    {
                        return false;
                    }

                    pipeline.StartLiveStream(PlaylistPath);
                    pipelineStreamStarted = true;
                    return true;
                }
                catch (ObjectDisposedException)
                {
                    // Its host closed it meanwhile.
                    pipelineFailed = true;
                    return true;
                }
                catch (InvalidOperationException ex)
                {
                    // Not playing yet. Should it never get there, this is
                    // what the viewer is told when the wait is given up.
                    AddErrorLine(ex.Message);
                    return false;
                }
            }
        }

        private async Task StartPipelineStreamWhenPlayingAsync()
        {
            while (!TryStartPipelineStream())
            {
                await Task.Delay(PipelinePollInterval).ConfigureAwait(false);
            }
        }

        private void StopPipelineStream()
        {
            if (pipeline is null)
            {
                return;
            }

            lock (pipelineStreamLock)
            {
                closed = true;
            }

            pipeline.ConnectionStateChanged -= OnPipelineStateChanged;

            try
            {
                pipeline.StopLiveStream();
            }
            catch (ObjectDisposedException)
            {
                // Its host closed it; the stream ended with it.
            }

            releasePipeline?.Invoke();
        }

        private void StopProcess()
        {
            if (process is null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.StandardInput.Write('q');
                    process.StandardInput.Flush();

                    if (!process.WaitForExit(5000))
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private sealed class StreamLeaseEntry(
        Guid cameraId,
        StreamSession session,
        DateTimeOffset renewedUtc)
    {
        private long renewedUtcTicks = renewedUtc.UtcTicks;

        public Guid CameraId { get; } = cameraId;

        public StreamSession Session { get; } = session;

        public DateTimeOffset RenewedUtc
            => new(Interlocked.Read(ref renewedUtcTicks), TimeSpan.Zero);

        public void Renew(DateTimeOffset now)
            => Interlocked.Exchange(ref renewedUtcTicks, now.UtcTicks);
    }
}