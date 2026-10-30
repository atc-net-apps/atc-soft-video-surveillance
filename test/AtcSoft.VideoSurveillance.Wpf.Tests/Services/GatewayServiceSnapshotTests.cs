namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// The snapshot goes through the real generated endpoint and Atc.Rest.Client,
/// so a test sees what the client really makes of the server's PNG.
/// </summary>
public sealed class GatewayServiceSnapshotTests
{
    // A PNG signature and bytes that are no UTF-8: read as text, they change.
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xFF, 0x00, 0xC3, 0x28, 0x7F];

    [Fact]
    public async Task A_Snapshot_Is_The_Picture_The_Server_Sent()
    {
        using var png = new ByteArrayContent(Png);
        using var server = new GatewayTestServer(HttpStatusCode.OK, png, "image/png");

        var picture = await server.Gateway.CaptureSnapshotAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        picture.Should().Equal(Png);
    }

    [Fact]
    public async Task A_Camera_That_Gives_No_Picture_Gives_No_Snapshot()
    {
        using var problem = new StringContent("""{"status":503,"detail":"Camera did not reach Connected within 15s."}""");
        using var server = new GatewayTestServer(HttpStatusCode.ServiceUnavailable, problem, "application/problem+json");

        var picture = await server.Gateway.CaptureSnapshotAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        picture.Should().BeNull();
    }
}