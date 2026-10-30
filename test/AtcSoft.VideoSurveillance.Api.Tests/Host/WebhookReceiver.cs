namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// A small web server that plays the receiving end of the server's
/// webhooks and keeps what it was sent.
/// </summary>
public sealed class WebhookReceiver : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly ConcurrentQueue<ReceivedWebhook> received = new();

    public WebhookReceiver()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        app = builder.Build();
        app.MapPost("/hook", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
            received.Enqueue(new ReceivedWebhook(
                request.Headers["X-AtcSoft-Event"].ToString(),
                request.Headers["X-AtcSoft-Signature"].ToString(),
                body));
            return Results.Ok();
        });

        app.StartAsync().GetAwaiter().GetResult();
        Address = app.Urls.First().TrimEnd('/') + "/hook";
    }

    /// <summary>
    /// Gets the address the server is told to post to.
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// Gets what has arrived so far.
    /// </summary>
    public IReadOnlyCollection<ReceivedWebhook> Received => [.. received];

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
}