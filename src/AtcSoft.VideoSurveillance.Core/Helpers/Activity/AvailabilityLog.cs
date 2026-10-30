namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The history of whether a camera could be recorded: one JSON line per change,
/// <c>{"t":"2026-10-19T09:00:00.000Z","c":"offline"}</c>, appended as it happens. A line that is
/// cut off or not understood is skipped. The server keeps one file per camera and one for itself.
/// </summary>
public static class AvailabilityLog
{
    private static readonly Lock Writing = new();

    /// <summary>
    /// Appends a change to the history file at the path.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="entry">The change.</param>
    public static void Append(
        string path,
        AvailabilityEntry entry)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(entry);

        var line = Write(entry) + "\n";
        lock (Writing)
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }

            File.AppendAllText(path, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    /// <summary>
    /// Reads the history file at the path.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The changes, oldest first; none when there is no file.</returns>
    public static IReadOnlyList<AvailabilityEntry> Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string[] lines;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            lines = reader.ReadToEnd().Split('\n');
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Missing or unreadable: a gap is then explained as unknown, the timeline still answers.
            return [];
        }

        return [.. lines.Select(Parse).OfType<AvailabilityEntry>().OrderBy(e => e.Time)];
    }

    private static string Write(AvailabilityEntry entry)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("t", entry.Time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
            writer.WriteString("c", Name(entry.Change));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static AvailabilityEntry? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("t", out var t) &&
                   t.ValueKind == JsonValueKind.String &&
                   DateTimeOffset.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time) &&
                   root.TryGetProperty("c", out var c) &&
                   c.ValueKind == JsonValueKind.String &&
                   Change(c.GetString()) is { } change
                ? new AvailabilityEntry(time, change)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Name(AvailabilityChange change)
        => change switch
        {
            AvailabilityChange.Online => "online",
            AvailabilityChange.Offline => "offline",
            AvailabilityChange.RecordingStarted => "recordingStarted",
            AvailabilityChange.RecordingStopped => "recordingStopped",
            AvailabilityChange.DiskFull => "diskFull",
            AvailabilityChange.ServerStarted => "serverStarted",
            AvailabilityChange.ServerStopped => "serverStopped",
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, message: null),
        };

    private static AvailabilityChange? Change(string? name)
        => name switch
        {
            "online" => AvailabilityChange.Online,
            "offline" => AvailabilityChange.Offline,
            "recordingStarted" => AvailabilityChange.RecordingStarted,
            "recordingStopped" => AvailabilityChange.RecordingStopped,
            "diskFull" => AvailabilityChange.DiskFull,
            "serverStarted" => AvailabilityChange.ServerStarted,
            "serverStopped" => AvailabilityChange.ServerStopped,
            _ => null,
        };
}