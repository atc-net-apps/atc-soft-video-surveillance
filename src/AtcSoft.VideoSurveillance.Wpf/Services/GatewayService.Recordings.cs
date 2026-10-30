namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Gateway service - Recordings operations using generated endpoints.
/// CA1055 suppression applied at the type level via [SuppressMessage] on
/// the GatewayService.cs partial.
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
    /// <param name="filePath">The recording's path as the listing gives it.</param>
    /// <param name="buckets">How many stretches.</param>
    /// <param name="durationSeconds">How long the recording is, in seconds.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The activity, or <see langword="null"/> when the server did not give it.</returns>
    public async Task<RecordingActivity?> GetRecordingActivityAsync(
        string filePath,
        int buckets,
        double durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var parameters = new GetRecordingActivityParameters(FilePath: filePath, Buckets: buckets, DurationSeconds: durationSeconds);
        var result = await getRecordingActivityEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// Asks the server to analyse a recording without an activity index for one, in the background.
    /// </summary>
    /// <param name="filePath">The recording's path as the listing gives it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <exception cref="HttpRequestException">The server refused, in its own words.</exception>
    public async Task StartRecordingAnalysisAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var result = await startRecordingAnalysisEndpoint
            .ExecuteAsync(new StartRecordingAnalysisParameters(FilePath: filePath), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsAccepted)
        {
            throw Refused(result);
        }
    }

    /// <summary>
    /// Adds a verdict given on a recording's timeline to the review beside it on the server.
    /// </summary>
    /// <param name="filePath">The recording's path as the listing gives it.</param>
    /// <param name="verdict">The verdict and the stretch it is about.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <exception cref="HttpRequestException">The server refused, in its own words.</exception>
    public async Task AddRecordingVerdictAsync(
        string filePath,
        TimelineVerdict verdict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        var request = new AddRecordingVerdictRequest(verdict.FromSeconds, verdict.ToSeconds, new ReviewVerdict(verdict.Verdict), Count: null, Label: null);
        var result = await addRecordingVerdictEndpoint
            .ExecuteAsync(new AddRecordingVerdictParameters(FilePath: filePath, Request: request), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsNoContent)
        {
            throw Refused(result);
        }
    }

    /// <summary>
    /// Gets the saver of the verdicts given on a recording's timeline: the server writes them.
    /// </summary>
    /// <param name="filePath">The recording's path as the listing gives it.</param>
    public RecordingVerdictSaver VerdictSaver(string filePath)
        => (verdict, cancellationToken) => AddRecordingVerdictAsync(filePath, verdict, cancellationToken);

    /// <summary>
    /// Gets where a recording's preview pictures are; a recording without them is queued for them.
    /// </summary>
    /// <param name="filePath">The recording's path as the listing gives it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
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

    public string GetRecordingFileUrl(string filePath)
        => $"{ApiBaseUrl}/recordings-files/{AtcSoft.VideoSurveillance.Helpers.RecordingPathHelper.EscapePathSegments(filePath)}";
}