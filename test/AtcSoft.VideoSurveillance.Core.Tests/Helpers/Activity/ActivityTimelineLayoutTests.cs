namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The timeline under a recording: bars for how much changed, bands for where something moved,
/// and jumps to the next or previous event.
/// </summary>
public sealed class ActivityTimelineLayoutTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0.1)]
    [InlineData(25, 0.5)]
    [InlineData(100, 1)]
    [InlineData(250, 1)]
    [InlineData(-3, 0)]
    public void A_Bar_Rises_With_The_Square_Root_Of_The_Level(
        double level,
        double height)
        => ActivityTimelineLayout.BarHeight(level).Should().BeApproximately(height, 1e-9);

    [Fact]
    public void Runs_Of_Stretches_With_Something_Moving_Are_Events()
    {
        var buckets = Buckets(0, 1, 2, 0, 0, 1, 0, 0, 0, 3);

        ActivityTimelineLayout.FindEvents(buckets).Should().Equal(
            new ActivityEvent(0.1, 0.3),
            new ActivityEvent(0.5, 0.6),
            new ActivityEvent(0.9, 1.0));
    }

    [Fact]
    public void Moving_Seconds_Five_Seconds_Or_Less_Apart_Are_One_Event()
    {
        // A stretch a second: five quiet seconds join, six part.
        var buckets = Buckets(1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0);

        ActivityTimelineLayout.FindEvents(buckets, durationSeconds: 16).Should().Equal(
            new ActivityEvent(0, 7.0 / 16),
            new ActivityEvent(13.0 / 16, 14.0 / 16));
    }

    [Fact]
    public void The_Gap_Is_Measured_In_Seconds_Whatever_The_Stretches()
    {
        // Two seconds a stretch: two quiet stretches (4 s) join, three (6 s) part.
        var buckets = Buckets(1, 0, 0, 1, 0, 0, 0, 1, 0, 0);

        ActivityTimelineLayout.FindEvents(buckets, durationSeconds: 20).Should().Equal(
            new ActivityEvent(0, 0.4),
            new ActivityEvent(0.7, 0.8));
        ActivityTimelineLayout.FindEvents(buckets, durationSeconds: 0).Should().HaveCount(3, "without the length nothing is joined");
    }

    [Fact]
    public void A_Recordings_Events_Are_Those_Of_The_Timeline_Of_Every_Camera()
    {
        // Moving seconds with every gap from none to eight quiet seconds, over and over.
        var moving = new HashSet<int>();
        for (var (second, gap) = (0, 0); second < 600; second += gap + 1, gap = (gap + 1) % 9)
        {
            moving.Add(second);
        }

        var seconds = Enumerable.Range(0, 600)
            .Select(i => new ActivitySecond(i, 10, moving.Contains(i) ? 1 : 0, []))
            .ToList();
        var log = new ActivityLog(DateTimeOffset.UnixEpoch, seconds);

        var events = ActivityTimelineLayout.FindEvents(ActivityDownsampler.ByMax(log, 600, 600), 600);

        events.Select(e => ((int)Math.Round(e.Start * 600), (int)Math.Round(e.End * 600)))
            .Should().Equal(ActivitySummarizer.Summarize(log).Events.Select(e => (e.StartOffset, e.EndOffset)));
    }

    [Fact]
    public void An_Event_Is_In_The_Zone_Of_Most_Of_Its_Stretches_The_Earlier_On_A_Tie()
    {
        ActivityBucket Moving(params string[] zones)
            => new(5, 1, IsAnalysed: true) { Zones = zones };
        var quiet = new ActivityBucket(0, 0, IsAnalysed: true);
        IReadOnlyList<ActivityBucket> buckets =
        [
            Moving("Path"), Moving("Gate", "Path"), Moving("Gate"), Moving("Gate"), quiet,
            Moving("Path"), Moving("Gate"), quiet,
            Moving(), quiet,
        ];

        ActivityTimelineLayout.FindEvents(buckets).Select(e => e.Zone).Should().Equal("Gate", "Path", null);
    }

    [Fact]
    public void A_Five_Second_Event_In_An_Hour_Is_Drawn_Three_Pixels_Wide_Around_Its_Middle()
    {
        var fiveSeconds = new ActivityEvent(1800.0 / 3600, 1805.0 / 3600);

        var (x, width) = ActivityTimelineLayout.ToPixels(fiveSeconds, 1000);

        width.Should().Be(3);
        (x + 1.5).Should().BeApproximately(1802.5 / 3600 * 1000, 1e-9);
    }

    [Fact]
    public void A_Narrow_Band_At_An_Edge_Stays_On_The_Timeline_And_A_Wide_One_Keeps_Its_Width()
    {
        ActivityTimelineLayout.ToPixels(new ActivityEvent(0, 0.0001), 1000).Should().Be((0, 3));
        ActivityTimelineLayout.ToPixels(new ActivityEvent(0.9999, 1), 1000).Should().Be((997, 3));
        ActivityTimelineLayout.ToPixels(new ActivityEvent(0.25, 0.5), 1000).Should().Be((250, 250));
        ActivityTimelineLayout.ToPixels(new ActivityEvent(0.25, 0.5), 0).Should().Be((0, 0));
    }

    [Fact]
    public void Next_Goes_To_The_First_Event_After_The_Position()
    {
        var events = ActivityTimelineLayout.FindEvents(Buckets(0, 1, 0, 0, 1, 0, 0, 0, 0, 0));

        ActivityTimelineLayout.NextEventStart(events, 0).Should().Be(0.1);
        ActivityTimelineLayout.NextEventStart(events, 0.1).Should().Be(0.4);
        ActivityTimelineLayout.NextEventStart(events, 0.4).Should().BeNull();
    }

    [Fact]
    public void Previous_Goes_One_Back_Even_Right_After_Jumping_To_An_Event()
    {
        var events = ActivityTimelineLayout.FindEvents(Buckets(0, 1, 0, 0, 1, 0, 0, 0, 0, 0));

        ActivityTimelineLayout.PreviousEventStart(events, 0.8, slack: 0.01).Should().Be(0.4);
        ActivityTimelineLayout.PreviousEventStart(events, 0.405, slack: 0.01).Should().Be(0.1, "it is still at the start of the second event");
        ActivityTimelineLayout.PreviousEventStart(events, 0.1, slack: 0.01).Should().BeNull();
    }

    private static List<ActivityBucket> Buckets(params int[] tracks)
        => [.. tracks.Select(t => new ActivityBucket(t * 5, t, IsAnalysed: true))];

    [Fact]
    public void Play_Events_Only_Plays_On_In_An_Event_Jumps_To_The_Next_And_Ends_After_The_Last()
    {
        ActivityEvent[] events = [new(0.2, 0.3), new(0.6, 0.7)];

        ActivityTimelineLayout.EventsOnlyTarget(events, 0, 0.01, 0.02).Should().BeApproximately(0.19, 1e-9, "before the first event: to its lead-in");
        ActivityTimelineLayout.EventsOnlyTarget(events, 0.25, 0.01, 0.02).Should().BeNull("inside an event it plays on");
        ActivityTimelineLayout.EventsOnlyTarget(events, 0.315, 0.01, 0.02).Should().BeNull("the lead-out is shown too");
        ActivityTimelineLayout.EventsOnlyTarget(events, 0.4, 0.01, 0.02).Should().BeApproximately(0.59, 1e-9);
        ActivityTimelineLayout.EventsOnlyTarget(events, 0.8, 0.01, 0.02).Should().Be(1, "after the last event: the end");
        ActivityTimelineLayout.EventsOnlyTarget([], 0.5, 0.01, 0.02).Should().BeNull("without events the recording plays as it is");
    }
}