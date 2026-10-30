namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Zooming a timeline of every camera keeps the place last played where it is on the screen,
/// stays between ten minutes and seven days, and never goes past now.
/// </summary>
public sealed class TimelineZoomTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Nine.AddDays(2);

    [Fact]
    public void Zooming_In_Halves_The_Span_And_Keeps_The_Focus_In_Its_Place()
    {
        var (from, to) = TimelineZoom.In(Nine, Nine.AddHours(4), Nine.AddHours(1), Now);

        (from, to).Should().Be((Nine.AddMinutes(30), Nine.AddHours(2).AddMinutes(30)));
    }

    [Fact]
    public void A_Focus_Outside_The_Span_Zooms_Around_Its_Middle()
        => TimelineZoom.In(Nine, Nine.AddHours(4), Nine.AddDays(-1), Now).Should().Be((Nine.AddHours(1), Nine.AddHours(3)));

    [Fact]
    public void The_Span_Stays_Between_Ten_Minutes_And_Seven_Days()
    {
        var (shortFrom, shortTo) = TimelineZoom.In(Nine, Nine.AddMinutes(15), Nine, Now);
        var (longFrom, longTo) = TimelineZoom.Out(Nine.AddDays(-6), Nine, Nine, Nine);

        (shortTo - shortFrom).Should().Be(TimeSpan.FromMinutes(10));
        (longTo - longFrom).Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Nothing_Goes_Past_Now()
    {
        TimelineZoom.Out(Now.AddHours(-2), Now, Now, Now).Should().Be((Now.AddHours(-4), Now));
        TimelineZoom.Move(Now.AddHours(-2), Now, later: true, Now).Should().Be((Now.AddHours(-2), Now));
        TimelineZoom.Move(Nine, Nine.AddHours(2), later: false, Now).Should().Be((Nine.AddHours(-1), Nine.AddHours(1)));
    }
}