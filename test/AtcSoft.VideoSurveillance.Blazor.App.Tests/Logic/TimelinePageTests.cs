namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The Timeline page: the span it shows, the shapes of each camera's lane, the marks under them,
/// and where a click plays from.
/// </summary>
public sealed class TimelinePageTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    [Fact]
    public void A_Day_Runs_From_Midnight_To_Midnight_In_The_Viewers_Zone_Also_When_The_Clocks_Change()
    {
        var ordinary = TimelineSpan.Day(new DateOnly(2026, 10, 19), Copenhagen);
        var clocksBack = TimelineSpan.Day(new DateOnly(2026, 10, 25), Copenhagen);

        ordinary.From.Should().Be(new DateTimeOffset(2026, 10, 18, 22, 0, 0, TimeSpan.Zero));
        (ordinary.To - ordinary.From).Should().Be(TimeSpan.FromHours(24));
        (clocksBack.To - clocksBack.From).Should().Be(TimeSpan.FromHours(25));
    }

    [Fact]
    public void A_Moment_Is_Found_By_Its_Place_In_The_Span_And_Back()
    {
        var span = TimelineSpan.Last24Hours(Nine);

        span.From.Should().Be(Nine.AddHours(-24));
        span.At(0.5).Should().Be(Nine.AddHours(-12));
        span.FractionOf(Nine.AddHours(-6)).Should().Be(0.75);
        span.FractionOf(Nine.AddHours(1)).Should().BeNull();
    }

    [Fact]
    public void Unanalysed_Stretches_Are_One_Rectangle_Per_Run()
    {
        var lane = Lane(
            [Bucket(recorded: false), Bucket(recorded: false), Bucket(), Bucket(analysed: false), Bucket(analysed: false), Bucket(level: 25)],
            events: []);

        TimelineLaneShapes.NotAnalysed(lane).Should().Equal(new TimelineRect(3, 0, 2, ActivityTimelineShapes.BoxHeight - ActivityTimelineShapes.BandHeight));
        TimelineLaneShapes.Bars(lane).Should().ContainSingle().Which.X.Should().Be(5);
    }

    [Fact]
    public void A_Gap_Is_Placed_By_Its_Time_And_Coloured_And_Named_By_Its_Reason()
    {
        var span = new TimelineSpan(Nine, Nine.AddHours(1));
        var lane = Lane(
            [.. Enumerable.Range(0, 60).Select(_ => Bucket())],
            events: [],
            gaps:
            [
                new TimelineGap(Nine.AddMinutes(-30), Nine.AddMinutes(15), TimelineGapReason.Offline),
                new TimelineGap(Nine.AddMinutes(40), Nine.AddMinutes(50), TimelineGapReason.DiskFull),
                new TimelineGap(Nine.AddMinutes(50), Nine.AddMinutes(55), new TimelineGapReason("somethingNew")),
            ]);

        var gaps = TimelineLaneShapes.Gaps(lane, span);

        gaps.Select(g => (Math.Round(g.Rect.X, 6), Math.Round(g.Rect.Width, 6))).Should().Equal((0, 15), (40, 10), (50, 5));
        gaps.Should().AllSatisfy(g => g.Rect.Height.Should().Be(ActivityTimelineShapes.BoxHeight));
        gaps[0].Text.Should().Be(Translations.GapOffline);
        gaps[1].Fill.Should().Be(TimelineGapStyle.Fill(TimelineGapReason.Names.DiskFull));
        gaps[2].Text.Should().Be(Translations.GapUnknown, "a reason this client does not know yet is shown as nothing recorded");
        TimelineGapStyle.Reasons.Select(TimelineGapStyle.Fill).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("offline", AtcSoft.VideoSurveillance.Enums.GapReason.Offline)]
    [InlineData("diskFull", AtcSoft.VideoSurveillance.Enums.GapReason.DiskFull)]
    [InlineData("serverDown", AtcSoft.VideoSurveillance.Enums.GapReason.ServerDown)]
    [InlineData("notRecording", AtcSoft.VideoSurveillance.Enums.GapReason.NotRecording)]
    [InlineData("unknown", AtcSoft.VideoSurveillance.Enums.GapReason.Unknown)]
    public void The_Gap_Colours_Are_The_Same_As_The_Desktop_Clients(
        string reason,
        AtcSoft.VideoSurveillance.Enums.GapReason core)
    {
        var (a, r, g, b) = AtcSoft.VideoSurveillance.Helpers.Activity.TimelineGapColors.Of(core);
        var parts = TimelineGapStyle.Fill(reason)["rgba(".Length..^1].Split(',');

        parts[..3].Select(p => byte.Parse(p, CultureInfo.InvariantCulture)).Should().Equal(r, g, b);
        Math.Round(double.Parse(parts[3], CultureInfo.InvariantCulture) * 255).Should().Be(a);
        TimelineGapStyle.Reasons.Should().Contain(reason);
    }

    [Fact]
    public void An_Event_Is_Placed_By_Its_Time_And_Drawn_At_Least_Three_Stretches_Wide()
    {
        var span = new TimelineSpan(Nine, Nine.AddHours(1));
        var lane = Lane(
            [.. Enumerable.Range(0, 60).Select(_ => Bucket())],
            events: [new TimelineEvent(Guid.Empty, Nine.AddMinutes(30), Nine.AddMinutes(30).AddSeconds(5), 20, 1, "Cam/a.mkv", 1800)]);

        var band = TimelineLaneShapes.Bands(lane, span).Should().ContainSingle().Subject;

        band.Width.Should().Be(ActivityTimelineLayout.MinimumEventWidth);
        (band.X + (band.Width / 2)).Should().BeApproximately(30 + (5.0 / 120), 1e-9);
    }

    [Fact]
    public void A_Day_Has_A_Mark_Every_Three_Hours_And_A_Week_One_Every_Midnight()
    {
        var day = TimelineSpan.Day(new DateOnly(2026, 10, 19), Copenhagen);
        var week = new TimelineSpan(day.From, day.From.AddDays(7));

        var dayTicks = TimelineTicks.For(day, Copenhagen, CultureInfo.InvariantCulture);
        var weekTicks = TimelineTicks.For(week, Copenhagen, CultureInfo.InvariantCulture);

        dayTicks.Select(t => t.Label).Should().Equal("00:00", "03:00", "06:00", "09:00", "12:00", "15:00", "18:00", "21:00", "00:00");
        dayTicks[4].Fraction.Should().Be(0.5);
        dayTicks[4].Left.Should().Be("50%");
        weekTicks.Should().HaveCount(7, "seven times 24 hours across the clock change ends an hour before the eighth midnight");
        weekTicks[0].Label.Should().Be("Mon 19");
    }

    [Fact]
    public void The_Marks_Are_The_Same_As_The_Desktop_Clients()
    {
        var day = TimelineSpan.Day(new DateOnly(2026, 10, 25), Copenhagen);
        TimelineSpan[] spans =
        [
            day,
            TimelineSpan.Last24Hours(Nine),
            new(Nine, Nine.AddHours(4)),
            new(day.From, day.From.AddDays(7)),
            new(Nine, Nine),
        ];

        foreach (var span in spans)
        {
            foreach (var culture in new[] { CultureInfo.InvariantCulture, new CultureInfo("da-DK") })
            {
                TimelineTicks.For(span, Copenhagen, culture).Select(t => (t.Fraction, t.Label))
                    .Should().Equal(AtcSoft.VideoSurveillance.Helpers.Activity.TimelineMarks.For(span.From, span.To, Copenhagen, culture).Select(m => (m.Fraction, m.Label)));
            }
        }
    }

    [Fact]
    public void A_Click_Plays_The_Recording_That_Covers_It_From_That_Far_In()
    {
        // The recording started before the span: its coverage is cut, its start is not.
        var lane = Lane([Bucket()], events: [], coverage: [new TimelineCoverage(Nine, Nine.AddMinutes(30), "Cam/a.mkv", Nine.AddMinutes(-10))]);

        TimelinePlayback.At(lane, Nine.AddMinutes(5)).Should().Be(new TimelinePlayback("Cam/a.mkv", 15 * 60));
        TimelinePlayback.At(lane, Nine.AddMinutes(40)).Should().BeNull("nothing was recorded then");
        TimelinePlayback.Of(new TimelineEvent(Guid.Empty, Nine, Nine.AddSeconds(4), 9, 1, "Cam/a.mkv", 0.5))
            .Should().Be(new TimelinePlayback("Cam/a.mkv", 0), "an event plays from a second before it, never before the file");
    }

    [Fact]
    public void After_A_Recording_The_Player_Moves_On_To_The_Lanes_Next_Recordings_In_Time_Order()
    {
        var lane = Lane(
            [],
            events: [],
            coverage:
            [
                new TimelineCoverage(Nine.AddHours(2), Nine.AddHours(3), "Cam/c.mkv", Nine.AddHours(2)),
                new TimelineCoverage(Nine, Nine.AddHours(1), "Cam/a.mkv", Nine),
                new TimelineCoverage(Nine.AddHours(1), Nine.AddHours(1.5), "Cam/b.mkv", Nine.AddHours(1)),
            ]);

        TimelinePlayback.Following(lane, "Cam/a.mkv").Select(c => c.FilePath).Should().Equal("Cam/b.mkv", "Cam/c.mkv");
        TimelinePlayback.Following(lane, "Cam/c.mkv").Should().BeEmpty();
        TimelinePlayback.Following(lane, "Other/x.mkv").Should().BeEmpty();
    }

    [Fact]
    public void The_Page_Reloads_On_Activity_And_Recordings_Only_While_It_Shows_Now_And_Not_Too_Often()
    {
        var activity = new SurveillanceEvent("activityChanged", Guid.Empty, Nine, Activity: new ActivityChangedEvent(Guid.Empty, true, Nine, "Cam/a.mkv", 0, 5, 1));
        var connection = new SurveillanceEvent("connectionStateChanged", Guid.Empty, Nine);
        var unknown = new SurveillanceEvent("somethingNew", Guid.Empty, Nine);

        TimelineRefresh.ShouldReload(activity, showsNow: true).Should().BeTrue();
        TimelineRefresh.ShouldReload(activity, showsNow: false).Should().BeFalse("a past day does not change");
        TimelineRefresh.ShouldReload(connection, showsNow: true).Should().BeFalse();
        TimelineRefresh.ShouldReload(unknown, showsNow: true).Should().BeFalse();

        TimelineRefresh.Wait(Nine, Nine.AddSeconds(2)).Should().Be(TimeSpan.FromSeconds(3));
        TimelineRefresh.Wait(Nine, Nine.AddSeconds(9)).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Zoom_And_Move_Are_The_Same_As_The_Desktop_Clients()
    {
        var now = Nine.AddDays(2);
        (DateTimeOffset From, DateTimeOffset To)[] spans =
        [
            (Nine, Nine.AddHours(4)),
            (Nine, Nine.AddMinutes(15)),
            (now.AddDays(-6), now),
            (now.AddHours(-2), now),
        ];
        DateTimeOffset[] focuses = [Nine, Nine.AddHours(1), now, Nine.AddDays(-3)];

        foreach (var (from, to) in spans)
        {
            var span = new TimelineSpan(from, to);
            foreach (var focus in focuses)
            {
                Pair(TimelineZoom.In(span, focus, now)).Should().Be(AtcSoft.VideoSurveillance.Helpers.Activity.TimelineZoom.In(from, to, focus, now));
                Pair(TimelineZoom.Out(span, focus, now)).Should().Be(AtcSoft.VideoSurveillance.Helpers.Activity.TimelineZoom.Out(from, to, focus, now));
            }

            foreach (var later in new[] { true, false })
            {
                Pair(TimelineZoom.Move(span, later, now)).Should().Be(AtcSoft.VideoSurveillance.Helpers.Activity.TimelineZoom.Move(from, to, later, now));
            }
        }

        static (DateTimeOffset, DateTimeOffset) Pair(TimelineSpan span)
            => (span.From, span.To);
    }

    private static TimelineBucket Bucket(
        bool recorded = true,
        bool analysed = true,
        double level = 0)
        => new(Level: level, Tracks: 0, IsRecorded: recorded, IsAnalysed: recorded && analysed);

    private static TimelineLane Lane(
        List<TimelineBucket> buckets,
        List<TimelineEvent> events,
        List<TimelineCoverage>? coverage = null,
        List<TimelineGap>? gaps = null)
        => new(CameraId: Guid.Empty, CameraName: "Cam", Coverage: coverage ?? [], Gaps: gaps ?? [], Buckets: buckets, Events: events);
}