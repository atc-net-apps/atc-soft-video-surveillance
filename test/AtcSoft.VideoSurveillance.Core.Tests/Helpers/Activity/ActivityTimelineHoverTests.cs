namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The mouse resting on a recording's timeline tells the most that happened under it, and the
/// watch zone of an event there.
/// </summary>
public sealed class ActivityTimelineHoverTests
{
    private static readonly ActivityBucket[] Buckets =
    [
        ActivityBucket.NotAnalysed,
        new(5, 0, IsAnalysed: true),
        new(40, 2, IsAnalysed: true) { Zones = ["Gate"] },
        new(20, 1, IsAnalysed: true),
        new(5, 0, IsAnalysed: true),
    ];

    [Fact]
    public void It_Tells_The_Most_Under_The_Mouse_And_The_Zone_Of_Its_Event()
    {
        var hover = ActivityTimelineHover.At(Buckets, ActivityTimelineLayout.FindEvents(Buckets), 0.6, 0.7)!;

        (hover with { Position = Math.Round(hover.Position, 9) }).Should().Be(new ActivityHover(0.65, 1, 20, IsAnalysed: true, "Gate"), "the fourth stretch is in the event that started in the gate");
    }

    [Fact]
    public void A_Quiet_Or_Unanalysed_Place_Says_So()
    {
        var events = ActivityTimelineLayout.FindEvents(Buckets);

        ActivityTimelineHover.At(Buckets, events, 0.05, 0.1)!.IsAnalysed.Should().BeFalse();
        ActivityTimelineHover.At(Buckets, events, 0.9, 0.95)!.Should().Be(new ActivityHover(0.925, 0, 5, IsAnalysed: true, Zone: null));
        ActivityTimelineHover.At([], [], 0, 1).Should().BeNull();
    }

    [Fact]
    public void The_Places_May_Come_In_Either_Order_And_Stay_On_The_Recording()
        => ActivityTimelineHover.At(Buckets, [], 1.5, 0.95)!.Position.Should().Be(0.975);
}