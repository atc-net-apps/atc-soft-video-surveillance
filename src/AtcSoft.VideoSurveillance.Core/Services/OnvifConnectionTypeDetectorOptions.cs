namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Options for <see cref="OnvifConnectionTypeDetector"/>.
/// </summary>
public sealed class OnvifConnectionTypeDetectorOptions
{
    /// <summary>
    /// Gets or sets the HTTP ports probed for the ONVIF Device service, in
    /// order. The ONVIF port is usually not the RTSP port: 80 is the common
    /// default, 8080 / 8000 are frequent alternatives and 2020 is used by
    /// TP-Link Tapo cameras.
    /// </summary>
    public IReadOnlyList<int> Ports { get; set; } = [80, 8080, 8000, 2020];

    /// <summary>
    /// Gets or sets the path of the ONVIF Device service.
    /// </summary>
    public string DeviceServicePath { get; set; } = "/onvif/device_service";

    /// <summary>
    /// Gets or sets the timeout of a single request.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the upper bound for a whole detection, across all ports.
    /// </summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(6);
}