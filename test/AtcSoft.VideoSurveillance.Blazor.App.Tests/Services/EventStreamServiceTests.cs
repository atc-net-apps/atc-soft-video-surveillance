namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// The client reads GET /events over the real generated endpoint, and goes on
/// after the last event it read when the stream drops.
/// </summary>
public sealed class EventStreamServiceTests
{
    private static readonly Guid Front = Guid.NewGuid();

    [Fact]
    public async Task A_Stream_That_Drops_Is_Read_On_After_The_Last_Event()
    {
        var requests = new List<string?>();
        using var server = new GatewayTestServer(request =>
        {
            var lastEventId = request.Headers.TryGetValues("Last-Event-ID", out var values) ? values.Single() : null;
            requests.Add(lastEventId);

            // The first stream ends after two events (as a dropped connection does), the second goes on after them.
            return lastEventId is null
                ? Stream("event: ping\ndata:\n\n" + Event(1) + Event(2))
                : Stream(Event(3));
        });
        var sut = new EventStreamService(server.Get<IListEventsEndpoint>()) { Time = new ImmediateTimeProvider() };
        using var done = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var received = new List<SurveillanceEvent>();

        await sut.RunAsync(
            async e =>
            {
                received.Add(e);
                if (received.Count == 3)
                {
                    await done.CancelAsync();
                }
            },
            done.Token);

        received.Select(e => e.Connection!.NewState).Should().Equal(
            CameraConnectionState.Connecting,
            CameraConnectionState.Connected,
            CameraConnectionState.Disconnected);
        requests.Take(2).Should().Equal(null, "2");
    }

    [Fact]
    public async Task A_Server_That_Is_Away_Is_Asked_Again_A_Little_Later()
    {
        var calls = 0;
        using var server = new GatewayTestServer(_ =>
            ++calls == 1
                ? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("""{"status":503,"detail":"Starting."}""", System.Text.Encoding.UTF8, "application/problem+json"),
                }
                : Stream(Event(1)));
        var time = new ImmediateTimeProvider();
        var sut = new EventStreamService(server.Get<IListEventsEndpoint>()) { Time = time };
        using var done = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await sut.RunAsync(
            async _ => await done.CancelAsync(),
            done.Token);

        calls.Should().Be(2);
        time.Waits.Should().ContainSingle().Which.Should().BePositive();
    }

    [Fact]
    public async Task An_Event_That_Is_Not_Json_Does_Not_End_The_Reading()
    {
        var calls = 0;
        using var server = new GatewayTestServer(_ =>
            ++calls == 1
                ? Stream("id: 1\ndata: {not json\n\n")
                : Stream(Event(2)));
        var sut = new EventStreamService(server.Get<IListEventsEndpoint>()) { Time = new ImmediateTimeProvider() };
        using var done = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await sut.RunAsync(async _ => await done.CancelAsync(), done.Token);

        calls.Should().Be(2);
    }

    [Fact]
    public async Task It_Says_When_It_Starts_And_Stops_Reading()
    {
        using var server = new GatewayTestServer(_ => Stream(Event(1)));
        var sut = new EventStreamService(server.Get<IListEventsEndpoint>()) { Time = new ImmediateTimeProvider() };
        var changes = new List<bool>();
        sut.ReadingChanged += () => changes.Add(sut.IsReading);
        using var done = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await sut.RunAsync(async _ => await done.CancelAsync(), done.Token);

        // Open before the first event, closed when it ends.
        changes.Should().Equal(true, false);
    }

    private static HttpResponseMessage Stream(string body)
        => new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "text/event-stream"),
        };

    private static string Event(int id)
    {
        var state = id switch
        {
            1 => "connecting",
            2 => "connected",
            _ => "disconnected",
        };

        var timestamp = $"2026-10-15T09:00:0{id}Z";
        var json =
            "{\"type\":\"connectionStateChanged\",\"cameraId\":\"" + Front + "\",\"timestamp\":\"" + timestamp + "\"," +
            "\"connection\":{\"cameraId\":\"" + Front + "\",\"newState\":\"" + state + "\",\"timestamp\":\"" + timestamp + "\"}}";

        return $"id: {id}\ndata: {json}\n\n";
    }

    // Ends every wait at once and remembers how long each was.
    private sealed class ImmediateTimeProvider : TimeProvider
    {
        public List<TimeSpan> Waits { get; } = [];

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            lock (Waits)
            {
                Waits.Add(dueTime);
            }

            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoTimer();
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(
                TimeSpan dueTime,
                TimeSpan period)
                => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;
        }
    }
}