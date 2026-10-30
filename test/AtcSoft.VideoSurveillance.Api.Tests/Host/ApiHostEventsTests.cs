namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// GET /events streams the camera events as Server-Sent Events, with ids a
/// client resumes after, and a ping while nothing happens.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostEventsTests(ApiHostFixture host)
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_Stream_Answers_At_Once_Before_Any_Event()
    {
        using var client = host.CreateClient();
        var opened = Stopwatch.StartNew();

        // Nothing happens for a minute, yet the headers come straight away.
        using var response = await OpenAsync(client, $"/events?cameraId={Guid.NewGuid()}&heartbeatSeconds=60");

        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
        opened.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_Quiet_Stream_Sends_A_Ping()
    {
        using var client = host.CreateClient();
        using var response = await OpenAsync(client, $"/events?cameraId={Guid.NewGuid()}&heartbeatSeconds=1");

        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        // One after each quiet second.
        var events = await ReadEventsAsync(response, count: 2, skipPings: false);

        events.Select(e => e.Name).Should().Equal("ping", "ping");
    }

    [Fact]
    public async Task An_Event_Reaches_The_Stream_With_An_Id()
    {
        var cameraId = Guid.NewGuid();
        using var client = host.CreateClient();
        using var response = await OpenAsync(client, $"/events?cameraId={cameraId}");

        var sequence = host.GetService<SurveillanceEventFeed>().Publish(Motion(cameraId));
        var received = (await ReadEventsAsync(response, count: 1)).Single();

        received.Id.Should().Be(sequence.ToString(CultureInfo.InvariantCulture));
        using var data = JsonDocument.Parse(received.Data);
        data.RootElement.GetProperty("type").GetString().Should().Be("motionDetected");
        data.RootElement.GetProperty("cameraId").GetGuid().Should().Be(cameraId);
        data.RootElement.GetProperty("motion").GetProperty("isMotionActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_Motion_Box_Reaches_The_Stream_With_Its_Track_Id()
    {
        var cameraId = Guid.NewGuid();
        using var client = host.CreateClient();
        using var response = await OpenAsync(client, $"/events?cameraId={cameraId}");

        host.GetService<SurveillanceEventFeed>().Publish(Motion(cameraId, new MotionBox(X: 10, Y: 20, Width: 30, Height: 40, TrackId: 7)));
        var received = (await ReadEventsAsync(response, count: 1)).Single();

        using var data = JsonDocument.Parse(received.Data);
        var box = data.RootElement.GetProperty("motion").GetProperty("boundingBoxes")[0];
        box.GetProperty("trackId").GetInt32().Should().Be(7, "a client follows each box by its track");
    }

    [Fact]
    public async Task A_Client_That_Resumes_Gets_What_It_Missed()
    {
        var cameraId = Guid.NewGuid();
        var feed = host.GetService<SurveillanceEventFeed>();
        var first = feed.Publish(Motion(cameraId));
        var second = feed.Publish(Motion(cameraId));
        var third = feed.Publish(Motion(cameraId));
        using var client = host.CreateClient();

        using var response = await OpenAsync(client, $"/events?cameraId={cameraId}", lastEventId: first);
        var replayed = await ReadEventsAsync(response, count: 2);

        replayed.Select(e => e.Id).Should().Equal(
            second.ToString(CultureInfo.InvariantCulture),
            third.ToString(CultureInfo.InvariantCulture));
        response.Headers.GetValues("X-Events-Missed").Single().Should().Be("0");
    }

    [Fact]
    public async Task The_Camera_Filter_Leaves_Out_Other_Cameras()
    {
        var cameraId = Guid.NewGuid();
        var feed = host.GetService<SurveillanceEventFeed>();
        using var client = host.CreateClient();
        using var response = await OpenAsync(client, $"/events?cameraId={cameraId}");

        feed.Publish(Motion(Guid.NewGuid()));
        var mine = feed.Publish(Motion(cameraId));
        var received = (await ReadEventsAsync(response, count: 1)).Single();

        received.Id.Should().Be(mine.ToString(CultureInfo.InvariantCulture));
    }

    private static SurveillanceEvent Motion(
        Guid cameraId,
        params MotionBox[] boxes)
        => new MotionDetectedEvent(
            cameraId,
            IsMotionActive: true,
            ChangePercentage: 2.5,
            BoundingBoxes: [.. boxes],
            AnalysisWidth: 320,
            AnalysisHeight: 240,
            Timestamp: DateTimeOffset.UtcNow).ToStreamEvent();

    private static async Task<HttpResponseMessage> OpenAsync(
        HttpClient client,
        string path,
        long? lastEventId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (lastEventId is { } id)
        {
            request.Headers.Add("Last-Event-ID", id.ToString(CultureInfo.InvariantCulture));
        }

        var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken)
            .WaitAsync(Wait, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return response;
    }

    // Reads Server-Sent Events: "id:", "event:" and "data:" lines, an empty line ends one.
    private static async Task<List<(string? Id, string Name, string Data)>> ReadEventsAsync(
        HttpResponseMessage response,
        int count,
        bool skipPings = true)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Wait);
        var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);
        var events = new List<(string? Id, string Name, string Data)>();
        string? id = null;
        var name = "message";
        var data = new System.Text.StringBuilder();
        while (events.Count < count)
        {
            var line = await reader.ReadLineAsync(timeout.Token) ?? throw new EndOfStreamException();
            if (line.Length == 0)
            {
                if ((data.Length > 0 || name != "message") && !(skipPings && name == "ping"))
                {
                    events.Add((id, name, data.ToString()));
                }

                id = null;
                name = "message";
                data.Clear();
            }
            else if (line.StartsWith("id:", StringComparison.Ordinal))
            {
                id = line[3..].Trim();
            }
            else if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                name = line[6..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                data.Append(line[5..].TrimStart());
            }
        }

        return events;
    }
}