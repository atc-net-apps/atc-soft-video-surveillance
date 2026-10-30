namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Reads a recording's activity index, also while it is being written. A line that is cut off
/// or not understood is skipped; the rest of the index is still read.
/// </summary>
public static class ActivityLogReader
{
    /// <summary>
    /// Reads the activity index file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The file, from <see cref="RecordingPathHelper.BuildActivityPath"/>.</param>
    /// <returns>The index, or <see langword="null"/> when there is no file or it has no header.</returns>
    public static ActivityLog? Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return Read(reader);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads an activity index.
    /// </summary>
    /// <param name="reader">The lines.</param>
    /// <returns>The index, or <see langword="null"/> when it has no header.</returns>
    public static ActivityLog? Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.ReadLine() is not { } header ||
            !ActivityLogFormat.TryReadHeader(header, out var start))
        {
            return null;
        }

        // The lines come in order, one per second; anything else (a second written twice, a
        // line out of place) is sorted out at the end.
        var seconds = new List<ActivitySecond>(capacity: 4096);
        var inOrder = true;
        while (reader.ReadLine() is { } line)
        {
            if (!ActivityLogFormat.TryReadSecond(line, out var second))
            {
                continue;
            }

            inOrder &= seconds.Count == 0 || second.Offset > seconds[^1].Offset;
            seconds.Add(second);
        }

        return new ActivityLog(start, inOrder ? seconds : Merge(seconds));
    }

    // Should a second come twice, the most of both counts.
    private static List<ActivitySecond> Merge(List<ActivitySecond> seconds)
        => [.. seconds
            .GroupBy(s => s.Offset)
            .OrderBy(g => g.Key)
            .Select(g => g.Count() == 1
                ? g.First()
                : new ActivitySecond(
                    g.Key,
                    g.Max(s => s.Level),
                    g.Max(s => s.Tracks),
                    [.. g.SelectMany(s => s.TrackIds).Distinct().Order()])
                {
                    Zones = [.. g.SelectMany(s => s.Zones).Distinct(StringComparer.Ordinal)],
                })];
}