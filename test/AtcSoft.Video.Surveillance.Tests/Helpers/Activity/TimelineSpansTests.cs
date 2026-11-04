namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// A timeline shows the last 1 to 24 hours or a chosen day, and a range slider zooms into a part
/// of it: never outside it, and never shorter than ten minutes.
/// </summary>
public sealed class TimelineSpansTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_Choices_Run_From_The_Last_Hour_To_The_Last_Day()
        => TimelineSpans.LastHoursChoices.Should().Equal(1, 3, 6, 12, 24);

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(24)]
    public void The_Last_Hours_End_Now(int hours)
        => TimelineSpans.LastHours(hours, Nine).Should().Be((Nine.AddHours(-hours), Nine));

    [Fact]
    public void A_Part_Is_The_Minutes_Chosen_On_The_Slider()
        => TimelineSpans.Within(Nine.AddHours(-6), Nine, 60, 150)
            .Should().Be((Nine.AddHours(-5), Nine.AddHours(-3.5)));

    [Fact]
    public void A_Part_Never_Lies_Outside_The_Span()
        => TimelineSpans.Within(Nine.AddHours(-1), Nine, -20, 500)
            .Should().Be((Nine.AddHours(-1), Nine));

    [Fact]
    public void A_Part_Is_At_Least_Ten_Minutes_Around_Where_It_Was_Chosen()
        => TimelineSpans.Within(Nine.AddHours(-1), Nine, 30, 30)
            .Should().Be((Nine.AddMinutes(-35), Nine.AddMinutes(-25)));

    [Fact]
    public void A_Short_Part_At_The_End_Stays_Inside_The_Span()
        => TimelineSpans.Within(Nine.AddHours(-1), Nine, 59, 60)
            .Should().Be((Nine.AddMinutes(-10), Nine));

    [Theory]
    [InlineData(0, 360, true)]
    [InlineData(0.2, 359.8, true)]
    [InlineData(10, 360, false)]
    [InlineData(0, 300, false)]
    public void The_Whole_Span_Is_Told_From_A_Part(
        double start,
        double end,
        bool whole)
        => TimelineSpans.IsWhole(start, end, 360).Should().Be(whole);
}