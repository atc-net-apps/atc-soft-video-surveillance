namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Specifies the protocol used for camera streaming.
/// </summary>
public enum CameraProtocol
{
    /// <summary>
    /// Real Time Streaming Protocol (default for IP cameras).
    /// </summary>
    Rtsp,

    /// <summary>
    /// HTTP streaming protocol.
    /// </summary>
    Http,

    /// <summary>
    /// HTTPS streaming protocol (secure HTTP).
    /// </summary>
    Https,
}