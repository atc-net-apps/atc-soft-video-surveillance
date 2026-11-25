namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// How reading a camera's ONVIF profiles went.
/// </summary>
public enum OnvifImportOutcome
{
    /// <summary>The profiles were read; the choice says which stream is which.</summary>
    Read = 0,

    /// <summary>Nothing ONVIF answered on the camera's ports.</summary>
    NotOnvif = 1,

    /// <summary>The camera refused the user and password.</summary>
    Unauthorized = 2,

    /// <summary>The camera did not answer in time.</summary>
    Timeout = 3,

    /// <summary>The camera answered, but offers no RTSP stream.</summary>
    NoRtspProfile = 4,
}