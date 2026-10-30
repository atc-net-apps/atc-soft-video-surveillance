namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Keeps what was read from activity index files, read again when a file changed (size or time,
/// as an index grows while recording); the least recently used are dropped beyond the limit.
/// </summary>
/// <typeparam name="T">What is kept of a file.</typeparam>
internal sealed class ActivityFileCache<T>
    where T : class
{
    private readonly int capacity;
    private readonly Func<string, T?> read;
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    private long clock;

    public ActivityFileCache(
        int capacity,
        Func<string, T?> read)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentNullException.ThrowIfNull(read);

        this.capacity = capacity;
        this.read = read;
    }

    public int Count
        => entries.Count;

    public T? Get(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            entries.TryRemove(path, out _);
            return null;
        }

        var used = Interlocked.Increment(ref clock);
        if (entries.TryGetValue(path, out var kept) &&
            kept.Length == info.Length &&
            kept.LastWriteUtc == info.LastWriteTimeUtc)
        {
            kept.LastUsed = used;
            return kept.Value;
        }

        var value = read(path);
        entries[path] = new Entry(info.Length, info.LastWriteTimeUtc, value) { LastUsed = used };
        Trim();
        return value;
    }

    private void Trim()
    {
        var excess = entries.Count - capacity;
        if (excess <= 0)
        {
            return;
        }

        foreach (var path in entries.OrderBy(e => e.Value.LastUsed).Take(excess).Select(e => e.Key).ToList())
        {
            entries.TryRemove(path, out _);
        }
    }

    private sealed class Entry(
        long length,
        DateTime lastWriteUtc,
        T? value)
    {
        public long Length { get; } = length;

        public DateTime LastWriteUtc { get; } = lastWriteUtc;

        public T? Value { get; } = value;

        public long LastUsed { get; set; }
    }
}