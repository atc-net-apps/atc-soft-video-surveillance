namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - Cameras operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    public async Task<Camera[]?> GetCamerasAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await listCamerasEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }

    public async Task<GatewayResult<Camera>> CreateCameraAsync(
        CreateCameraRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateCameraParameters(Request: request);
        var result = await createCameraEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? GatewayResult.Success(result.CreatedContent)
            : GatewayResult.Refused<Camera>(result);
    }

    public async Task<Camera?> GetCameraByIdAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new GetCameraByIdParameters(CameraId: cameraId);
        var result = await getCameraByIdEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// Asks the server to probe the camera (ONVIF) with its stored credentials.
    /// Returns the detected type without saving it, or <see langword="null"/>
    /// when the request failed.
    /// </summary>
    public async Task<CameraConnectionType?> DetectConnectionTypeAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DetectConnectionTypeParameters(CameraId: cameraId);
        var result = await detectConnectionTypeEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ConnectionType
            : null;
    }

    public async Task<GatewayResult<Camera>> UpdateCameraAsync(
        Guid cameraId,
        UpdateCameraRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UpdateCameraParameters(CameraId: cameraId, Request: request);
        var result = await updateCameraEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<Camera>(result);
    }

    public async Task<GatewayResult> DeleteCameraAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DeleteCameraParameters(CameraId: cameraId);
        var result = await deleteCameraEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsNoContent
            ? GatewayResult.Done
            : GatewayResult.Refused(result);
    }

    public async Task<GatewayResult<RecordingStatus>> StartRecordingAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new StartRecordingParameters(CameraId: cameraId);
        var result = await startRecordingEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<RecordingStatus>(result);
    }

    public async Task<GatewayResult<RecordingStatus>> StopRecordingAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new StopRecordingParameters(CameraId: cameraId);
        var result = await stopRecordingEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<RecordingStatus>(result);
    }

    /// <summary>
    /// Takes a lease on a camera's live stream; answered when it can be played.
    /// </summary>
    public async Task<GatewayResult<StreamLease>> StartStreamAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new StartStreamParameters(CameraId: cameraId);
        var result = await startStreamEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<StreamLease>(result);
    }

    /// <summary>
    /// Renews a stream lease. Gone means the stream is to be started again;
    /// a server that is away keeps the lease for the next time.
    /// </summary>
    public async Task<StreamLeaseRenewal> RenewStreamLeaseAsync(
        Guid cameraId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new RenewStreamLeaseParameters(CameraId: cameraId, LeaseId: leaseId);
        var result = await renewStreamLeaseEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsNoContent)
        {
            return StreamLeaseRenewal.Renewed;
        }

        return result.IsNotFound
            ? StreamLeaseRenewal.Gone
            : StreamLeaseRenewal.Unreachable;
    }

    /// <summary>
    /// Gives a stream lease back. A server that is away lets it run out by itself.
    /// </summary>
    public Task StopStreamAsync(
        Guid cameraId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
        => deleteStreamLeaseEndpoint.ExecuteAsync(
            new DeleteStreamLeaseParameters(CameraId: cameraId, LeaseId: leaseId),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Gets a PNG of the camera's picture now, or <see langword="null"/> when
    /// the camera is unknown, gives no picture or the server cannot be reached.
    /// </summary>
    public async Task<byte[]?> CaptureSnapshotAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CaptureSnapshotParameters(CameraId: cameraId);
        try
        {
            var result = await captureSnapshotEndpoint
                .ExecuteAsync(parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result.IsOk
                ? result.OkContent.Content
                : null;
        }
        catch (Exception ex) when (ApiTransportFailure.IsTransportFailure(ex, cancellationToken))
        {
            // A picture is a download, which "transportFailure": "result" does not cover.
            return null;
        }
    }
}