namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Notes when each camera could be recorded and when not, so the timeline can say why it has a
/// gap: a camera going online or offline, its recording starting or stopping, a recording stopped
/// or refused for a full disk, and the server's own starts and stops. One JSON-lines file per
/// camera in <see cref="ServerDataPaths.AvailabilityPath"/>, one for the server.
/// </summary>
/// <remarks>
/// The changes are noted as their events fire. <see cref="DoWorkAsync"/> only notes the time in
/// <c>server.alive</c>: a server that stopped without saying so (a crash, a power cut) is noted as
/// stopped then, at its next start.
/// </remarks>
public sealed partial class AvailabilityHistoryService : BackgroundServiceBase<AvailabilityHistoryService>, IAvailabilityHistory
{
    private const string ServerFileName = "server.jsonl";
    private const string AliveFileName = "server.alive";

    private readonly ServerRecordingService recordingService;
    private readonly string folder;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<(Guid Camera, bool Connection), AvailabilityChange> last = new();

    public AvailabilityHistoryService(
        ILogger<AvailabilityHistoryService> logger,
        AvailabilityHistoryServiceOptions options,
        ServerRecordingService recordingService,
        ServerDataPaths dataPaths,
        TimeProvider timeProvider)
        : base(logger, options)
    {
        ArgumentNullException.ThrowIfNull(dataPaths);

        this.recordingService = recordingService;
        this.timeProvider = timeProvider;
        folder = dataPaths.AvailabilityPath;
    }

    /// <inheritdoc />
    public IReadOnlyList<AvailabilityEntry> For(Guid cameraId)
        => [.. AvailabilityLog.Read(CameraFile(cameraId))
            .Concat(AvailabilityLog.Read(Path.Combine(folder, ServerFileName)))
            .OrderBy(e => e.Time)];

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        recordingService.CameraConnectionStateChanged += OnConnectionStateChanged;
        recordingService.RecordingStateChanged += OnRecordingStateChanged;
        recordingService.DiskSpaceStopped += OnDiskSpaceStopped;
        NoteUnannouncedStop();
        Write(Path.Combine(folder, ServerFileName), AvailabilityChange.ServerStarted);
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        recordingService.CameraConnectionStateChanged -= OnConnectionStateChanged;
        recordingService.RecordingStateChanged -= OnRecordingStateChanged;
        recordingService.DiskSpaceStopped -= OnDiskSpaceStopped;
        Write(Path.Combine(folder, ServerFileName), AvailabilityChange.ServerStopped);
        return base.StopAsync(cancellationToken);
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
        Write(CameraFile(cameraId), change);
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
}