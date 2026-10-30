namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the DeleteStreamLease operation: a viewer stopped watching.
/// </summary>
public sealed class DeleteStreamLeaseHandler(ILiveStreamService streams) : IDeleteStreamLeaseHandler
{
    public Task<DeleteStreamLeaseResult> ExecuteAsync(
        DeleteStreamLeaseParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        streams.Release(parameters.CameraId, parameters.LeaseId);
        return Task.FromResult(DeleteStreamLeaseResult.NoContent());
    }
}