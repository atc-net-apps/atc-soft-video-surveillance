namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// A tile renews its stream lease, and starts the stream again when the server
/// says the lease is gone, but not when the server is only away.
/// </summary>
public sealed class GatewayServiceStreamLeaseTests
{
    [Theory]
    [InlineData(HttpStatusCode.NoContent, StreamLeaseRenewal.Renewed)]
    [InlineData(HttpStatusCode.NotFound, StreamLeaseRenewal.Gone)]
    [InlineData(HttpStatusCode.ServiceUnavailable, StreamLeaseRenewal.Unreachable)]
    public async Task The_Answer_To_A_Renewal_Is_What_Happens_To_The_Stream(
        HttpStatusCode status,
        StreamLeaseRenewal expected)
    {
        using var body = new StringContent("""{"status":0,"detail":"x"}""");
        using var server = new GatewayTestServer(status, body, "application/problem+json");

        var renewal = await server.Gateway.RenewStreamLeaseAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        renewal.Should().Be(expected);
    }

    [Fact]
    public async Task A_Server_That_Cannot_Be_Reached_Keeps_The_Lease_For_The_Next_Renewal()
    {
        using var server = new GatewayTestServer(_ => throw new HttpRequestException("No connection could be made"));

        var renewal = await server.Gateway.RenewStreamLeaseAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        renewal.Should().Be(StreamLeaseRenewal.Unreachable);
    }

    [Fact]
    public async Task A_Stream_That_Could_Not_Start_Is_Refused_With_The_Servers_Reason()
    {
        using var body = new StringContent("""{"status":503,"detail":"The camera could not be opened."}""");
        using var server = new GatewayTestServer(HttpStatusCode.ServiceUnavailable, body, "application/problem+json");

        var act = () => server.Gateway.StartStreamAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("The camera could not be opened.");
    }

    [Fact]
    public async Task Giving_A_Lease_Back_To_A_Server_That_Is_Away_Is_No_Error()
    {
        using var server = new GatewayTestServer(_ => throw new HttpRequestException("No connection could be made"));

        var act = () => server.Gateway.StopStreamAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync("the lease runs out by itself");
    }
}