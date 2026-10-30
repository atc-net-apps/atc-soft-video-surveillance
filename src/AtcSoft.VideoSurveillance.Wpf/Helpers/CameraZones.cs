namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

using CoreMotionZone = AtcSoft.VideoSurveillance.Models.MotionZone;

/// <summary>
/// A camera's motion zones as the server has them now, for the colours of a recording's timeline.
/// </summary>
public static class CameraZones
{
    /// <summary>
    /// Gets a camera's zones; none when the camera is gone or the server does not answer.
    /// </summary>
    /// <param name="gateway">The server.</param>
    /// <param name="cameraId">The camera.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<IReadOnlyList<CoreMotionZone>> LoadAsync(
        GatewayService gateway,
        Guid cameraId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gateway);

        try
        {
            var camera = await gateway.GetCameraByIdAsync(cameraId, cancellationToken).ConfigureAwait(false);
            return camera is null ? [] : [.. (camera.MotionZones ?? []).Select(z => z.ToCoreZone())];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}