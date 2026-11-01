namespace AtcSoft.Video.Surveillance.Wpf.Core.Helpers;

/// <summary>
/// The lengths of finished recordings, read once: probing a file takes some 25-50 ms, so an
/// archive of a few thousand hourly recordings took close to a minute each time the recordings
/// browser opened. A file is known by its path, size and last write; a changed file is read again.
/// </summary>
public sealed class RecordingDurationCache
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Length, DateTime LastWriteUtc, TimeSpan Duration)> durations =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Func<string, TimeSpan> probe;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingDurationCache"/> class.
    /// </summary>
    /// <param name="probe">Reads a file's length; <see langword="null"/> asks FFmpeg.</param>
    public RecordingDurationCache(Func<string, TimeSpan>? probe = null)
        => this.probe = probe ?? MediaProbe.GetDuration;

    /// <summary>
    /// Gets the length of a file read before, if the file has not changed since.
    /// </summary>
    /// <param name="filePath">The recording.</param>
    /// <param name="duration">The length.</param>
    /// <returns><see langword="true"/> when the length is known.</returns>
    public bool TryGet(
        string filePath,
        out TimeSpan duration)
    {
        if (durations.TryGetValue(filePath, out var known) &&
            Stamp(filePath) is { } stamp &&
            stamp == (known.Length, known.LastWriteUtc))
        {
            duration = known.Duration;
            return true;
        }

        duration = TimeSpan.Zero;
        return false;
    }

    /// <summary>
    /// Gets the length of a file, read from the file when it is not known yet.
    /// </summary>
    /// <param name="filePath">The recording.</param>
    /// <returns>The length; zero for a file that cannot be read.</returns>
    public TimeSpan Get(string filePath)
    {
        if (TryGet(filePath, out var duration))
        {
            return duration;
        }

        duration = probe(filePath);
        if (Stamp(filePath) is { } stamp)
        {
            durations[filePath] = (stamp.Length, stamp.LastWriteUtc, duration);
        }

        return duration;
    }

    private static (long Length, DateTime LastWriteUtc)? Stamp(string filePath)
    {
        var info = new FileInfo(filePath);
        return info.Exists ? (info.Length, info.LastWriteTimeUtc) : null;
    }
}