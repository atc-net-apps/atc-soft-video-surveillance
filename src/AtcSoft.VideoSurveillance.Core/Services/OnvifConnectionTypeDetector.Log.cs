namespace AtcSoft.VideoSurveillance.Services;

public sealed partial class OnvifConnectionTypeDetector
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "ONVIF connection-type detection for {IpAddress}:{Port} returned {ConnectionType}")]
    private partial void LogDetected(string ipAddress, int port, CameraConnectionType connectionType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ONVIF {Operation} on {Uri} returned HTTP {StatusCode}")]
    private partial void LogRequestRejected(string operation, Uri uri, int statusCode);

    // A camera answers ONVIF on one port at most, so most probes end this
    // way. One line each, without a stack trace.
    [LoggerMessage(Level = LogLevel.Debug, Message = "ONVIF {Operation} on {Uri} got no answer")]
    private partial void LogRequestTimedOut(string operation, Uri uri);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ONVIF {Operation} on {Uri} failed: {Reason}")]
    private partial void LogRequestFailed(string operation, Uri uri, string reason);
}