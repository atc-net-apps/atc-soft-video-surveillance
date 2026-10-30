namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

/// <summary>
/// The three calls of a viewer's stream lease, on what the stream service answers.
/// </summary>
public class StreamLeaseHandlerTests
{
    private readonly ILiveStreamService streams = Substitute.For<ILiveStreamService>();
    private readonly Guid cameraId = Guid.NewGuid();
    private readonly Guid leaseId = Guid.NewGuid();

    public StreamLeaseHandlerTests()
        => streams.RenewInterval.Returns(TimeSpan.FromSeconds(30));

    [Fact]
    public async Task A_Started_Stream_Is_The_Lease_And_The_Playlist()
    {
        streams.StartAsync(cameraId, Arg.Any<CancellationToken>()).Returns(LiveStreamStart.Started(leaseId, "/streams/a/stream.m3u8"));
        var handler = new StartStreamHandler(streams);

        var result = await handler.ExecuteAsync(new StartStreamParameters(cameraId), CancellationToken.None);

        var response = await result.Result.ExecuteAsync();
        response.StatusCode.Should().Be(StatusCodes.Status200OK);
        var lease = ReadJson(response);
        lease.GetProperty("leaseId").GetGuid().Should().Be(leaseId);
        lease.GetProperty("cameraId").GetGuid().Should().Be(cameraId);
        lease.GetProperty("playlistUrl").GetString().Should().Be("/streams/a/stream.m3u8");
        lease.GetProperty("renewSeconds").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task A_Camera_The_Server_Does_Not_Know_Is_Not_Found()
    {
        streams.StartAsync(cameraId, Arg.Any<CancellationToken>()).Returns(LiveStreamStart.CameraNotFound(cameraId));
        var handler = new StartStreamHandler(streams);

        var result = await handler.ExecuteAsync(new StartStreamParameters(cameraId), CancellationToken.None);

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task A_Stream_That_Could_Not_Start_Is_Unavailable_With_The_Reason_And_When_To_Try_Again()
    {
        streams.StartAsync(cameraId, Arg.Any<CancellationToken>()).Returns(LiveStreamStart.Failed("The camera could not be opened."));
        var handler = new StartStreamHandler(streams);

        var result = await handler.ExecuteAsync(new StartStreamParameters(cameraId), CancellationToken.None);

        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var response = await result.Result.ExecuteAsync();
        response.Headers.RetryAfter.ToString().Should().Be("10");
        ReadJson(response).GetProperty("detail").GetString().Should().Be("The camera could not be opened.");
    }

    [Fact]
    public async Task A_Renewed_Lease_Is_No_Content_And_One_The_Server_Does_Not_Have_Is_Not_Found()
    {
        streams.Renew(cameraId, leaseId).Returns(true);
        var handler = new RenewStreamLeaseHandler(streams);

        var renewed = await handler.ExecuteAsync(new RenewStreamLeaseParameters(cameraId, leaseId), CancellationToken.None);
        var unknown = await handler.ExecuteAsync(new RenewStreamLeaseParameters(cameraId, Guid.NewGuid()), CancellationToken.None);

        (await renewed.Result.ExecuteAsync()).StatusCode.Should().Be(StatusCodes.Status204NoContent);
        unknown.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task A_Lease_Given_Back_Is_Released()
    {
        var handler = new DeleteStreamLeaseHandler(streams);

        var result = await handler.ExecuteAsync(new DeleteStreamLeaseParameters(cameraId, leaseId), CancellationToken.None);

        streams.Received(1).Release(cameraId, leaseId);
        (await result.Result.ExecuteAsync()).StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    private static JsonElement ReadJson(HttpResponse response)
    {
        response.Body.Position = 0;
        return JsonDocument.Parse(response.Body).RootElement.Clone();
    }
}