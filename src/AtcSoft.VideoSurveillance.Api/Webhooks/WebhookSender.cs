namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// Posts events as JSON. A receiver that answers with a server error, is
/// busy or cannot be reached is tried a few times; one that refuses the
/// request is not, since the same request gets the same answer.
/// </summary>
public sealed partial class WebhookSender : IWebhookSender
{
    /// <summary>
    /// The name of the HTTP client the sender uses.
    /// </summary>
    public const string HttpClientName = "Webhooks";

    /// <summary>
    /// How many times a delivery is attempted before it is given up on.
    /// </summary>
    public const int MaxAttempts = 3;

    private const string EventHeader = "X-AtcSoft-Event";
    private const string DeliveryHeader = "X-AtcSoft-Delivery";
    private const string SignatureHeader = "X-AtcSoft-Signature";

    // The body is the specification's WebhookPayload; a field without a value is left out.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<WebhookSender> logger;

    public WebhookSender(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookSender> logger)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets or sets the pause before the second attempt; it doubles for each one after.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<bool> SendAsync(
        WebhookEndpoint endpoint,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(webhookEvent);

        if (!endpoint.IsValid)
        {
            LogInvalidEndpoint(endpoint.LogName);
            return false;
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(
            new WebhookPayload(
                Event: webhookEvent.Type,
                CameraId: webhookEvent.CameraId,
                CameraName: webhookEvent.CameraName,
                Timestamp: webhookEvent.Timestamp,
                Data: webhookEvent.Data),
            JsonOptions);

        // The same for every attempt, so a receiver can drop a repeat.
        var deliveryId = Guid.NewGuid().ToString("N");

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(RetryDelay * Math.Pow(2, attempt - 2), cancellationToken).ConfigureAwait(false);
            }

            var outcome = await TrySendAsync(endpoint, webhookEvent.Type, deliveryId, body, cancellationToken).ConfigureAwait(false);
            switch (outcome)
            {
                case Outcome.Delivered:
                    LogDelivered(webhookEvent.Type, endpoint.LogName, attempt);
                    return true;
                case Outcome.Refused:
                    return false;
            }
        }

        LogGivenUp(webhookEvent.Type, endpoint.LogName, MaxAttempts);
        return false;
    }

    private async Task<Outcome> TrySendAsync(
        WebhookEndpoint endpoint,
        string eventType,
        string deliveryId,
        byte[] body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint.Url));
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            request.Headers.TryAddWithoutValidation(EventHeader, eventType);
            request.Headers.TryAddWithoutValidation(DeliveryHeader, deliveryId);
            if (!string.IsNullOrEmpty(endpoint.Secret))
            {
                request.Headers.TryAddWithoutValidation(SignatureHeader, WebhookSignature.Compute(endpoint.Secret, body));
            }

            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return Outcome.Delivered;
            }

            var status = (int)response.StatusCode;
            if (status >= 500 || status is 408 or 429)
            {
                LogAttemptFailed(eventType, endpoint.LogName, status);
                return Outcome.TryAgain;
            }

            LogRefused(eventType, endpoint.LogName, status);
            return Outcome.Refused;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            // Only the kind of failure: the message may repeat the address.
            LogAttemptUnreachable(eventType, endpoint.LogName, ex.GetType().Name);
            return Outcome.TryAgain;
        }
    }

    private enum Outcome
    {
        Delivered,
        TryAgain,
        Refused,
    }
}