namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Timeline;

/// <summary>
/// The recordings a timeline is built from: the server's recordings that touch a span, with
/// their activity summaries, per camera. The summaries are read in parallel, as a cold span of
/// days reads many indexes.
/// </summary>
internal static class TimelineRecordings
{
    /// <summary>
    /// The longest span a timeline covers.
    /// </summary>
    public static readonly TimeSpan MaximumSpan = TimeSpan.FromDays(7);

    public static Dictionary<Guid, List<AtcSoft.VideoSurveillance.Models.TimelineRecording>> Find(
        IReadOnlyList<RecordingFile> files,
        DateTimeOffset from,
        DateTimeOffset to,
        ActivitySummaryCache cache,
        CancellationToken cancellationToken)
    {
        var touching = files
            .Where(f => f.StartedAt < to && f.LastWrittenAt > from)
            .ToList();

        var summaries = new AtcSoft.VideoSurveillance.Models.ActivitySummary?[touching.Count];
        Parallel.For(
            0,
            touching.Count,
            new ParallelOptions { CancellationToken = cancellationToken },
            i => summaries[i] = cache.Get(RecordingPathHelper.BuildActivityPath(touching[i].FullPath)));

        var byCamera = new Dictionary<Guid, List<AtcSoft.VideoSurveillance.Models.TimelineRecording>>();
        for (var i = 0; i < touching.Count; i++)
        {
            var file = touching[i];
            if (!byCamera.TryGetValue(file.CameraId, out var list))
            {
                list = [];
                byCamera[file.CameraId] = list;
            }

            list.Add(new AtcSoft.VideoSurveillance.Models.TimelineRecording(file.RelativePath, file.StartedAt, file.LastWrittenAt, summaries[i]));
        }

        return byCamera;
    }

    public static TimelineEvent ToApi(
        Guid cameraId,
        AtcSoft.VideoSurveillance.Models.TimelineActivityEvent activityEvent)
        => new(
            CameraId: cameraId,
            Start: activityEvent.Start,
            End: activityEvent.End,
            PeakLevel: activityEvent.PeakLevel,
            Tracks: activityEvent.Tracks,
            FilePath: activityEvent.VideoPath,
            OffsetSeconds: activityEvent.OffsetSeconds);
}