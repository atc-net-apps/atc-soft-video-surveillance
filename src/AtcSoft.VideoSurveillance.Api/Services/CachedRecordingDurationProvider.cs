namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Reads the length of a recording from the file and remembers it. A
/// recording does not change once it is finished, so each file is opened
/// once; the listing asks for every file every time.
/// <para>
/// Opening a file takes tens of milliseconds and an archive holds tens of
/// thousands of them, so a listing never waits for all of them: what fits a
/// small budget is read on the spot, the rest is read on a background
/// worker and is there the next time the file is asked for.
/// </para>
/// </summary>
public sealed class CachedRecordingDurationProvider : IRecordingDurationProvider, IDisposable
{
    // Enough for a screenful of new recordings; not enough to be noticed.
    private static readonly TimeSpan DefaultSynchronousBudget = TimeSpan.FromMilliseconds(250);

    // The budget is per burst of calls: it is refilled when nothing has
    // been asked for this long.
    private static readonly TimeSpan BudgetWindow = TimeSpan.FromSeconds(1);

    private readonly Func<string, TimeSpan> probe;
    private readonly TimeSpan synchronousBudget;
    private readonly ConcurrentDictionary<string, Entry> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Channels.Channel<string> queue =
        System.Threading.Channels.Channel.CreateUnbounded<string>(new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });

    private readonly CancellationTokenSource stopping = new();
    private readonly Lock budgetLock = new();
    private long windowStartedAt;
    private TimeSpan spentInWindow;
    private int workerStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedRecordingDurationProvider"/> class
    /// that reads lengths with the video engine.
    /// </summary>
    public CachedRecordingDurationProvider()
        : this(MediaProbe.GetDuration, DefaultSynchronousBudget)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedRecordingDurationProvider"/> class.
    /// </summary>
    /// <param name="probe">Reads the length of a file; zero when it cannot.</param>
    /// <param name="synchronousBudget">How long a burst of calls may spend reading files on the spot.</param>
    public CachedRecordingDurationProvider(
        Func<string, TimeSpan> probe,
        TimeSpan synchronousBudget)
    {
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        this.synchronousBudget = synchronousBudget;
    }

    /// <summary>
    /// Gets the number of files waiting to be read in the background.
    /// </summary>
    public int PendingCount => pending.Count;

    /// <inheritdoc />
    public TimeSpan? GetDuration(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
            {
                cache.TryRemove(filePath, out _);
                return null;
            }

            // The size and the time of the last write tell a changed file
            // (or another file under the same name) from the one that was read.
            if (cache.TryGetValue(filePath, out var entry) &&
                entry.Length == info.Length &&
                entry.LastWriteUtc == info.LastWriteTimeUtc)
            {
                return entry.Duration;
            }

            if (pending.ContainsKey(filePath))
            {
                return null;
            }

            if (!TryTakeBudget())
            {
                ReadInBackground(filePath);
                return null;
            }

            var started = Stopwatch.GetTimestamp();
            var duration = Read(filePath, info);
            Spend(Stopwatch.GetElapsedTime(started));
            return duration;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        stopping.Cancel();
        queue.Writer.TryComplete();
        stopping.Dispose();
    }

    private TimeSpan? Read(
        string filePath,
        FileInfo info)
    {
        var duration = probe(filePath);
        if (duration <= TimeSpan.Zero)
        {
            return null;
        }

        cache[filePath] = new Entry(info.Length, info.LastWriteTimeUtc, duration);
        return duration;
    }

    private bool TryTakeBudget()
    {
        lock (budgetLock)
        {
            var now = Environment.TickCount64;
            if (now - windowStartedAt > BudgetWindow.TotalMilliseconds)
            {
                windowStartedAt = now;
                spentInWindow = TimeSpan.Zero;
            }

            return spentInWindow < synchronousBudget;
        }
    }

    private void Spend(TimeSpan elapsed)
    {
        lock (budgetLock)
        {
            spentInWindow += elapsed;

            // Keeps the window open while a long listing is still asking.
            windowStartedAt = Environment.TickCount64;
        }
    }

    private void ReadInBackground(string filePath)
    {
        if (!pending.TryAdd(filePath, 0))
        {
            return;
        }

        if (!queue.Writer.TryWrite(filePath))
        {
            pending.TryRemove(filePath, out _);
            return;
        }

        // The worker is started by the first file that needs it.
        if (Interlocked.Exchange(ref workerStarted, 1) == 0)
        {
            var token = stopping.Token;
            _ = Task.Run(() => RunWorkerAsync(token), token);
        }
    }

    private async Task RunWorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var filePath in queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var info = new FileInfo(filePath);
                    if (info.Exists)
                    {
                        Read(filePath, info);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    // Left without a length; asked for again next time.
                }
                finally
                {
                    pending.TryRemove(filePath, out _);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The server is stopping.
        }
    }

    private readonly record struct Entry(long Length, DateTime LastWriteUtc, TimeSpan Duration);
}