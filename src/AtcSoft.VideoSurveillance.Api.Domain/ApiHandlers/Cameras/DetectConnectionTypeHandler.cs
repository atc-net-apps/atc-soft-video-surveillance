namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the DetectConnectionType operation. Probes the
/// camera with its stored credentials and returns the result without saving
/// it; the client decides whether to apply it (explicit detection).
/// </summary>
public sealed class DetectConnectionTypeHandler(
    ICameraStorageService storage,
    ICameraConnectionTypeDetector detector) : IDetectConnectionTypeHandler
{
    public async Task<DetectConnectionTypeResult> ExecuteAsync(
        DetectConnectionTypeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var camera = storage.GetCameraById(parameters.CameraId);
        if (camera is null)
        {
            return DetectConnectionTypeResult.NotFound($"Camera {parameters.CameraId} not found.");
        }

        // USB cameras need no probe: they are always USB.
        var detected = camera.Connection.Source == AtcSoft.VideoSurveillance.Enums.CameraSource.Usb
            ? AtcSoft.VideoSurveillance.Enums.CameraConnectionType.Usb
            : await detector
                .DetectAsync(
                    camera.Connection.IpAddress,
                    camera.Authentication.UserName,
                    camera.Authentication.Password,
                    cancellationToken)
                .ConfigureAwait(false);

        return DetectConnectionTypeResult.Ok(new ConnectionTypeDetection(detected.ToApiConnectionType()));
    }
}