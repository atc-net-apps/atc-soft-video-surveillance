namespace AtcSoft.VideoSurveillance.Wpf.Services;

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

    public async Task<Camera> CreateCameraAsync(
        CreateCameraRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateCameraParameters(Request: request);
        var result = await createCameraEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? result.CreatedContent
            : throw Refused(result);
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

    public async Task<Camera> UpdateCameraAsync(
        Guid cameraId,
        UpdateCameraRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UpdateCameraParameters(CameraId: cameraId, Request: request);
        var result = await updateCameraEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : throw Refused(result);
    }

    public async Task DeleteCameraAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DeleteCameraParameters(CameraId: cameraId);
        var result = await deleteCameraEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsNoContent)
        {
            throw Refused(result);
        }
    }

    /// <inheritdoc />
    public async Task<StreamLease> StartStreamAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new StartStreamParameters(CameraId: cameraId);
        var result = await startStreamEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : throw Refused(result);
    }

    /// <inheritdoc />
    public async Task<StreamLeaseRenewal> RenewStreamLeaseAsync(
        Guid cameraId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        try
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return StreamLeaseRenewal.Unreachable;
        }
    }

    /// <inheritdoc />
    public async Task StopStreamAsync(
        Guid cameraId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new DeleteStreamLeaseParameters(CameraId: cameraId, LeaseId: leaseId);
            await deleteStreamLeaseEndpoint
                .ExecuteAsync(parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The lease runs out by itself.
        }
    }

    public async Task<RecordingStatus> StartRecordingAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new StartRecordingParameters(CameraId: cameraId);
        var result = await startRecordingEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : throw Refused(result);
    }

    public async Task<RecordingStatus> StopRecordingAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new StopRecordingParameters(CameraId: cameraId);
        var result = await stopRecordingEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : throw Refused(result);
    }

    /// <summary>
    /// Gets a PNG of the camera's picture now, or <see langword="null"/> when
    /// the camera is unknown or gives no picture.
    /// </summary>
    public async Task<byte[]?> CaptureSnapshotAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CaptureSnapshotParameters(CameraId: cameraId);
        var result = await captureSnapshotEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.Content
            : null;
    }
}