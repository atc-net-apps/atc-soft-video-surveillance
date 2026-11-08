namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

using AvailabilityChange = AtcSoft.Video.Surveillance.Enums.AvailabilityChange;
using AvailabilityEntry = AtcSoft.Video.Surveillance.Models.AvailabilityEntry;
using AvailabilityFolder = AtcSoft.Video.Surveillance.Helpers.Activity.AvailabilityFolder;
using AvailabilityLog = AtcSoft.Video.Surveillance.Helpers.Activity.AvailabilityLog;
using CameraWallLogHistory = AtcSoft.Video.Surveillance.Helpers.Activity.CameraWallLogHistory;

/// <summary>
/// CameraWall's availability history (<see cref="AvailabilityFolder"/>, in the data folder, so a
/// shared data folder shares it too): each camera going online or offline, and the app's own starts
/// and stops. The timeline shows from it when a camera was offline and when CameraWall was not running.
/// </summary>
/// <remarks>
/// The changes come on the UI thread: they are queued and written by a writer of their own, so the
/// UI never waits for the disk. <see cref="DoWorkAsync"/> notes the time in
/// <see cref="AvailabilityFolder.AliveFileName"/>, so a run that ended without a stop is noted as
/// stopped then, at the next start; once a day it drops what is older than the recordings kept.
/// </remarks>
// Not auto-registered via [Registration]: the App registers one singleton as ICameraAvailabilityNotes and as a hosted service.
public sealed partial class CameraAvailabilityService : BackgroundServiceBase<CameraAvailabilityService>, ICameraAvailabilityNotes
{
    // History is kept a little longer than the recordings, and at least a month.
    private const int MinimumKeptDays = 31;
    private const int KeptDaysWithoutRetention = 400;

    private static readonly TimeSpan TrimInterval = TimeSpan.FromDays(1);

    private readonly string folder;
    private readonly IApplicationSettingsService? settingsService;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<Guid, AvailabilityChange> last = new();

    // Changes waiting to be written; the oldest go first when a disk hangs.
    private readonly Channel<(string Path, AvailabilityEntry Entry)> pending =
        Channel.CreateBounded<(string Path, AvailabilityEntry Entry)>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    private Task? writer;
    private DateTimeOffset lastTrim = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraAvailabilityService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">How often the app notes that it is alive.</param>
    /// <param name="settingsService">The settings: how long recordings are kept.</param>
    /// <param name="folder">The folder of the history; CameraWall's own when not given.</param>
    /// <param name="timeProvider">The clock; the system's when not given.</param>
    public CameraAvailabilityService(
        ILogger<CameraAvailabilityService> logger,
        CameraAvailabilityServiceOptions options,
        IApplicationSettingsService? settingsService = null,
        string? folder = null,
        TimeProvider? timeProvider = null)
        : base(logger, options)
    {
        this.settingsService = settingsService;
        this.folder = folder ?? AtcSoft.Video.Surveillance.Wpf.Core.ApplicationPaths.DefaultAvailabilityPath;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public void NoteConnection(
        Guid cameraId,
        string state)
    {
        if (CameraWallLogHistory.FromConnectionState(state) is not { } change)
        {
            return;
        }

        // Noted once per change: 169 failed attempts to reconnect are one line.
        if (last.TryGetValue(cameraId, out var previous) && previous == change)
        {
            return;
        }

        last[cameraId] = change;
        pending.Writer.TryWrite((AvailabilityFolder.CameraFile(folder, cameraId), new AvailabilityEntry(timeProvider.GetUtcNow(), change)));
    }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var appFile = Path.Combine(folder, AvailabilityFolder.AppFileName);
        try
        {
            if (AvailabilityFolder.FindUnannouncedStop(folder) is { } stopped)
            {
                Write(appFile, new AvailabilityEntry(stopped, AvailabilityChange.ServerStopped));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogHistoryWriteFailed(ex, appFile);
        }

        Write(appFile, new AvailabilityEntry(timeProvider.GetUtcNow(), AvailabilityChange.ServerStarted));
        writer = Task.Run(WriteQueuedAsync, CancellationToken.None);
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // What is queued is written before the stop, so the stop is last.
        pending.Writer.TryComplete();
        if (writer is not null)
        {
            await writer.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        Write(Path.Combine(folder, AvailabilityFolder.AppFileName), new AvailabilityEntry(timeProvider.GetUtcNow(), AvailabilityChange.ServerStopped));
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
        var path = Path.Combine(folder, AvailabilityFolder.AliveFileName);
        try
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(path, timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogHistoryWriteFailed(ex, path);
        }

        var now = timeProvider.GetUtcNow();
        if (now - lastTrim >= TrimInterval)
        {
            lastTrim = now;
            Trim(now);
        }
    }

    private void Trim(DateTimeOffset now)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var retention = settingsService?.Recording.Cleanup.RecordingRetentionDays ?? 0;
        var keptDays = retention > 0 ? Math.Max(MinimumKeptDays, retention + 1) : KeptDaysWithoutRetention;
        var keepFrom = now - TimeSpan.FromDays(keptDays);
        foreach (var file in Directory.EnumerateFiles(folder, "*.jsonl"))
        {
            try
            {
                AvailabilityLog.Trim(file, keepFrom);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogHistoryWriteFailed(ex, file);
            }
        }
    }

    private async Task WriteQueuedAsync()
    {
        await foreach (var (path, entry) in pending.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            Write(path, entry);
        }
    }

    private void Write(
        string path,
        AvailabilityEntry entry)
    {
        try
        {
            AvailabilityLog.Append(path, entry);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogHistoryWriteFailed(ex, path);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not write the availability history {Path}")]
    private partial void LogHistoryWriteFailed(Exception ex, string path);
}