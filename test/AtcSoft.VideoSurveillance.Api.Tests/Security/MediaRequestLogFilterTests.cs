namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// A request for a stream's files or a recording carries a lease or a media
/// token in its address; its request lines are kept out of the log.
/// </summary>
public sealed class MediaRequestLogFilterTests
{
    [Theory]
    [InlineData("/streams/0f8fad5bd9cb469fa165e1b1f1b7a6d1/stream.m3u8", true)]
    [InlineData("/recordings-files/Gate/2026-10-15/Gate.mp4", true)]
    [InlineData("/cameras", false)]
    [InlineData("/recordings", false)]
    public void A_Media_Request_Is_Not_Logged(
        string path,
        bool excluded)
    {
        var logEvent = new Serilog.Events.LogEvent(
            DateTimeOffset.UtcNow,
            Serilog.Events.LogEventLevel.Information,
            exception: null,
            new Serilog.Parsing.MessageTemplateParser().Parse("Request starting {RequestPath}{QueryString}"),
            [
                new Serilog.Events.LogEventProperty("RequestPath", new Serilog.Events.ScalarValue(path)),
                new Serilog.Events.LogEventProperty("QueryString", new Serilog.Events.ScalarValue("?media=secret")),
            ]);

        MediaRequestLogFilter.IsMediaRequest(logEvent).Should().Be(excluded);
    }
}