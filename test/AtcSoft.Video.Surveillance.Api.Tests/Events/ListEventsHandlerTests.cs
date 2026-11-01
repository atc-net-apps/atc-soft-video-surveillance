namespace AtcSoft.Video.Surveillance.Api.Events;

/// <summary>
/// A client that goes before its events are read leaves the feed with its request.
/// </summary>
public sealed class ListEventsHandlerTests
{
    [Fact]
    public async Task A_Stream_Abandoned_Before_It_Is_Read_Leaves_The_Feed()
    {
        var feed = new SurveillanceEventFeed();
        using var request = new CancellationTokenSource();
        var sut = new AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Events.ListEventsHandler(feed);

        await sut.ExecuteAsync(new global::VideoSurveillance.Generated.Events.Parameters.ListEventsParameters(), request.Token);
        feed.SubscriberCount.Should().Be(1);

        await request.CancelAsync();

        feed.SubscriberCount.Should().Be(0);
    }
}