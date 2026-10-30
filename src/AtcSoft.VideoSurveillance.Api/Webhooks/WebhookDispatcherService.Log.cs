namespace AtcSoft.VideoSurveillance.Api.Webhooks;

public sealed partial class WebhookDispatcherService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook dispatcher started with {Count} endpoint(s)")]
    private partial void LogStarted(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook endpoint {Endpoint} is not an http or https address and is ignored")]
    private partial void LogInvalidEndpoint(string endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The webhook queue is full; {Count} of the oldest event(s) were dropped")]
    private partial void LogDropped(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook '{EventType}' to {Endpoint} failed unexpectedly ({Failure})")]
    private partial void LogSendFailed(string eventType, string endpoint, string failure);
}