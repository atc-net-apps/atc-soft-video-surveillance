namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Zooming a recording's timeline: from the whole recording down to a minute, around a place that
/// keeps its spot, with the player kept in view and the bars never averaged away.
/// </summary>
public sealed class ActivityTimelineZoomTests
{
    private const double Hour = 3600;

    [Theory]
    [InlineData(0, 400)]
    [InlineData(30, 400)]
    [InlineData(1800, 1800)]
    [InlineData(1800.2, 1801)]
    [InlineData(4 * Hour, 4000)]
    public void A_Recording_Is_Loaded_In_One_Stretch_A_Second_Within_Limits(
        double durationSeconds,
        int expected)
        => ActivityTimelineZoom.BucketsFor(durationSeconds).Should().Be(expected);

    [Fact]
    public void Zooming_In_Halves_The_Window_And_Keeps_The_Focus_In_Its_Place()
    {
        ActivityTimelineZoom.In(0, 1, 0.25, Hour).Should().Be((0.125, 0.625));
        ActivityTimelineZoom.Out(0.125, 0.625, 0.25).Should().Be((0, 1));
    }

    [Fact]
    public void A_Focus_Outside_The_Window_Zooms_Around_Its_Middle()
        => ActivityTimelineZoom.In(0.5, 1, 0.1, Hour).Should().Be((0.625, 0.875));

    [Fact]
    public void The_Window_Stays_On_The_Recording_And_Between_A_Minute_And_All_Of_It()
    {
        ActivityTimelineZoom.In(0, 1, 1, Hour).Should().Be((0.5, 1));
        Round(ActivityTimelineZoom.Out(0.8, 1, 1)).Should().Be((0.6, 1));

        var (start, end) = (0d, 1d);
        for (var i = 0; i < 20; i++)
        {
            (start, end) = ActivityTimelineZoom.In(start, end, 0.3, Hour);
        }

        ((end - start) * Hour).Should().BeApproximately(60, 1e-6);
        ActivityTimelineZoom.CanZoomIn(start, end, Hour).Should().BeFalse();
        ActivityTimelineZoom.CanZoomOut(start, end).Should().BeTrue();
        ActivityTimelineZoom.CanZoomOut(0, 1).Should().BeFalse();
        ActivityTimelineZoom.CanZoomIn(0, 1, 50).Should().BeFalse("a recording under a minute is shown whole");
    }

    [Fact]
    public void The_Window_Follows_The_Player_Out_Of_It_But_Not_Into_It()
    {
        ActivityTimelineZoom.Follow(0.2, 0.3, 0.29, 0.31).Should().Be((0.31, 0.41), "playing on past the end turns the page");
        ActivityTimelineZoom.Follow(0.2, 0.3, 0.25, 0.95).Should().Be((0.9, 1), "a seek past the end moves there, kept on the recording");
        ActivityTimelineZoom.Follow(0.2, 0.3, 0.25, 0.26).Should().Be((0.2, 0.3));
        ActivityTimelineZoom.Follow(0.2, 0.3, 0.6, 0.61).Should().Be((0.2, 0.3), "a window the player was not in is a look elsewhere");
    }

    [Fact]
    public void A_Place_On_The_Timeline_Is_A_Place_In_The_Recording()
    {
        ActivityTimelineZoom.ToRecording(0.5, 0.75, 0.5).Should().Be(0.625);
        ActivityTimelineZoom.ToRecording(0.5, 0.75, 2).Should().Be(0.75);
        ActivityTimelineZoom.ToShare(0.5, 0.75, 0.625).Should().Be(0.5);
        ActivityTimelineZoom.ToShare(0.5, 0.75, 0.25).Should().BeNegative("not shown");
    }

    [Fact]
    public void Bars_Keep_The_Most_Of_The_Stretches_On_A_Column()
    {
        ActivityBucket[] buckets =
        [
            .. Enumerable.Range(0, 3600).Select(i => i == 1801
                ? new ActivityBucket(80, 1, IsAnalysed: true)
                : i < 100 ? ActivityBucket.NotAnalysed : new ActivityBucket(1, 0, IsAnalysed: true)),
        ];

        var whole = ActivityTimelineZoom.Bars(buckets, 0, 1, 400);

        whole.Should().HaveCount(400);
        whole[0].IsAnalysed.Should().BeFalse();
        whole[200].Level.Should().Be(80, "a second in nine is kept, never averaged away");
        whole[^1].End.Should().Be(1);

        var minute = ActivityTimelineZoom.Bars(buckets, 1800 / 3600d, 1860 / 3600d, 400);

        minute.Should().HaveCount(60, "a stretch a second, each its own bar");
        (Round((minute[1].Start, minute[1].End)), minute[1].Level, minute[1].IsAnalysed).Should().Be((Round((1 / 60d, 2 / 60d)), 80d, true));
    }

    [Fact]
    public void Events_Outside_The_Window_Are_Left_Out_And_Those_At_Its_Edges_Cut()
    {
        ActivityEvent[] events = [new(0.1, 0.2), new(0.45, 0.55, "Gate"), new(0.9, 0.95)];

        var visible = ActivityTimelineZoom.Visible(events, 0.5, 0.75);

        visible.Select(e => (Round((e.Start, e.End)), e.Zone)).Should().Equal(((0d, 0.2), "Gate"));
    }

    private static (double Start, double End) Round(
        (double Start, double End) window)
        => (Math.Round(window.Start, 9), Math.Round(window.End, 9));
}