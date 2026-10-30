namespace AtcSoft.VideoSurveillance.Api.Webhooks;

public sealed partial class WebhookRegistry
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "The configured webhook receiver {Receiver} has no usable address and gets no events")]
    private partial void LogInvalidEndpoint(string receiver);

    [LoggerMessage(Level = LogLevel.Error, Message = "The webhook receivers could not be saved to {Path}")]
    private partial void LogSaveFailed(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The secret of webhook receiver {Receiver} cannot be read with this server's keys; enter it again")]
    private partial void LogSecretUnreadable(string receiver);

    [LoggerMessage(Level = LogLevel.Information, Message = "Encrypted {Count} webhook secret(s) kept in plain text in {Path}")]
    private partial void LogSecretsEncrypted(int count, string path);
}