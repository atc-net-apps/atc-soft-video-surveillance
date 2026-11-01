namespace AtcSoft.Video.Surveillance.Api.Webhooks;

public sealed partial class WebhookDispatcherService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook dispatcher started with {Count} endpoint(s)")]
    private partial void LogStarted(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook endpoint {Endpoint} is not an http or https address and is ignored")]
    private partial void LogInvalidEndpoint(string endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The webhook queue of {Endpoint} is full; {Count} of its oldest event(s) were dropped")]
    private partial void LogDropped(int count, string endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook receiver {Endpoint} failed {Failures} events in a row; it is left alone for {Seconds} seconds, its events wait")]
    private partial void LogReceiverPaused(string endpoint, int failures, int seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook '{EventType}' to {Endpoint} failed unexpectedly ({Failure})")]
    private partial void LogSendFailed(string eventType, string endpoint, string failure);
}