namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// What is played from a hub camera.
/// </summary>
public enum HubStreamKind
{
    /// <summary>
    /// Motion JPEG over HTTP: a picture at a time, no audio.
    /// </summary>
    Mjpeg,

    /// <summary>
    /// RTSP (H.264 or H.265 as a rule).
    /// </summary>
    Rtsp,
}