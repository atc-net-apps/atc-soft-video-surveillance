namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// The changes already read from CameraWall's log files (<see cref="CameraWallLogHistory"/>): a
/// file is read again only when its length or last write time changed, so a timeline that reloads
/// every few seconds reads a day's log once.
/// </summary>
public sealed class LogHistoryCache
{
    private readonly ConcurrentDictionary<string, Cached> files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the changes of a file, read again when it changed.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="read">Reads the file.</param>
    /// <returns>The changes in the file.</returns>
    public IReadOnlyList<(string? CameraName, AvailabilityEntry Entry)> Get(
        string path,
        Func<string, List<(string? CameraName, AvailabilityEntry Entry)>> read)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(read);

        (long Length, DateTime LastWriteUtc) stamp;
        try
        {
            var info = new FileInfo(path);
            stamp = (info.Length, info.LastWriteTimeUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        if (files.TryGetValue(path, out var cached) && cached.Stamp == stamp)
        {
            return cached.Entries;
        }

        var entries = read(path);
        files[path] = new Cached(stamp, entries);
        return entries;
    }

    private sealed record Cached(
        (long Length, DateTime LastWriteUtc) Stamp,
        IReadOnlyList<(string? CameraName, AvailabilityEntry Entry)> Entries);
}