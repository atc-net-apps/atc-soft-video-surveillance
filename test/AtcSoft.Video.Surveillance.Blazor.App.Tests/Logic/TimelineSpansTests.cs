namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

using CoreSpans = AtcSoft.Video.Surveillance.Helpers.Activity.TimelineSpans;

/// <summary>
/// The web client's copy of the timeline's spans and range-slider zoom, kept equal to Core's (the
/// desktop apps'), and the slider's highlighted part.
/// </summary>
public sealed class TimelineSpansTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_Choices_And_The_Shortest_Part_Are_Cores()
    {
        TimelineSpans.LastHoursChoices.Should().Equal(CoreSpans.LastHoursChoices);
        TimelineSpans.Shortest.Should().Be(AtcSoft.Video.Surveillance.Helpers.Activity.TimelineZoom.Shortest);
    }

    [Theory]
    [InlineData(1, 0, 60)]
    [InlineData(6, 60, 150)]
    [InlineData(6, -20, 500)]
    [InlineData(1, 30, 30)]
    [InlineData(1, 59, 60)]
    [InlineData(24, 700, 300)]
    public void A_Part_Is_The_Same_As_In_The_Desktop_Apps(
        int hours,
        double start,
        double end)
    {
        var web = TimelineSpans.Within(TimelineSpans.LastHours(hours, Nine), start, end);
        var core = CoreSpans.Within(Nine.AddHours(-hours), Nine, start, end);

        (web.From, web.To).Should().Be(core);
    }

    [Theory]
    [InlineData(0, 360)]
    [InlineData(10, 360)]
    [InlineData(0, 300)]
    public void The_Whole_Span_Is_Told_As_In_The_Desktop_Apps(
        double start,
        double end)
        => TimelineSpans.IsWhole(start, end, 360).Should().Be(CoreSpans.IsWhole(start, end, 360));

    [Fact]
    public void The_Last_Hours_Or_A_Day_Are_Chosen()
    {
        TimelineSpans.Whole(3, day: null, Nine, TimeZoneInfo.Utc).Should().Be(new TimelineSpan(Nine.AddHours(-3), Nine));
        TimelineSpans.Whole(0, new DateOnly(2026, 11, 2), Nine, TimeZoneInfo.Utc)
            .Should().Be(TimelineSpan.Day(new DateOnly(2026, 11, 2), TimeZoneInfo.Utc));
        TimelineSpans.Whole(0, day: null, Nine, TimeZoneInfo.Utc)
            .Should().Be(TimelineSpan.Day(new DateOnly(2026, 11, 4), TimeZoneInfo.Utc), "today, when no day is chosen yet");
    }

    [Fact]
    public void The_Sliders_Highlight_Lies_Between_Its_Thumbs()
    {
        TimelineSpans.Fill(60, 180, 240).Should().Be(("25%", "50%"));
        TimelineSpans.Fill(180, 60, 240).Should().Be(("25%", "50%"));
        TimelineSpans.Fill(0, 0, 0).Should().Be(("0%", "0%"));
    }
}