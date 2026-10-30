namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Timeline;

/// <summary>
/// Handler business logic for the ListTimelineEvents operation: the events nearest after or
/// before a moment, across the cameras' recordings, looking at most seven days away.
/// </summary>
/// <param name="recordingService">The recordings in progress.</param>
/// <param name="settingsService">The application settings.</param>
/// <param name="cameraStorageService">The cameras.</param>
/// <param name="summaryCache">The summaries of the activity indexes already read.</param>
/// <param name="timeProvider">The clock: a recording in progress reaches now.</param>
public sealed class ListTimelineEventsHandler(
    IRecordingService recordingService,
    IApplicationSettingsService settingsService,
    ICameraStorageService cameraStorageService,
    ActivitySummaryCache summaryCache,
    TimeProvider timeProvider) : IListTimelineEventsHandler
{
    public Task<ListTimelineEventsResult> ExecuteAsync(
        ListTimelineEventsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.After is null == parameters.Before is null)
        {
            return Task.FromResult(ListTimelineEventsResult.BadRequest("Give after or before, not both and not neither."));
        }

        var forward = parameters.After is not null;
        var at = parameters.After ?? parameters.Before!.Value;
        var (from, to) = forward
            ? (at, at + TimelineRecordings.MaximumSpan)
            : (at - TimelineRecordings.MaximumSpan, at);

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

        var events = cameras
            .SelectMany(camera => TimelineBuilder.FindEvents(byCamera.GetValueOrDefault(camera.Id) ?? [])
                .Where(e => forward ? e.Start > at : e.Start < at)
                .Select(e => TimelineRecordings.ToApi(camera.Id, e)));
        var nearest = (forward ? events.OrderBy(e => e.Start) : events.OrderByDescending(e => e.Start))
            .Take(parameters.Limit ?? 1)
            .ToList();

        return Task.FromResult(ListTimelineEventsResult.Ok(nearest));
    }
}