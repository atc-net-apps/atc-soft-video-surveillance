namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// Makes a camera of a hub camera: a network camera on the hub's stream, named as on the hub,
/// with the hub as its origin. An MJPEG stream records transcoded to H.264 (MJPEG is several
/// times the size), unless the camera already says otherwise.
/// </summary>
public static class HubCameraSetup
{
    /// <summary>
    /// Fills the camera from the hub camera; the camera's other settings stay as they are.
    /// </summary>
    /// <param name="camera">The camera being added.</param>
    /// <param name="listing">The hub's listing the camera is from.</param>
    /// <param name="hubCamera">The camera on the hub.</param>
    /// <returns>Whether the camera could be filled (the hub camera has a stream).</returns>
    public static bool ApplyTo(
        CameraConfiguration camera,
        CameraHubListing listing,
        HubCamera hubCamera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(hubCamera);

        if (hubCamera.Stream is not { } stream || hubCamera.Problem != HubCameraProblem.None)
        {
            return false;
        }

        camera.Connection.Source = CameraSource.Network;
        camera.Connection.Protocol = hubCamera.StreamKind == HubStreamKind.Rtsp
            ? CameraProtocol.Rtsp
            : string.Equals(stream.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? CameraProtocol.Https
                : CameraProtocol.Http;
        camera.Connection.IpAddress = stream.Host;
        camera.Connection.Port = stream.Port;
        camera.Connection.Path = stream.PathAndQuery == "/" ? null : stream.PathAndQuery.TrimStart('/');
        camera.Connection.Usb = null;
        camera.Connection.Hub = new CameraHubOrigin
        {
            Kind = listing.Kind,
            Address = listing.Address.ToString(),
            CameraId = hubCamera.Id,
            CameraName = hubCamera.Name,
        };

        if (hubCamera.StreamUserName is not null)
        {
            camera.Authentication.UserName = hubCamera.StreamUserName;
            camera.Authentication.Password = hubCamera.StreamPassword;
        }

        if (!string.IsNullOrWhiteSpace(hubCamera.Name))
        {
            camera.Display.DisplayName = hubCamera.Name;
        }

        if (hubCamera.StreamKind == HubStreamKind.Mjpeg)
        {
            camera.Overrides.Recording.TranscodeVideoCodec ??= VideoTranscodeCodec.H264;
        }

        return true;
    }
}