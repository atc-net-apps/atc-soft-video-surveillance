namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// A <see cref="GatewayService"/> on the real generated endpoints and Atc.Rest.Client,
/// whose server answers
/// every request the same way.
/// </summary>
internal sealed class GatewayTestServer : IDisposable
{
    private readonly ServiceProvider services;

    public GatewayTestServer(
        Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var collection = new ServiceCollection();
        collection
            .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://server.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new AnswerHandler(answer));
        collection.AddVideoSurveillanceEndpoints();
        services = collection.BuildServiceProvider();
        Gateway = ActivatorUtilities.CreateInstance<GatewayService>(services);
    }

    public GatewayService Gateway { get; }

    public T Get<T>()
        where T : notnull
        => services.GetRequiredService<T>();

    public static GatewayTestServer Answering(
        System.Net.HttpStatusCode status,
        string problemJson)
        => new(request => new HttpResponseMessage(status)
        {
            RequestMessage = request,
            Content = new StringContent(problemJson, System.Text.Encoding.UTF8, "application/problem+json"),
        });

    public void Dispose()
        => services.Dispose();

    private sealed class AnswerHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(answer(request));
    }
}