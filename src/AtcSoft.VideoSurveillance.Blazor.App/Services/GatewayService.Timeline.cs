namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - Timeline operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// Gets every camera's timeline over a span.
    /// </summary>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span, at most seven days later.</param>
    /// <param name="buckets">How many stretches.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The timeline, or <see langword="null"/> when the server did not give it.</returns>
    public async Task<Timeline?> GetTimelineAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int buckets,
        CancellationToken cancellationToken = default)
    {
        var parameters = new GetTimelineParameters(From: from, To: to, CameraId: null, Buckets: buckets);
        var result = await getTimelineEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// Finds the event nearest after or before a moment, across every camera.
    /// </summary>
    /// <param name="at">The moment.</param>
    /// <param name="forward">Whether to look after it; before it when <see langword="false"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The event, or <see langword="null"/> when none was found within a week.</returns>
    public async Task<TimelineEvent?> FindTimelineEventAsync(
        DateTimeOffset at,
        bool forward,
        CancellationToken cancellationToken = default)
    {
        var parameters = new ListTimelineEventsParameters(
            After: forward ? at : null,
            Before: forward ? null : at,
            CameraId: null,
            Limit: 1);
        var result = await listTimelineEventsEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.FirstOrDefault()
            : null;
    }
}