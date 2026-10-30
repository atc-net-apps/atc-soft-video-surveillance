namespace AtcSoft.VideoSurveillance.Wpf.Extensions;

/// <summary>
/// The resilience is the one the specification declares (x-retry-*), run
/// through the generated endpoints, which tell it how to send each call.
/// </summary>
public sealed class ApiResilienceTests
{
    [Fact]
    public async Task A_Start_That_Fails_Is_Sent_Once()
    {
        using var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable);
        await using var services = BuildServices(handler);

        await services.GetRequiredService<IStartRecordingEndpoint>().ExecuteAsync(
            new StartRecordingParameters(Guid.NewGuid()),
            cancellationToken: TestContext.Current.CancellationToken);

        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_Save_That_Fails_Is_Sent_Once()
    {
        using var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable);
        await using var services = BuildServices(handler);

        await services.GetRequiredService<IUpdateLayoutEndpoint>().ExecuteAsync(
            new UpdateLayoutParameters(Guid.NewGuid(), new UpdateLayoutRequest(Name: "Wall")),
            cancellationToken: TestContext.Current.CancellationToken);

        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_Delete_That_Fails_Is_Sent_Once()
    {
        using var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable);
        await using var services = BuildServices(handler);

        await services.GetRequiredService<IDeleteCameraEndpoint>().ExecuteAsync(
            new DeleteCameraParameters(Guid.NewGuid()),
            cancellationToken: TestContext.Current.CancellationToken);

        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_Read_That_Fails_Is_Tried_Again()
    {
        using var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        await using var services = BuildServices(handler);

        var result = await services.GetRequiredService<IListCamerasEndpoint>().ExecuteAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsOk.Should().BeTrue();
        handler.Calls.Should().Be(2);
    }

    private static ServiceProvider BuildServices(CountingHandler handler)
    {
        var collection = new ServiceCollection();
        collection
            .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://server.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddApiResilience();
        collection.AddVideoSurveillanceEndpoints();
        return collection.BuildServiceProvider();
    }

    private sealed class CountingHandler(
        params HttpStatusCode[] answers) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var status = answers[Math.Min(Calls, answers.Length - 1)];
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.OK ? "[]" : """{"status":503}""", System.Text.Encoding.UTF8, status == HttpStatusCode.OK ? "application/json" : "application/problem+json"),
                RequestMessage = request,
            });
        }
    }
}