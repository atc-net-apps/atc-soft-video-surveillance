namespace AtcSoft.VideoSurveillance.Wpf.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="CameraProtocol"/>.
/// </summary>
public static class CameraProtocolExtensions
{
    /// <summary>
    /// Gets the URI scheme for the specified camera protocol.
    /// </summary>
    /// <param name="protocol">The camera protocol.</param>
    /// <returns>The lowercase URI scheme string (e.g., "rtsp", "http", "https").</returns>
    public static string ToScheme(this CameraProtocol protocol)
        => protocol switch
        {
            CameraProtocol.Rtsp => CameraProtocol.Rtsp.ToStringLowerCase(),
            CameraProtocol.Http => CameraProtocol.Http.ToStringLowerCase(),
            CameraProtocol.Https => CameraProtocol.Https.ToStringLowerCase(),
            _ => CameraProtocol.Rtsp.ToStringLowerCase(),
        };
}