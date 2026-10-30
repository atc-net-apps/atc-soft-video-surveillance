namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// Keeps the requests for a stream's files and for recordings out of the log:
/// their addresses carry a stream lease or a media token, and a player asks for
/// a segment every second or two.
/// </summary>
public static class MediaRequestLogFilter
{
    public static bool IsMediaRequest(Serilog.Events.LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        return logEvent.Properties.TryGetValue("RequestPath", out var value) &&
               value is Serilog.Events.ScalarValue { Value: string path } &&
               (path.StartsWith("/streams/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/recordings-files/", StringComparison.OrdinalIgnoreCase));
    }
}