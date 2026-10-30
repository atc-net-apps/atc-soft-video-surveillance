namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Timeline;

/// <summary>
/// Handler business logic for the GetTimeline operation: one lane per camera over a span, built
/// from the recordings and the summaries of their activity indexes.
/// </summary>
/// <param name="recordingService">The recordings in progress.</param>
/// <param name="settingsService">The application settings.</param>
/// <param name="cameraStorageService">The cameras.</param>
/// <param name="summaryCache">The summaries of the activity indexes already read.</param>
/// <param name="timeProvider">The clock: a recording in progress reaches now.</param>
/// <param name="availabilityHistory">When each camera could be recorded, to say why a gap is there; without it every gap is unknown.</param>
public sealed class GetTimelineHandler(
    IRecordingService recordingService,
    IApplicationSettingsService settingsService,
    ICameraStorageService cameraStorageService,
    ActivitySummaryCache summaryCache,
    TimeProvider timeProvider,
    IAvailabilityHistory? availabilityHistory = null) : IGetTimelineHandler
{
    /// <summary>
    /// How many stretches when the caller does not say: the spec's default.
    /// </summary>
    public const int DefaultBuckets = 1000;

    public Task<GetTimelineResult> ExecuteAsync(
        GetTimelineParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var (from, to) = (parameters.From, parameters.To);
        if (to <= from)
        {
            return Task.FromResult(GetTimelineResult.BadRequest("The span must end after it starts."));
        }

        if (to - from > TimelineRecordings.MaximumSpan)
        {
            return Task.FromResult(GetTimelineResult.BadRequest("The span may be at most seven days."));
        }

        var cameras = cameraStorageService.GetAllCameras()
            .Where(c => parameters.CameraId is null || c.Id == parameters.CameraId)
            .ToList();
        var files = RecordingCatalog.Find(
            settingsService.Recording.RecordingPath,
            cameras,
            recordingService.GetActiveSessions(),
            parameters.CameraId,
            timeProvider.GetUtcNow());
        var byCamera = TimelineRecordings.Find(files, from, to, summaryCache, cancellationToken);
        var buckets = parameters.Buckets ?? DefaultBuckets;

        var lanes = cameras
            .Select(camera =>
            {
                var timeline = TimelineBuilder.Build(byCamera.GetValueOrDefault(camera.Id) ?? [], from, to, buckets);
                var gaps = TimelineGaps.Find(timeline.Coverage, from, to, availabilityHistory?.For(camera.Id) ?? []);
                return new TimelineLane(
                    CameraId: camera.Id,
                    CameraName: camera.Display.DisplayName,
                    Coverage: [.. timeline.Coverage.Select(c => new TimelineCoverage(Start: c.Start, End: c.End, FilePath: c.VideoPath, RecordingStart: c.RecordingStart))],
                    Gaps: [.. gaps.Select(g => new TimelineGap(Start: g.Start, End: g.End, Reason: ToApi(g.Reason)))],
                    Buckets: [.. timeline.Stretches.Select(s => new TimelineBucket(Level: s.Level, Tracks: s.Tracks, IsRecorded: s.IsRecorded, IsAnalysed: s.IsAnalysed))],
                    Events: [.. timeline.Events.Select(e => TimelineRecordings.ToApi(camera.Id, e))]);
            })
            .ToList();

        return Task.FromResult(GetTimelineResult.Ok(new global::VideoSurveillance.Generated.Timeline.Models.Timeline(From: from, To: to, Lanes: lanes)));
    }

    private static TimelineGapReason ToApi(
        AtcSoft.VideoSurveillance.Enums.GapReason reason)
        => reason switch
        {
            AtcSoft.VideoSurveillance.Enums.GapReason.Offline => TimelineGapReason.Offline,
            AtcSoft.VideoSurveillance.Enums.GapReason.NotRecording => TimelineGapReason.NotRecording,
            AtcSoft.VideoSurveillance.Enums.GapReason.DiskFull => TimelineGapReason.DiskFull,
            AtcSoft.VideoSurveillance.Enums.GapReason.ServerDown => TimelineGapReason.ServerDown,
            _ => TimelineGapReason.Unknown,
        };
}