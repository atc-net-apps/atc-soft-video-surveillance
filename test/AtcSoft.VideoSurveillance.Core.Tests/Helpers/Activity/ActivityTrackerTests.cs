namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Activity on a recorded camera starts when a confirmed track appears and ends when nothing has
/// moved for the merge gap, the same way the summaries join moving seconds into events.
/// </summary>
public sealed class ActivityTrackerTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Camera = Guid.NewGuid();

    [Fact]
    public void Activity_Starts_With_The_First_Track_And_Ends_After_The_Gap()
    {
        var tracker = new ActivityTracker(Camera, "Cam/a.mkv", Nine);

        tracker.Add(Nine.AddSeconds(10), Quiet()).Should().BeNull();
        var started = tracker.Add(Nine.AddSeconds(12), Moving(20, 1));
        tracker.Add(Nine.AddSeconds(13), Moving(30, 2)).Should().BeNull("it goes on");
        tracker.Add(Nine.AddSeconds(17), Quiet()).Should().BeNull("four quiet seconds are within the gap");
        var ended = tracker.Add(Nine.AddSeconds(19), Quiet());

        started.Should().NotBeNull();
        started!.IsActive.Should().BeTrue();
        started.Start.Should().Be(Nine.AddSeconds(12));
        started.OffsetSeconds.Should().Be(12);
        started.VideoPath.Should().Be("Cam/a.mkv");
        started.End.Should().BeNull();

        ended.Should().NotBeNull();
        ended!.IsActive.Should().BeFalse();
        ended.Start.Should().Be(Nine.AddSeconds(12));
        ended.End.Should().Be(Nine.AddSeconds(14));
        ended.PeakLevel.Should().Be(30);
        ended.Tracks.Should().Be(2);
        tracker.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activity_Going_On_Ends_With_Its_Recording_And_Nothing_Ends_Twice()
    {
        var tracker = new ActivityTracker(Camera, "Cam/a.mkv", Nine);
        tracker.Add(Nine.AddSeconds(5), Moving(10, 1));

        tracker.End()!.End.Should().Be(Nine.AddSeconds(6));
        tracker.End().Should().BeNull();
    }

    private static MotionDetectorResult Quiet()
        => new(false, MotionHoldChange.None, 1, []);

    private static MotionDetectorResult Moving(
        double level,
        int tracks)
        => new(true, MotionHoldChange.Continues, level, [.. Enumerable.Range(1, tracks).Select(id => new BoundingBox { Width = 5, Height = 5, TrackId = id })]);
}