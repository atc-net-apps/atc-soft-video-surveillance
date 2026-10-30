namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Recordings;

/// <summary>
/// Handler business logic for the StartRecordingAnalysis operation: queues a recording without an
/// activity index to be analysed for one.
/// </summary>
/// <param name="settingsService">The application settings, for the recordings root.</param>
/// <param name="recordingService">The recordings in progress, which are not analysed this way.</param>
/// <param name="queue">The recordings waiting to be analysed.</param>
public sealed class StartRecordingAnalysisHandler(
    IApplicationSettingsService settingsService,
    IRecordingService recordingService,
    IRecordingAnalyses queue) : IStartRecordingAnalysisHandler
{
    public Task<StartRecordingAnalysisResult> ExecuteAsync(
        StartRecordingAnalysisParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (!RecordingPathHelper.TryResolveRecording(settingsService.Recording.RecordingPath, parameters.FilePath, out var videoPath))
        {
            return Task.FromResult(StartRecordingAnalysisResult.BadRequest("The path does not name a recording."));
        }

        if (!File.Exists(videoPath))
        {
            return Task.FromResult(StartRecordingAnalysisResult.NotFound("There is no such recording."));
        }

        if (File.Exists(RecordingPathHelper.BuildActivityPath(videoPath)))
        {
            return Task.FromResult(StartRecordingAnalysisResult.Conflict("The recording has an activity index already."));
        }

        if (recordingService.GetActiveSessions().Any(s => string.Equals(s.CurrentFilePath, videoPath, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(StartRecordingAnalysisResult.Conflict("The recording is still being made; it gets its index as it is recorded."));
        }

        return Task.FromResult(queue.TryEnqueue(videoPath)
            ? StartRecordingAnalysisResult.Accepted()
            : StartRecordingAnalysisResult.Conflict("The recording is being analysed already."));
    }
}