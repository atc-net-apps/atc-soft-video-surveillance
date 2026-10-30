namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - Recordings operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    public async Task<Recording[]?> GetRecordingsAsync(
        Guid? cameraId = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new ListRecordingsParameters(CameraId: cameraId);
        var result = await listRecordingsEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }

    /// <summary>
    /// Gets a recording's activity for its timeline.
    /// </summary>
    /// <param name="filePath">The recording's path, as the listing gave it.</param>
    /// <param name="buckets">How many stretches.</param>
    /// <param name="durationSeconds">How long the recording is, in seconds; 0 when not known.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The activity, or <see langword="null"/> when the server did not give it.</returns>
    public async Task<RecordingActivity?> GetRecordingActivityAsync(
        string filePath,
        int buckets,
        double durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var parameters = new GetRecordingActivityParameters(
            FilePath: filePath,
            Buckets: buckets,
            DurationSeconds: durationSeconds > 0 ? durationSeconds : null);
        var result = await getRecordingActivityEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// Gets where a recording's preview pictures are; a recording without them is queued for them.
    /// </summary>
    /// <param name="filePath">The recording's path, as the listing gave it.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The previews, or <see langword="null"/> when the server did not give them.</returns>
    public async Task<RecordingPreviewSheet?> GetRecordingPreviewsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var result = await getRecordingPreviewsEndpoint
            .ExecuteAsync(new GetRecordingPreviewsParameters(FilePath: filePath), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// Asks the server to analyse a recording without an activity index for one, in the background.
    /// </summary>
    /// <param name="filePath">The recording's path, as the listing gave it.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Queued, or the server's reason.</returns>
    public async Task<GatewayResult> StartRecordingAnalysisAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var result = await startRecordingAnalysisEndpoint
            .ExecuteAsync(new StartRecordingAnalysisParameters(FilePath: filePath), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsAccepted
            ? GatewayResult.Done
            : GatewayResult.Refused(result);
    }

    /// <summary>
    /// Adds a verdict given on a recording's timeline to the review beside it on the server.
    /// </summary>
    /// <param name="filePath">The recording's path, as the listing gave it.</param>
    /// <param name="verdict">The verdict and the stretch it is about.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Saved, or the server's reason.</returns>
    public async Task<GatewayResult> AddRecordingVerdictAsync(
        string filePath,
        TimelineVerdict verdict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        var request = new AddRecordingVerdictRequest(verdict.FromSeconds, verdict.ToSeconds, new ReviewVerdict(verdict.Verdict), Count: null, Label: null);
        var result = await addRecordingVerdictEndpoint
            .ExecuteAsync(new AddRecordingVerdictParameters(FilePath: filePath, Request: request), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsNoContent
            ? GatewayResult.Done
            : GatewayResult.Refused(result);
    }

    /// <summary>
    /// Deletes a finished recording on the server.
    /// </summary>
    /// <param name="filePath">The recording's path, as the listing gave it.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<RecordingDeletionOutcome> DeleteRecordingAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DeleteRecordingParameters(FilePath: filePath);
        var result = await deleteRecordingEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsNoContent)
        {
            return RecordingDeletionOutcome.Deleted;
        }

        if (result.IsConflict)
        {
            return RecordingDeletionOutcome.InProgress;
        }

        return result.IsNotFound
            ? RecordingDeletionOutcome.NotFound
            : RecordingDeletionOutcome.Failed;
    }
}