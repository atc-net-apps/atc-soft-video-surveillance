namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// Notes when each camera could be recorded and when not, so the timeline can say why it has a
/// gap: a camera going online or offline, its recording starting or stopping, a recording stopped
/// or refused for a full disk, and the server's own starts and stops. One JSON-lines file per
/// camera in <see cref="ServerDataPaths.AvailabilityPath"/>, one for the server.
/// </summary>
/// <remarks>
/// The changes are noted as their events fire, written by a queue of their own so the threads
/// that raise them (a camera's, a recording's) never wait for the disk. <see cref="DoWorkAsync"/>
/// notes the time in <c>server.alive</c>: a server that stopped without saying so (a crash, a
/// power cut) is noted as stopped then, at its next start. Once a day it drops what is older than
/// the recordings kept, so the files do not grow for ever; a file is read again only when it changed.
/// </remarks>
public sealed partial class AvailabilityHistoryService : BackgroundServiceBase<AvailabilityHistoryService>, IAvailabilityHistory
{
    private const string ServerFileName = "server.jsonl";
    private const string AliveFileName = "server.alive";

    // History is kept a little longer than the recordings, and at least a month.
    private const int MinimumKeptDays = 31;
    private const int KeptDaysWithoutRetention = 400;

    private static readonly TimeSpan TrimInterval = TimeSpan.FromDays(1);

    private readonly ServerRecordingService recordingService;
    private readonly string folder;
    private readonly TimeProvider timeProvider;
    private readonly IApplicationSettingsService? settingsService;
    private readonly ConcurrentDictionary<(Guid Camera, bool Connection), AvailabilityChange> last = new();
    private readonly ConcurrentDictionary<string, CachedHistory> cache = new(StringComparer.OrdinalIgnoreCase);

    // Changes waiting to be written; the oldest go first when a disk hangs.
    private readonly Channel<(string Path, AvailabilityEntry Entry)> pending =
        Channel.CreateBounded<(string Path, AvailabilityEntry Entry)>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    private Task? writer;
    private DateTimeOffset lastTrim = DateTimeOffset.MinValue;

    public AvailabilityHistoryService(
        ILogger<AvailabilityHistoryService> logger,
        AvailabilityHistoryServiceOptions options,
        ServerRecordingService recordingService,
        ServerDataPaths dataPaths,
        TimeProvider timeProvider,
        IApplicationSettingsService? settingsService = null)
        : base(logger, options)
    {
        ArgumentNullException.ThrowIfNull(dataPaths);

        this.recordingService = recordingService;
        this.timeProvider = timeProvider;
        this.settingsService = settingsService;
        folder = dataPaths.AvailabilityPath;
    }

    /// <inheritdoc />
    public IReadOnlyList<AvailabilityEntry> For(Guid cameraId)
        => [.. ReadCached(CameraFile(cameraId))
            .Concat(ReadCached(Path.Combine(folder, ServerFileName)))
            .OrderBy(e => e.Time),];

    /// <summary>
    /// Drops the history older than what is kept, from every file.
    /// </summary>
    /// <returns>How many changes were dropped.</returns>
    public int Trim()
    {
        var retention = settingsService?.Recording.Cleanup.RecordingRetentionDays ?? 0;
        var keptDays = retention > 0 ? Math.Max(MinimumKeptDays, retention + 1) : KeptDaysWithoutRetention;
        var keepFrom = timeProvider.GetUtcNow() - TimeSpan.FromDays(keptDays);

        var dropped = 0;
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.jsonl"))
        {
            try
            {
                dropped += AvailabilityLog.Trim(file, keepFrom);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogHistoryWriteFailed(ex, file);
            }
        }

        return dropped;
    }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        recordingService.CameraConnectionStateChanged += OnConnectionStateChanged;
        recordingService.RecordingStateChanged += OnRecordingStateChanged;
        recordingService.DiskSpaceStopped += OnDiskSpaceStopped;
        NoteUnannouncedStop();
        Write(Path.Combine(folder, ServerFileName), AvailabilityChange.ServerStarted);
        writer = Task.Run(WriteQueuedAsync, CancellationToken.None);
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        recordingService.CameraConnectionStateChanged -= OnConnectionStateChanged;
        recordingService.RecordingStateChanged -= OnRecordingStateChanged;
        recordingService.DiskSpaceStopped -= OnDiskSpaceStopped;

