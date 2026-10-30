namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// A timeline of every camera built from recording folders on this computer: CameraWall's, which
/// has no server. The folders are the global recording folder and each camera's own; a file
/// belongs to the camera whose folder it is in, below the innermost of them. The time in a file's
/// name is read in the zone it was written in (CameraWall writes local time); a recording in
/// progress reaches now.
/// </summary>
/// <remarks>
/// A camera that was renamed has its older recordings in a folder of its old name, which no camera
/// owns: they get no lane, as on the server. On the night the clocks go back, a name in the hour
/// that comes twice is read as its first time.
/// </remarks>
public static class LocalTimeline
{
    /// <summary>
    /// The longest span a timeline covers, and how far previous and next event look.
    /// </summary>
    public static readonly TimeSpan MaximumSpan = TimeSpan.FromDays(7);

    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi"];

    /// <summary>
    /// Finds every camera's recordings that touch a span, with the summaries of their activity.
    /// </summary>
    /// <param name="folders">The recording folders: the global one and each camera's own.</param>
    /// <param name="cameras">The cameras.</param>
    /// <param name="sessions">The recordings in progress.</param>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span.</param>
    /// <param name="nameZone">The zone the times in the files' names are in.</param>
    /// <param name="now">Now: where a recording in progress has reached.</param>
    /// <param name="cache">The summaries already read.</param>
    /// <returns>Per camera, its recordings in no particular order.</returns>
    public static IReadOnlyDictionary<Guid, IReadOnlyList<TimelineRecording>> FindRecordings(
        IReadOnlyList<string?> folders,
        IReadOnlyList<TimelineCamera> cameras,
        IReadOnlyList<RecordingSession> sessions,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeZoneInfo nameZone,
        DateTimeOffset now,
        ActivitySummaryCache cache)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(nameZone);
        ArgumentNullException.ThrowIfNull(cache);

        var cameraIds = new HashSet<Guid>(cameras.Select(c => c.Id));
        var camerasByFolder = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var camera in cameras)
        {
            camerasByFolder.TryAdd(RecordingPathHelper.GetCameraFolderName(camera.Name, camera.Id), camera.Id);
        }

        // The file of a recording in progress is written as it goes: its last write time lags.
        var found = sessions
            .Where(s => cameraIds.Contains(s.CameraId) && !string.IsNullOrEmpty(s.CurrentFilePath))
            .Select(s => (s.CameraId, Path: s.CurrentFilePath, Start: ToUtc(s.StartTime), End: now))
            .ToList();
        var inProgress = new HashSet<string>(found.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);

        var existing = folders.Where(f => !string.IsNullOrWhiteSpace(f) && Directory.Exists(f)).ToList();
        foreach (var root in RecordingRoots.Collect(null, existing))
        {
            foreach (var file in EnumerateVideos(root).Where(f => !inProgress.Contains(f)))
            {
                var owner = RecordingRoots.Innermost(file, existing) ?? root;
                if (RecordingPathHelper.IsInThumbnailsFolder(file, owner) ||
                    RecordingPathHelper.GetCameraFolderFromPath(file, owner) is not { } folder ||
                    !camerasByFolder.TryGetValue(folder, out var cameraId))
                {
                    continue;
                }

                var info = new FileInfo(file);
                var start = RecordingPathHelper.TryParseTimestamp(info.Name, out var named)
                    ? InZone(named, nameZone)
                    : new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero);
                found.Add((cameraId, file, start, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)));
            }
        }

        var touching = found.Where(f => f.Start < to && f.End > from).ToList();
        var summaries = new ActivitySummary?[touching.Count];
        Parallel.For(0, touching.Count, i => summaries[i] = cache.Get(RecordingPathHelper.BuildActivityPath(touching[i].Path)));

        var byCamera = new Dictionary<Guid, List<TimelineRecording>>();
        for (var i = 0; i < touching.Count; i++)
        {
            var (cameraId, path, start, end) = touching[i];
            if (!byCamera.TryGetValue(cameraId, out var list))
            {
                list = [];
                byCamera[cameraId] = list;
            }

            list.Add(new TimelineRecording(path, start, end, summaries[i]));
        }

        return byCamera.ToDictionary(p => p.Key, p => (IReadOnlyList<TimelineRecording>)p.Value);
    }

    /// <summary>
    /// Finds the nearest event after or before a moment, across every camera, looking at most
    /// <see cref="MaximumSpan"/> away.
    /// </summary>
    /// <param name="recordings">Per camera, its recordings around the moment.</param>
    /// <param name="moment">Where to look from.</param>
    /// <param name="forward">After the moment when true, else before it.</param>
    /// <returns>The camera and the event, or <see langword="null"/> when there is none.</returns>
    public static (Guid CameraId, TimelineActivityEvent Event)? FindEvent(
        IReadOnlyDictionary<Guid, IReadOnlyList<TimelineRecording>> recordings,
        DateTimeOffset moment,
        bool forward)
    {
        ArgumentNullException.ThrowIfNull(recordings);

        var events = recordings
            .SelectMany(p => TimelineBuilder.FindEvents(p.Value).Select(e => (CameraId: p.Key, Event: e)))
            .ToList();
        var found = forward
            ? events.Where(e => e.Event.Start > moment && e.Event.Start - moment <= MaximumSpan).OrderBy(e => e.Event.Start).FirstOrDefault()
            : events.Where(e => e.Event.Start < moment && moment - e.Event.Start <= MaximumSpan).OrderByDescending(e => e.Event.Start).FirstOrDefault();
        return found.Event is null ? null : found;
    }

    private static IEnumerable<string> EnumerateVideos(string root)
    {
        try
        {
            return Directory
                .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => VideoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be read (a disconnected drive) has no recordings to show.
            return [];
        }
    }

    // A time read from a name has no zone of its own: the zone it was written in gives it one.
    internal static DateTimeOffset InZone(
        DateTime named,
        TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(named, DateTimeKind.Unspecified);
        var offset = zone.IsInvalidTime(local)
            ? zone.GetUtcOffset(local.AddHours(1))
            : zone.IsAmbiguousTime(local)
                ? zone.GetAmbiguousTimeOffsets(local).Max()
                : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private static DateTimeOffset ToUtc(DateTime time)
        => time.Kind == DateTimeKind.Local
            ? new DateTimeOffset(time.ToUniversalTime())
            : new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc));
}