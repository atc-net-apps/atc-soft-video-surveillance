namespace AtcSoft.Video.Surveillance.Wpf.Core.Models;

/// <summary>
/// The range slider under a timeline zooms only when the person moves it: a slider that clamps its
/// ends by itself (to its own maximum, before the bound one arrives) must not zoom the timeline.
/// </summary>
public sealed class TimelineZoomRangeTests
{
    [Fact]
    public void Ends_The_Slider_Changes_By_Itself_Do_Not_Zoom()
    {
        var zoomed = false;
        var sut = new TimelineZoomRange((_, _) => zoomed = true);
        sut.Set(0, 1440, 1440);

        // What the slider does before its maximum is bound: its end clamped to its default 100.
        sut.End = 100;

        zoomed.Should().BeFalse();
    }

    [Fact]
    public void The_Whole_Span_Is_Told_From_The_Ends()
    {
        var sut = new TimelineZoomRange((_, _) => { });

        sut.Set(0, 360, 360);
        sut.IsWhole.Should().BeTrue();

        sut.Set(60, 120, 360);
        sut.IsWhole.Should().BeFalse();
    }
}