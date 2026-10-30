namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// <see cref="IAuditLog"/> in a file in the server's data folder, one JSON line
/// per change. A file that grows past its size is moved aside (one older file is
/// kept) and a new one started; the latest changes are also kept in memory.
/// </summary>
public sealed class AuditLog : IAuditLog
{
    private const int KeptInMemory = 1000;
    private const long DefaultMaxFileBytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions LineOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Lock sync = new();
    private readonly string filePath;
    private readonly TimeProvider time;
    private readonly long maxFileBytes;
    private readonly LinkedList<AuditRecord> recent = new();

    public AuditLog(
        string filePath,
        TimeProvider time,
        long maxFileBytes = DefaultMaxFileBytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        this.filePath = filePath;
        this.time = time;
        this.maxFileBytes = maxFileBytes;

        foreach (var record in ReadLines(filePath + ".1").Concat(ReadLines(filePath)))
        {
            Remember(record);
        }
    }

    /// <inheritdoc />
    public void Record(
        string userName,
        string action,
        string target)
    {
        var record = new AuditRecord(time.GetUtcNow(), userName, action, target);
        var line = JsonSerializer.Serialize(record, LineOptions) + Environment.NewLine;

        lock (sync)
        {
            Remember(record);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                var file = new FileInfo(filePath);
                if (file.Exists && file.Length + line.Length > maxFileBytes)
                {
                    File.Move(filePath, filePath + ".1", overwrite: true);
                }

                File.AppendAllText(filePath, line);
            }
            catch (IOException)
            {
                // Kept in memory; the next change tries the file again.
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditRecord> Recent(int take)
    {
        lock (sync)
        {
            return [.. recent.Take(Math.Max(0, take))];
        }
    }

    private static IEnumerable<AuditRecord> ReadLines(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var records = new List<AuditRecord>();
        foreach (var line in File.ReadLines(path))
        {
            try
            {
                if (JsonSerializer.Deserialize<AuditRecord>(line, LineOptions) is { } record)
                {
                    records.Add(record);
                }
            }
            catch (JsonException)
            {
                // A line cut off when the server stopped.
            }
        }

        return records;
    }

    // Newest first, at most KeptInMemory.
    private void Remember(AuditRecord record)
    {
        recent.AddFirst(record);
        if (recent.Count > KeptInMemory)
        {
            recent.RemoveLast();
        }
    }
}