namespace AtcSoft.VideoSurveillance.Api.Webhooks;

public sealed class WebhookSenderTests
{
    private static readonly WebhookEvent Event = new(
        WebhookEventTypes.MotionStarted,
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        "Front Door",
        new DateTimeOffset(2026, 10, 13, 10, 15, 0, TimeSpan.Zero),
        new global::VideoSurveillance.Generated.Models.WebhookPayloadData(ChangePercentage: 4.5));

    [Fact]
    public async Task The_Event_Is_Posted_As_Json_With_Its_Type_In_A_Header()
    {
        using var handler = new RecordingHandler(HttpStatusCode.OK);
        var sut = CreateSender(handler);

        var delivered = await sut.SendAsync(new WebhookEndpoint { Url = "https://example.test/hook" }, Event, TestContext.Current.CancellationToken);

        delivered.Should().BeTrue();
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be(new Uri("https://example.test/hook"));
        request.ContentType.Should().Be("application/json");
        request.Headers["X-AtcSoft-Event"].Should().Be("motion.started");
        request.Headers.Should().ContainKey("X-AtcSoft-Delivery");
        request.Headers.Should().NotContainKey("X-AtcSoft-Signature", "the endpoint has no secret");

        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("event").GetString().Should().Be("motion.started");
        body.RootElement.GetProperty("cameraId").GetGuid().Should().Be(Event.CameraId);
        body.RootElement.GetProperty("cameraName").GetString().Should().Be("Front Door");
        body.RootElement.GetProperty("timestamp").GetDateTimeOffset().Should().Be(Event.Timestamp);
        body.RootElement.GetProperty("data").GetProperty("changePercentage").GetDouble().Should().Be(4.5);
    }

    [Fact]
    public async Task The_Body_Has_Only_What_The_Event_Knows()
    {
        // Receivers match on these names; a field without a value is left out.
        using var handler = new RecordingHandler(HttpStatusCode.OK);
        var sut = CreateSender(handler);

        await sut.SendAsync(new WebhookEndpoint { Url = "https://example.test/hook" }, Event, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(handler.Requests.Single().Body);
        body.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().Equal("event", "cameraId", "cameraName", "timestamp", "data");
        body.RootElement.GetProperty("data").EnumerateObject().Select(p => p.Name)
            .Should().Equal("changePercentage");
    }

    [Fact]
    public async Task With_A_Secret_The_Body_Is_Signed()
    {
        using var handler = new RecordingHandler(HttpStatusCode.OK);
        var sut = CreateSender(handler);

        await sut.SendAsync(new WebhookEndpoint { Url = "https://example.test/hook", Secret = "s3cret" }, Event, TestContext.Current.CancellationToken);

        var request = handler.Requests.Single();
        request.Headers["X-AtcSoft-Signature"].Should().Be(
            WebhookSignature.Compute("s3cret", System.Text.Encoding.UTF8.GetBytes(request.Body)));
    }

    [Fact]
    public async Task A_Server_Error_Is_Tried_Again()
    {
        using var handler = new RecordingHandler(HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway, HttpStatusCode.OK);
        var sut = CreateSender(handler);

        var delivered = await sut.SendAsync(new WebhookEndpoint { Url = "https://example.test/hook" }, Event, TestContext.Current.CancellationToken);

        delivered.Should().BeTrue();
        handler.Requests.Should().HaveCount(3);
        handler.Requests.Select(r => r.Headers["X-AtcSoft-Delivery"]).Distinct(StringComparer.Ordinal).Should().ContainSingle("a retry is the same delivery");
    }

    [Fact]
    public async Task A_Receiver_That_Keeps_Failing_Is_Given_Up_On()
    {
        using var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        var sut = CreateSender(handler);

        var delivered = await sut.SendAsync(new WebhookEndpoint { Url = "https://example.test/hook" }, Event, TestContext.Current.CancellationToken);

        delivered.Should().BeFalse();
        handler.Requests.Should().HaveCount(WebhookSender.MaxAttempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task A_Refusal_Is_Not_Tried_Again(HttpStatusCode status)
    {
        // Sending the same thing again gets the same answer.
        using var handler = new RecordingHandler(status);
        var sut = CreateSender(handler);

        var delivered = await sut.SendAsync(new WebhookEndpoint { Url = "https://example.test/hook" }, Event, TestContext.Current.CancellationToken);

        delivered.Should().BeFalse();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task A_Receiver_That_Cannot_Be_Reached_Is_Tried_Again_And_Never_Throws()
    {
        using var handler = new RecordingHandler(new HttpRequestException("no route to host"));
        var sut = CreateSender(handler);

        var delivered = await sut.SendAsync(new WebhookEndpoint { Url = "https://example.test/hook" }, Event, TestContext.Current.CancellationToken);

        delivered.Should().BeFalse();
        handler.Requests.Should().HaveCount(WebhookSender.MaxAttempts);
    }

    [Fact]
    public async Task An_Invalid_Address_Is_Not_Sent_To()
    {
        using var handler = new RecordingHandler(HttpStatusCode.OK);
        var sut = CreateSender(handler);

        var delivered = await sut.SendAsync(new WebhookEndpoint { Url = "not a url" }, Event, TestContext.Current.CancellationToken);

        delivered.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    private static WebhookSender CreateSender(RecordingHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(WebhookSender.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));

        return new WebhookSender(factory, NullLogger<WebhookSender>.Instance)
        {
            RetryDelay = TimeSpan.Zero,
        };
    }

    private sealed record SentRequest(
        HttpMethod Method,
        Uri? Uri,
        string? ContentType,
        Dictionary<string, string> Headers,
        string Body);

    /// <summary>
    /// Answers with the given statuses in turn (the last one for good) and
    /// keeps what was sent.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> statuses = new();
        private readonly Exception? failure;
        private HttpStatusCode last = HttpStatusCode.OK;

        public RecordingHandler(params HttpStatusCode[] statuses)
        {
            foreach (var status in statuses)
            {
                this.statuses.Enqueue(status);
            }
        }

        public RecordingHandler(Exception failure)
        {
            this.failure = failure;
        }

        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new SentRequest(
                request.Method,
                request.RequestUri,
                request.Content?.Headers.ContentType?.MediaType,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(',', h.Value), StringComparer.OrdinalIgnoreCase),
                body));

            if (failure is not null)
            {
                throw failure;
            }

            if (statuses.Count > 0)
            {
                last = statuses.Dequeue();
            }

            return new HttpResponseMessage(last);
        }
    }
}