namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// The availability history of CameraWall's cameras over a span, to explain the gaps of its
/// timeline: its own history (<see cref="AvailabilityFolder"/>), and before that began, what its
/// log tells (<see cref="CameraWallLogHistory"/>).
/// </summary>
public static class CameraWallAvailability
{
    /// <summary>
    /// Reads every camera's history for a span.
    /// </summary>
    /// <param name="cameras">The cameras.</param>
    /// <param name="availabilityFolder">The folder of its history, or <see langword="null"/>.</param>
    /// <param name="logFolder">Its log folder, or <see langword="null"/>.</param>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span.</param>
    /// <param name="zone">The zone of the log's file names and of a time written without one.</param>
    /// <param name="cache">The log files already read.</param>
    /// <returns>Each camera's history, with the app's starts and stops, oldest first.</returns>
    public static IReadOnlyDictionary<Guid, IReadOnlyList<AvailabilityEntry>> Read(
        IReadOnlyList<TimelineCamera> cameras,
        string? availabilityFolder,
        string? logFolder,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeZoneInfo zone,
        LogHistoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(cache);

        var kept = string.IsNullOrEmpty(availabilityFolder) ? null : AvailabilityFolder.Earliest(availabilityFolder);

        // The app's own history begins with a start, after which it tells every change: the log is
        // read only for a span that starts before that.
        var log = kept is null || from < kept
            ? CameraWallLogHistory.Read(logFolder, from, kept is { } until && until < to ? until : to, zone, cache)
            : null;

        return cameras.ToDictionary(
            c => c.Id,
            c => (IReadOnlyList<AvailabilityEntry>)[.. (log?.For(c.Name, kept) ?? [])
                .Concat(string.IsNullOrEmpty(availabilityFolder) ? [] : AvailabilityFolder.Read(availabilityFolder, c.Id))
                .OrderBy(e => e.Time),]);
    }
}