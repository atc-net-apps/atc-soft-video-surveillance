namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Recordings;

/// <summary>
/// Handler business logic for the DeleteRecording operation.
/// </summary>
/// <param name="recordingService">The recordings in progress.</param>
/// <param name="settingsService">The application settings.</param>
public sealed class DeleteRecordingHandler(
    IRecordingService recordingService,
    IApplicationSettingsService settingsService) : IDeleteRecordingHandler
{
    public Task<DeleteRecordingResult> ExecuteAsync(
        DeleteRecordingParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var activePaths = new HashSet<string>(
            recordingService.GetActiveSessions().Select(s => Path.GetFullPath(s.CurrentFilePath)),
            StringComparer.OrdinalIgnoreCase);

        var outcome = RecordingRemover.Remove(
            settingsService.Recording.RecordingPath,
            parameters.FilePath,
            activePaths);

        return Task.FromResult(outcome switch
        {
            RecordingRemovalOutcome.Removed => DeleteRecordingResult.NoContent(),
            RecordingRemovalOutcome.InProgress => DeleteRecordingResult.Conflict(
                "This recording is still being made. Stop the recording, or wait for it to end, and delete it then."),
            RecordingRemovalOutcome.NotFound => DeleteRecordingResult.NotFound("There is no such recording."),
            _ => DeleteRecordingResult.BadRequest("The path does not name a recording."),
        });
    }
}