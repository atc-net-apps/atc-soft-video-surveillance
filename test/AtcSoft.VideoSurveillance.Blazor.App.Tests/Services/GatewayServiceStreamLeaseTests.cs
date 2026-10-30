namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// The live view renews its stream leases, and starts a stream again when the
/// server says its lease is gone, but not when the server is only away.
/// </summary>
public sealed class GatewayServiceStreamLeaseTests
{
    [Theory]
    [InlineData(System.Net.HttpStatusCode.NoContent, StreamLeaseRenewal.Renewed)]
    [InlineData(System.Net.HttpStatusCode.NotFound, StreamLeaseRenewal.Gone)]
    [InlineData(System.Net.HttpStatusCode.ServiceUnavailable, StreamLeaseRenewal.Unreachable)]
    public async Task The_Answer_To_A_Renewal_Is_What_Happens_To_The_Stream(
        System.Net.HttpStatusCode status,
        StreamLeaseRenewal expected)
    {
        using var server = GatewayTestServer.Answering(status, """{"status":0,"detail":"x"}""");

        var renewal = await server.Gateway.RenewStreamLeaseAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        renewal.Should().Be(expected);
    }

    [Fact]
    public async Task A_Server_That_Cannot_Be_Reached_Keeps_The_Lease_For_The_Next_Renewal()
    {
        using var server = new GatewayTestServer(_ => throw new HttpRequestException("TypeError: Failed to fetch"));

        var renewal = await server.Gateway.RenewStreamLeaseAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        renewal.Should().Be(StreamLeaseRenewal.Unreachable);
    }

    [Fact]
    public async Task A_Started_Stream_Is_Its_Lease_And_One_That_Could_Not_Start_Is_The_Servers_Reason()
    {
        var cameraId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var calls = 0;
        using var server = new GatewayTestServer(request => ++calls == 1
            ? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(
                    "{\"leaseId\":\"" + leaseId + "\",\"cameraId\":\"" + cameraId + "\",\"playlistUrl\":\"/streams/a/stream.m3u8\",\"renewSeconds\":30}",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            }
            : new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
            {
                RequestMessage = request,
                Content = new StringContent("""{"status":503,"detail":"The camera could not be opened."}""", System.Text.Encoding.UTF8, "application/problem+json"),
            });

        var started = await server.Gateway.StartStreamAsync(cameraId, TestContext.Current.CancellationToken);
        var refused = await server.Gateway.StartStreamAsync(cameraId, TestContext.Current.CancellationToken);

        started.Value!.LeaseId.Should().Be(leaseId);
        started.Value.PlaylistUrl.Should().Be("/streams/a/stream.m3u8");
        refused.IsSuccess.Should().BeFalse();
        refused.Problem.Should().Be("The camera could not be opened.");
    }
}