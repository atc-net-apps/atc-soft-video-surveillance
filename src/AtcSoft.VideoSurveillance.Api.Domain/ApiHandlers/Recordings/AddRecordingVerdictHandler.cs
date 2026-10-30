namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Recordings;

/// <summary>
/// Handler business logic for the AddRecordingVerdict operation: adds a person's verdict on a
/// stretch of a recording to the review beside it.
/// </summary>
/// <param name="settingsService">The application settings, for the recordings root.</param>
/// <param name="recordingService">The recordings in progress, which cannot be reviewed yet.</param>
/// <param name="writer">Writes the review.</param>
public sealed class AddRecordingVerdictHandler(
    IApplicationSettingsService settingsService,
    IRecordingService recordingService,
    IRecordingReviewWriter writer) : IAddRecordingVerdictHandler
{
    public Task<AddRecordingVerdictResult> ExecuteAsync(
        AddRecordingVerdictParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;
        if (!RecordingPathHelper.TryResolveRecording(settingsService.Recording.RecordingPath, parameters.FilePath, out var videoPath))
        {
            return Task.FromResult(AddRecordingVerdictResult.BadRequest("The path does not name a recording."));
        }

        if (request.ToSeconds <= request.FromSeconds)
        {
            return Task.FromResult(AddRecordingVerdictResult.BadRequest("The stretch must end after it starts."));
        }

        if (request.Verdict == ReviewVerdict.FalseBox && request.Count is not null)
        {
            return Task.FromResult(AddRecordingVerdictResult.BadRequest("A false box has no count."));
        }

        if (!File.Exists(videoPath))
        {
            return Task.FromResult(AddRecordingVerdictResult.NotFound("There is no such recording."));
        }

        if (recordingService.GetActiveSessions().Any(s => string.Equals(s.CurrentFilePath, videoPath, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(AddRecordingVerdictResult.Conflict("The recording is still being made; review it when it ends."));
        }

        var span = new AtcSoft.VideoSurveillance.Helpers.Review.ReviewSpan
        {
            From = TimeSpan.FromSeconds(request.FromSeconds),
            To = TimeSpan.FromSeconds(request.ToSeconds),
            Verdict = ToVerdict(request.Verdict),
            Count = request.Count,
            Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim(),
        };
        try
        {
            writer.AddVerdict(videoPath, span);
        }
        catch (InvalidDataException ex)
        {
            return Task.FromResult(AddRecordingVerdictResult.Conflict($"The review beside the recording cannot be read, so it is not written over: {ex.Message}"));
        }

        return Task.FromResult(AddRecordingVerdictResult.NoContent());
    }

    private static string ToVerdict(ReviewVerdict verdict)
        => verdict switch
        {
            ReviewVerdict.FalseBox => AtcSoft.VideoSurveillance.Helpers.Review.ReviewVerdicts.FalseBox,
            ReviewVerdict.Missed => AtcSoft.VideoSurveillance.Helpers.Review.ReviewVerdicts.Missed,
            ReviewVerdict.WrongCount => AtcSoft.VideoSurveillance.Helpers.Review.ReviewVerdicts.WrongCount,
            _ => AtcSoft.VideoSurveillance.Helpers.Review.ReviewVerdicts.Correct,
        };
}