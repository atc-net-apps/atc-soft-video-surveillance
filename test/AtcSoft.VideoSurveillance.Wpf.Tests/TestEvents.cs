namespace AtcSoft.VideoSurveillance.Wpf;

/// <summary>
/// An event service that is never started: a view model can subscribe to it,
/// and no event arrives.
/// </summary>
internal static class TestEvents
{
    public static SurveillanceEventService Service()
        => new(
            Substitute.For<IListEventsEndpoint>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SurveillanceEventService>.Instance,
            new SurveillanceEventServiceOptions());
}