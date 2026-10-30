namespace AtcSoft.VideoSurveillance.Api.Webhooks;

public sealed partial class WebhookSender
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Webhook '{EventType}' delivered to {Endpoint} (attempt {Attempt})")]
    private partial void LogDelivered(string eventType, string endpoint, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook '{EventType}' to {Endpoint} failed with status {Status}; it will be tried again")]
    private partial void LogAttemptFailed(string eventType, string endpoint, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook '{EventType}' could not reach {Endpoint} ({Failure}); it will be tried again")]
    private partial void LogAttemptUnreachable(string eventType, string endpoint, string failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook '{EventType}' was refused by {Endpoint} with status {Status}; it is not tried again")]
    private partial void LogRefused(string eventType, string endpoint, int status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Webhook '{EventType}' to {Endpoint} was given up on after {Attempts} attempts")]
    private partial void LogGivenUp(string eventType, string endpoint, int attempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook endpoint {Endpoint} is not an http or https address; nothing is sent to it")]
    private partial void LogInvalidEndpoint(string endpoint);
}