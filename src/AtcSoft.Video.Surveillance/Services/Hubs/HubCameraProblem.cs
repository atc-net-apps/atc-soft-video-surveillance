namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// Why a hub camera cannot be added as it is.
/// </summary>
public enum HubCameraProblem
{
    /// <summary>
    /// It can be added.
    /// </summary>
    None,

    /// <summary>
    /// The camera is switched off on the hub.
    /// </summary>
    Disabled,

    /// <summary>
    /// Its stream is served to the hub's own machine only (motionEye: *Video Streaming* off).
    /// </summary>
    StreamingOff,

    /// <summary>
    /// It is a camera of another hub, shown on this one: add it from that hub.
    /// </summary>
    OnAnotherHub,
}