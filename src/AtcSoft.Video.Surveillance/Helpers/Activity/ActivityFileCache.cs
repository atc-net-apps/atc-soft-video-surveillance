namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// Keeps what was read from activity index files, read again when a file changed (size or time,
/// as an index grows while recording); the least recently used are dropped beyond the limit.
/// </summary>
/// <typeparam name="T">What is kept of a file.</typeparam>
internal sealed class ActivityFileCache<T>
    where T : class
{
    private readonly int maximumCapacity;
    private readonly Func<string, T?> read;
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock trimGate = new();
    private int capacity;
    private long clock;

    public ActivityFileCache(
        int capacity,
        Func<string, T?> read,
        int? maximumCapacity = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentNullException.ThrowIfNull(read);

        this.capacity = capacity;
        this.maximumCapacity = Math.Max(capacity, maximumCapacity ?? capacity);
        this.read = read;
    }

    public int Count
        => entries.Count;

    public int Capacity
        => Volatile.Read(ref capacity);

    /// <summary>
    /// Makes room for a request that reads <paramref name="count"/> files, up to the maximum.
    /// A request that reads more files than the cache keeps would read every one of them again
    /// each time: the least recently used is always the next one it needs.
    /// </summary>
    public void Reserve(int count)
    {
        var wanted = Math.Min(maximumCapacity, count + (count / 10));
        int current;
        do
        {
            current = Volatile.Read(ref capacity);
            if (wanted <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref capacity, wanted, current) != current);
    }

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

    // The least recently used tenth goes at once: sorting every entry for each one added past
    // the limit made a full cache slower than reading the files.
    private void Trim()
    {
        var limit = Capacity;
        if (entries.Count <= limit)
        {
            return;
        }

        lock (trimGate)
        {
            var excess = entries.Count - (limit - (limit / 10));
            if (entries.Count <= limit || excess <= 0)
            {
                return;
            }

            foreach (var path in entries.OrderBy(e => e.Value.LastUsed).Take(excess).Select(e => e.Key).ToList())
            {
                entries.TryRemove(path, out _);
            }
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