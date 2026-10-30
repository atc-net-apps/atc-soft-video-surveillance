namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the RenewStreamLease operation: a viewer still watching.
/// </summary>
public sealed class RenewStreamLeaseHandler(ILiveStreamService streams) : IRenewStreamLeaseHandler
{
    public Task<RenewStreamLeaseResult> ExecuteAsync(
        RenewStreamLeaseParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return Task.FromResult(streams.Renew(parameters.CameraId, parameters.LeaseId)
            ? RenewStreamLeaseResult.NoContent()
            : RenewStreamLeaseResult.NotFound($"Lease {parameters.LeaseId} on camera {parameters.CameraId} not found; start the stream again."));
    }
}