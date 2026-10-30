namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// The recordings page says why a recording was not deleted, from the status
/// the server answered.
/// </summary>
public sealed class GatewayServiceRecordingDeletionTests
{
    [Theory]
    [InlineData(System.Net.HttpStatusCode.NoContent, RecordingDeletionOutcome.Deleted)]
    [InlineData(System.Net.HttpStatusCode.Conflict, RecordingDeletionOutcome.InProgress)]
    [InlineData(System.Net.HttpStatusCode.NotFound, RecordingDeletionOutcome.NotFound)]
    [InlineData(System.Net.HttpStatusCode.BadRequest, RecordingDeletionOutcome.Failed)]
    public async Task The_Answer_Is_The_Outcome(
        System.Net.HttpStatusCode status,
        RecordingDeletionOutcome expected)
    {
        using var server = GatewayTestServer.Answering(status, """{"status":0,"detail":"x"}""");

        var outcome = await server.Gateway.DeleteRecordingAsync("Gate/2026-10-14/Gate_20261014_101500.mp4", TestContext.Current.CancellationToken);

        outcome.Should().Be(expected);
    }

    [Fact]
    public async Task A_Server_That_Cannot_Be_Reached_Is_A_Failure()
    {
        using var server = new GatewayTestServer(_ => throw new HttpRequestException("TypeError: Failed to fetch"));

        var outcome = await server.Gateway.DeleteRecordingAsync("Gate/x.mp4", TestContext.Current.CancellationToken);

        outcome.Should().Be(RecordingDeletionOutcome.Failed);
    }
}