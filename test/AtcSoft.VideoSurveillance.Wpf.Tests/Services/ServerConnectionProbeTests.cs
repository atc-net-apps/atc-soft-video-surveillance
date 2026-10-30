namespace AtcSoft.VideoSurveillance.Wpf.Services;

public sealed class ServerConnectionProbeTests
{
    [Theory]
    [InlineData("http://server.test:5000", "http://server.test:5000/cameras")]
    [InlineData("http://server.test:5000/", "http://server.test:5000/cameras")]
    public async Task The_Probe_Asks_For_The_Camera_List_The_Server_Serves(
        string server,
        string expected)
    {
        using var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);

        var status = await ServerConnectionProbe.TestAsync(httpClient, new Uri(server), TestContext.Current.CancellationToken);

        handler.RequestedUri.Should().Be(new Uri(expected));
        status.Should().Be("Connected (200)");
    }

    [Fact]
    public async Task A_Server_That_Answers_With_An_Error_Is_A_Failure_With_The_Status()
    {
        using var handler = new RecordingHandler(HttpStatusCode.NotFound);
        using var httpClient = new HttpClient(handler);

        var status = await ServerConnectionProbe.TestAsync(httpClient, new Uri("http://server.test"), TestContext.Current.CancellationToken);

        status.Should().Be("Failed: 404 Not Found");
    }

    [Fact]
    public async Task A_Server_That_Cannot_Be_Reached_Is_A_Failure_With_The_Reason()
    {
        using var handler = new RecordingHandler(new HttpRequestException("No connection could be made"));
        using var httpClient = new HttpClient(handler);

        var status = await ServerConnectionProbe.TestAsync(httpClient, new Uri("http://server.test"), TestContext.Current.CancellationToken);

        status.Should().Be("Failed: No connection could be made");
    }

    [Fact]
    public async Task A_Server_That_Does_Not_Answer_In_Time_Is_A_Timeout()
    {
        using var handler = new RecordingHandler(new TaskCanceledException());
        using var httpClient = new HttpClient(handler);

        var status = await ServerConnectionProbe.TestAsync(httpClient, new Uri("http://server.test"), TestContext.Current.CancellationToken);

        status.Should().Be("Failed: Connection timed out");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode status;
        private readonly Exception? failure;

        public RecordingHandler(HttpStatusCode status)
            => this.status = status;

        public RecordingHandler(Exception failure)
            => this.failure = failure;

        public Uri? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedUri = request.RequestUri;
            if (failure is not null)
            {
                return Task.FromException<HttpResponseMessage>(failure);
            }

            return Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
        }
    }
}