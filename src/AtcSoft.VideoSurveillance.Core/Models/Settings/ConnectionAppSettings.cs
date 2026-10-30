namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Application-level connection settings and defaults for new cameras.
/// </summary>
public class ConnectionAppSettings
{
    public CameraProtocol DefaultProtocol { get; set; } = CameraProtocol.Rtsp;

    public int DefaultPort { get; set; } = 554;

    public int ConnectionTimeoutSeconds { get; set; } = 10;

    public int ReconnectDelaySeconds { get; set; } = 10;

    public bool AutoReconnectOnFailure { get; set; } = true;

    public bool ShowNotificationOnDisconnect { get; set; } = true;

    public bool ShowNotificationOnReconnect { get; set; }

    public bool PlayNotificationSound { get; set; }

    /// <inheritdoc />
    public override string ToString()
        => $"ConnectionAppSettings {{ Protocol={DefaultProtocol}, Port={DefaultPort.ToString(CultureInfo.InvariantCulture)}, Timeout={ConnectionTimeoutSeconds.ToString(CultureInfo.InvariantCulture)}s }}";
}