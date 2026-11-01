namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// Brings the recordings list up to date after a recording event from the newest recordings only:
/// a recording that starts, ends or moves on to its next file changes the newest part of the list,
/// never the old part. Before, the whole archive was fetched again for every event (every camera at
/// every segment switch, in every open tab).
/// </summary>
public static class RecordingListPatch
{
    /// <summary>
    /// How far back the newest recordings are fetched: a file that ends now started at most this
    /// long ago (segments are an hour or less), unless one still running started earlier.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(2);

    /// <summary>
    /// Gets from when the newest recordings are fetched: the window, or the start of the oldest
    /// recording still running when that is earlier.
    /// </summary>
    /// <param name="current">The list as it is.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The moment from which the server is asked.</returns>
    public static DateTimeOffset Since(
        IReadOnlyCollection<Recording> current,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);

        var since = now - Window;
        foreach (var recording in current)
        {
            if (recording.InProgress && recording.StartedAt < since)
            {
                since = recording.StartedAt;
            }
        }

        return since;
    }

    /// <summary>
    /// Puts the newest recordings in place of the list's part from <paramref name="since"/> on:
    /// what ended has its length, what started is added, what was deleted is gone.
    /// </summary>
    /// <param name="current">The list as it is.</param>
    /// <param name="recent">The recordings the server has from <paramref name="since"/> on.</param>
    /// <param name="since">From when <paramref name="recent"/> was asked.</param>
    /// <returns>The list, newest first.</returns>
    public static Recording[] Merge(
        IEnumerable<Recording> current,
        IEnumerable<Recording> recent,
        DateTimeOffset since)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(recent);

        return
        [
            .. current
                .Where(r => r.StartedAt < since)
                .Concat(recent)
                .OrderByDescending(r => r.StartedAt),
        ];
    }
}