        // What is queued is written before the stop, so the stop is last.
        pending.Writer.TryComplete();
        if (writer is not null)
        {
            await writer.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        Write(Path.Combine(folder, ServerFileName), AvailabilityChange.ServerStopped);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        pending.Writer.TryComplete();
        base.Dispose();
    }

    /// <inheritdoc />
    public override async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        var path = Path.Combine(folder, AliveFileName);
        try
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(path, timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), stoppingToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogHistoryWriteFailed(ex, path);
        }

        var now = timeProvider.GetUtcNow();
        if (now - lastTrim >= TrimInterval)
        {
            lastTrim = now;
            Trim();
        }
    }

    // The last run ended without a stop: it stopped when it last said it was alive.
    private void NoteUnannouncedStop()
    {
        var serverFile = Path.Combine(folder, ServerFileName);
        var history = AvailabilityLog.Read(serverFile);
        if (history.Count == 0 || history[^1].Change != AvailabilityChange.ServerStarted)
        {
            return;
        }

        var started = history[^1].Time;
        var alive = started;
        try
        {
            if (DateTimeOffset.TryParse(File.ReadAllText(Path.Combine(folder, AliveFileName)), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var said) && said > started)
            {
                alive = said;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No sign of life after the start: it stopped at once.
        }

        Write(serverFile, AvailabilityChange.ServerStopped, alive);
    }

    private void OnConnectionStateChanged(
        Guid cameraId,
        ConnectionState state)
    {
        AvailabilityChange? change = state switch
        {
            ConnectionState.Connected => AvailabilityChange.Online,
            ConnectionState.Disconnected or ConnectionState.Error or ConnectionState.DeviceUnplugged => AvailabilityChange.Offline,
            _ => null,
        };

        if (change is { } noted)
        {
            Note(cameraId, connection: true, noted);
        }
    }

    // A recording that goes on in a new segment reports a stop too: only a real stop is noted.
    private void OnRecordingStateChanged(
        object? sender,
        RecordingStateChangedEventArgs e)
    {
        if (e.NewState != RecordingState.Idle)
        {
            Note(e.CameraId, connection: false, AvailabilityChange.RecordingStarted);
        }
        else if (!recordingService.IsRecording(e.CameraId))
        {
            Note(e.CameraId, connection: false, AvailabilityChange.RecordingStopped);
        }
    }

    private void OnDiskSpaceStopped(Guid cameraId)
        => Note(cameraId, connection: false, AvailabilityChange.DiskFull);

    // Noted once per change: a camera reported online twice is one line.
    private void Note(
        Guid cameraId,
        bool connection,
        AvailabilityChange change)
    {
        if (last.TryGetValue((cameraId, connection), out var previous) && previous == change)
        {
            return;
        }

        last[(cameraId, connection)] = change;

        // Queued: this runs on the thread of the camera or recording that changed.
        pending.Writer.TryWrite((CameraFile(cameraId), new AvailabilityEntry(timeProvider.GetUtcNow(), change)));
    }

    private async Task WriteQueuedAsync()
    {
        await foreach (var (path, entry) in pending.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            Write(path, entry.Change, entry.Time);
        }
    }

    // A file is parsed again only when it changed (this server appends to it, or trims it).
    private IReadOnlyList<AvailabilityEntry> ReadCached(string path)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(path);
            if (!info.Exists)
            {
                cache.TryRemove(path, out _);
                return [];
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var stamp = (info.Length, info.LastWriteTimeUtc);
        if (cache.TryGetValue(path, out var cached) && cached.Stamp == stamp)
        {
            return cached.Entries;
        }

        var entries = AvailabilityLog.Read(path);
        cache[path] = new CachedHistory(stamp, entries);
        return entries;
    }

    // The events fire on the threads that raise them: a failure here is logged, never thrown into them.
    private void Write(
        string path,
        AvailabilityChange change,
        DateTimeOffset? time = null)
    {
        try
        {
            AvailabilityLog.Append(path, new AvailabilityEntry(time ?? timeProvider.GetUtcNow(), change));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogHistoryWriteFailed(ex, path);
        }
    }

    private string CameraFile(Guid cameraId)
        => Path.Combine(folder, cameraId.ToString("N") + ".jsonl");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not write the availability history {Path}")]
    private partial void LogHistoryWriteFailed(Exception ex, string path);

    private sealed record CachedHistory(
        (long Length, DateTime LastWriteUtc) Stamp,
        IReadOnlyList<AvailabilityEntry> Entries);
}