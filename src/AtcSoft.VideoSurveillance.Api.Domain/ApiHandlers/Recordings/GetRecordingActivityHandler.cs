namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Recordings;

/// <summary>
/// Handler business logic for the GetRecordingActivity operation: the recording's activity
/// index, shrunk to the timeline's width by keeping the most of each stretch.
/// </summary>
/// <param name="settingsService">The application settings.</param>
/// <param name="analyses">The recordings queued or being analysed, so a client waiting for one can tell a failure.</param>
public sealed class GetRecordingActivityHandler(
    IApplicationSettingsService settingsService,
    IRecordingAnalyses? analyses = null) : IGetRecordingActivityHandler
{
    /// <summary>
    /// How many stretches when the caller does not say: the spec's default.
    /// </summary>
    public const int DefaultBuckets = 500;

    public Task<GetRecordingActivityResult> ExecuteAsync(
        GetRecordingActivityParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (!RecordingPathHelper.TryResolveRecording(settingsService.Recording.RecordingPath, parameters.FilePath, out var videoPath))
        {
            return Task.FromResult(GetRecordingActivityResult.BadRequest("The path does not name a recording."));
        }

        if (!File.Exists(videoPath))
        {
            return Task.FromResult(GetRecordingActivityResult.NotFound("There is no such recording."));
        }

        // A recording from before the index, or one the index could not be written for.
        if (ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(videoPath)) is not { } log)
        {
            return Task.FromResult(GetRecordingActivityResult.Ok(
                new RecordingActivity(HasIndex: false, IsBeingAnalysed: analyses?.Contains(videoPath) == true, DurationSeconds: 0, Buckets: [], StartedAt: null)));
        }

        var duration = parameters.DurationSeconds is { } given and > 0 ? given : log.DurationSeconds;
        var buckets = ActivityDownsampler.ByMax(log, duration, parameters.Buckets ?? DefaultBuckets);

        return Task.FromResult(GetRecordingActivityResult.Ok(new RecordingActivity(
            HasIndex: true,
            IsBeingAnalysed: false,
            DurationSeconds: duration,
            Buckets: [.. buckets.Select(b => new RecordingActivityBucket(Level: b.Level, Tracks: b.Tracks, IsAnalysed: b.IsAnalysed, Zones: b.Zones.Count > 0 ? [.. b.Zones] : null))],
            StartedAt: log.Start)));
    }
}