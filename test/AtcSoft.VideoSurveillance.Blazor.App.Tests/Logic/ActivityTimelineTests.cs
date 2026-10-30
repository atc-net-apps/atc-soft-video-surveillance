namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

using CoreBucket = AtcSoft.VideoSurveillance.Models.ActivityBucket;
using CoreEvent = AtcSoft.VideoSurveillance.Models.ActivityEvent;
using CoreHover = AtcSoft.VideoSurveillance.Helpers.Activity.ActivityTimelineHover;
using CoreLayout = AtcSoft.VideoSurveillance.Helpers.Activity.ActivityTimelineLayout;
using CoreZoom = AtcSoft.VideoSurveillance.Helpers.Activity.ActivityTimelineZoom;

/// <summary>
/// The web player's activity timeline: the same layout as the desktop apps' (a copy, kept equal
/// here), drawn as SVG, with jumps to the previous and next event.
/// </summary>
public sealed class ActivityTimelineTests
{
    private static readonly int[][] Recordings =
    [
        [0, 1, 2, 0, 0, 1, 0, 0, 0, 3],
        [1, 1, 1, 1],
        [0, 0, 0],
        [],
        [.. Enumerable.Range(0, 3600).Select(i => i is >= 1800 and < 1805 ? 1 : 0)],
    ];

    [Fact]
    public void The_Copy_Lays_Out_Like_The_Desktop_Apps()
    {
        foreach (var tracks in Recordings)
        {
            foreach (var duration in new[] { 0, 5, 10, 60, 3600 })
            {
                ActivityTimelineLayout.FindEvents([.. tracks.Select(t => Bucket(t * 7, t))], duration).Select(e => (e.Start, e.End))
                    .Should().Equal(CoreLayout.FindEvents([.. tracks.Select(t => new CoreBucket(t * 7, t, IsAnalysed: true))], duration).Select(e => (e.Start, e.End)));
            }

            var web = ActivityTimelineLayout.FindEvents([.. tracks.Select(t => Bucket(t * 7, t))], durationSeconds: 0);
            var core = CoreLayout.FindEvents([.. tracks.Select(t => new CoreBucket(t * 7, t, IsAnalysed: true))]);

            web.Select(e => (e.Start, e.End)).Should().Equal(core.Select(e => (e.Start, e.End)));
            foreach (var width in new[] { 0, 10, 400, 1000 })
            {
                web.Select(e => ActivityTimelineLayout.ToPixels(e, width))
                    .Should().Equal(core.Select(e => CoreLayout.ToPixels(e, width)));
            }

            foreach (var position in new[] { 0, 0.1, 0.3, 0.5001, 0.95, 1 })
            {
                ActivityTimelineLayout.NextEventStart(web, position).Should().Be(CoreLayout.NextEventStart(core, position));
                ActivityTimelineLayout.PreviousEventStart(web, position, 0.01).Should().Be(CoreLayout.PreviousEventStart(core, position, 0.01));
                ActivityTimelineLayout.EventsOnlyTarget(web, position, 0.01, 0.02).Should().Be(CoreLayout.EventsOnlyTarget(core, position, 0.01, 0.02));
            }
        }

        foreach (var level in new[] { -1, 0, 0.5, 4, 25, 99, 100, 400 })
        {
            ActivityTimelineLayout.BarHeight(level).Should().Be(CoreLayout.BarHeight(level));
        }

        ActivityTimelineLayout.MinimumEventWidth.Should().Be(CoreLayout.MinimumEventWidth);
        ActivityTimelineLayout.MergeGapSeconds.Should().Be(AtcSoft.VideoSurveillance.Helpers.Activity.ActivitySummarizer.MergeGapSeconds);
    }

    [Fact]
    public void An_Event_Is_Drawn_In_Its_Watch_Zones_Colour_Else_Orange()
    {
        RecordingActivityBucket[] buckets =
        [
            Bucket(5, 1) with { Zones = ["Gate"] }, Bucket(0, 0),
            Bucket(5, 1) with { Zones = ["Path"] }, Bucket(0, 0),
            Bucket(5, 1), Bucket(0, 0),
        ];
        IReadOnlyList<MotionZone> zones =
        [
            new("Gate", new MotionZoneKind(MotionZoneKind.Names.Watch), [], "Lime"),
            new("Gate", new MotionZoneKind(MotionZoneKind.Names.Watch), [], "Red"),
            new("Path", new MotionZoneKind(MotionZoneKind.Names.Ignore), [], "Blue"),
        ];

        var fills = ActivityTimelineShapes.Bands(ActivityTimelineLayout.FindEvents(buckets, durationSeconds: 0), MotionZoneColors.ByZone(zones)).Select(b => b.Fill);

        fills.Should().Equal("#00FF00", MotionZoneColors.Event, MotionZoneColors.Event);
    }

    [Fact]
    public void The_Copy_Names_An_Events_Zone_Like_The_Desktop_Apps()
    {
        string[][] zones = [["Path"], ["Gate", "Path"], ["Gate"], ["Gate"], [], ["Path"], ["Gate"], [], [], []];
        int[] tracks = [1, 1, 1, 1, 0, 1, 1, 0, 1, 0];

        var web = ActivityTimelineLayout.FindEvents([.. tracks.Select((t, i) => Bucket(t, t) with { Zones = zones[i].Length > 0 ? [.. zones[i]] : null })], durationSeconds: 0);
        var core = CoreLayout.FindEvents([.. tracks.Select((t, i) => new CoreBucket(t, t, IsAnalysed: true) { Zones = zones[i] })]);

        web.Select(e => e.Zone).Should().Equal(core.Select(e => e.Zone)).And.Equal("Gate", "Path", null);
    }

    [Fact]
    public void Bars_Rise_From_Above_The_Band_And_Stretches_Not_Analysed_Are_Marked()
    {
        RecordingActivityBucket[] buckets = [Bucket(25, 0), Bucket(0, 0), Bucket(100, 1), new(Level: 0, Tracks: 0, IsAnalysed: false)];

        var bars = ActivityTimelineShapes.Bars(buckets);
        var notAnalysed = ActivityTimelineShapes.NotAnalysed(buckets);
        var bands = ActivityTimelineShapes.Bands(ActivityTimelineLayout.FindEvents(buckets, durationSeconds: 0), zoneColours: null);

        var area = ActivityTimelineShapes.BoxHeight - ActivityTimelineShapes.BandHeight;
        bars.Should().Equal(new TimelineRect(0, area / 2, 100, area / 2), new TimelineRect(200, 0, 100, area));
        notAnalysed.Should().Equal(new TimelineRect(300, 0, 100, area));
        bands.Should().ContainSingle().Which.Band.Should().Be(new TimelineRect(200, area, 100, ActivityTimelineShapes.BandHeight));
    }

    [Fact]
    public void Zoomed_In_The_Part_Shown_Fills_The_Box()
    {
        RecordingActivityBucket[] buckets = [Bucket(100, 1), Bucket(0, 0), Bucket(100, 1), Bucket(0, 0)];
        var events = ActivityTimelineLayout.FindEvents(buckets, durationSeconds: 0);

        var area = ActivityTimelineShapes.BoxHeight - ActivityTimelineShapes.BandHeight;
        ActivityTimelineShapes.Bars(buckets, 0.5, 1).Should().Equal(new TimelineRect(0, 0, 200, area));
        ActivityTimelineShapes.Bands(events, zoneColours: null, 0.5, 1).Should().ContainSingle()
            .Which.Band.Should().Be(new TimelineRect(0, area, 200, ActivityTimelineShapes.BandHeight), "the first event is not shown");
        ActivityTimelineShapes.Playhead(0.25, 0.5, 1).Should().BeNull("the player is not in the part shown");
        ActivityTimelineShapes.Playhead(0.75, 0.5, 1)!.X.Should().Be(199);
        ActivityTimelineShapes.PlayheadColumn(0.25, 0.5, 1).Should().Be(-1);
    }

    [Fact]
    public void The_Hover_Is_The_Same_As_The_Desktop_Apps()
    {
        List<string>?[] zones = [null, ["Gate"], null, ["Path", "Gate"], null, null];
        int[] tracks = [0, 2, 1, 1, 0, 3];
        RecordingActivityBucket[] web = [.. tracks.Select((t, i) => Bucket(t * 10, t) with { Zones = zones[i] })];
        CoreBucket[] core = [.. tracks.Select((t, i) => new CoreBucket(t * 10, t, IsAnalysed: i != 5) { Zones = zones[i] ?? [] })];
        web[5] = web[5] with { IsAnalysed = false };
        var webEvents = ActivityTimelineLayout.FindEvents(web, durationSeconds: 0);
        var coreEvents = CoreLayout.FindEvents(core);

        foreach (var (from, to) in new[] { (0d, 0.1), (0.2, 0.3), (0.45, 0.55), (0.9, 1), (1.2, -1) })
        {
            var a = ActivityTimelineHover.At(web, webEvents, from, to)!;
            var b = CoreHover.At(core, coreEvents, from, to)!;
            (a.Position, a.Tracks, a.Level, a.IsAnalysed, a.Zone).Should().Be((b.Position, b.Tracks, b.Level, b.IsAnalysed, b.Zone));
        }

        ActivityTimelineHover.At([], [], 0, 1).Should().BeNull();
    }

    [Fact]
    public void The_Hover_Tells_The_Time_What_Moved_And_The_Zone()
    {
        var start = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Local)));

        ActivityHoverText.Lines(new ActivityHover(0.5, 2, 30, IsAnalysed: true, "Gate"), start, 120)
            .Should().Equal("09:01:00", string.Format(CultureInfo.CurrentCulture, Translations.ActivityHoverMoving1, 2), string.Format(CultureInfo.CurrentCulture, Translations.ActivityHoverZone1, "Gate"));
        ActivityHoverText.Lines(new ActivityHover(0.25, 0, 0, IsAnalysed: true, Zone: null), recordingStart: null, 120)
            .Should().Equal("00:30", Translations.ActivityHoverNothingMoving);
        ActivityHoverText.Lines(new ActivityHover(0.5, 0, 0, IsAnalysed: false, Zone: null), recordingStart: null, 7200)
            .Should().Equal("01:00:00", Translations.ActivityHoverNotAnalysed);
        ActivityHoverText.Left(0.5).Should().Be("50%");
        ActivityHoverText.Left(1).Should().Be("90%", "the words stay clear of the edge");
    }

    [Fact]
    public void A_Recording_Opened_Without_Its_Length_Is_Loaded_Again_Once_The_Video_Tells_It()
    {
        ActivityReload.IsNeeded(400, 0).Should().BeFalse("the length is not known yet");
        ActivityReload.IsNeeded(400, 120).Should().BeFalse("two minutes have no more seconds than the fewest stretches");
        ActivityReload.IsNeeded(400, 3600).Should().BeTrue("an hour has a stretch a second");
        ActivityReload.IsNeeded(3600, 3600).Should().BeFalse("loaded for its length already");
    }

    [Fact]
    public void The_Preview_Look_Ups_Are_The_Same_As_The_Desktop_Apps()
    {
        var (core, _) = AtcSoft.VideoSurveillance.Helpers.Activity.RecordingPreviewLayout.Plan(30, 640, 480, [6.5, 20.5]);
        var web = Sheet(core);

        foreach (var seconds in new[] { -1, 0, 0.4, 4.6, 29.9, 99 })
        {
            RecordingPreviewLayout.GridTile(web, seconds).Should().Be(AtcSoft.VideoSurveillance.Helpers.Activity.RecordingPreviewLayout.GridTile(core, seconds));
        }

        foreach (var (from, to) in new[] { (6d, 7d), (1d, 2d), (20d, 21d) })
        {
            RecordingPreviewLayout.EventTile(web, from, to).Should().Be(AtcSoft.VideoSurveillance.Helpers.Activity.RecordingPreviewLayout.EventTile(core, from, to));
        }

        foreach (var tile in new[] { 0, 19, 20, 32 })
        {
            RecordingPreviewLayout.TileRect(web, tile).Should().Be(AtcSoft.VideoSurveillance.Helpers.Activity.RecordingPreviewLayout.TileRect(core, tile));
        }
    }

    [Fact]
    public void Under_The_Mouse_An_Event_Shows_Its_Busiest_Moment_And_Elsewhere_The_Nearest_Picture()
    {
        var (core, _) = AtcSoft.VideoSurveillance.Helpers.Activity.RecordingPreviewLayout.Plan(10, 640, 480, [6.5]);
        var web = Sheet(core);
        ActivityTimelineEvent[] events = [new(0.6, 0.7)];

        RecordingPreviewLayout.Pick(web, events, 0.14, 0.16, 10).Should().Be((2, 0.2), "1.5 s is nearest the tile of 2 s");
        RecordingPreviewLayout.Pick(web, events, 0.64, 0.66, 10).Should().Be((11, 0.65), "the event's own picture, of 6.5 s");
        RecordingPreviewLayout.Pick(null, events, 0.64, 0.66, 10).Should().BeNull();
        RecordingPreviewLayout.TileStyle(web, 21, "http://server/a'b.jpg?media=x").Should().Be(
            "width: 160px; height: 120px; background-image: url('http://server/a%27b.jpg?media=x'); background-position: -160px -120px; background-repeat: no-repeat;");
    }

    [Fact]
    public void The_Zoom_Is_The_Same_As_The_Desktop_Apps()
    {
        (double Start, double End)[] windows = [(0, 1), (0.5, 1), (0.2, 0.3), (0.99, 1), (0.4, 0.40001)];
        double[] places = [-1, 0, 0.1, 0.25, 0.5, 0.95, 1];
        double[] lengths = [0, 30, 600, 3600, 4 * 3600];

        foreach (var seconds in lengths)
        {
            ActivityTimelineZoom.BucketsFor(seconds).Should().Be(CoreZoom.BucketsFor(seconds));
        }

        foreach (var (start, end) in windows)
        {
            ActivityTimelineZoom.CanZoomOut(start, end).Should().Be(CoreZoom.CanZoomOut(start, end));
            foreach (var seconds in lengths)
            {
                ActivityTimelineZoom.CanZoomIn(start, end, seconds).Should().Be(CoreZoom.CanZoomIn(start, end, seconds));
            }

            foreach (var place in places)
            {
                ActivityTimelineZoom.In(start, end, place, 3600).Should().Be(CoreZoom.In(start, end, place, 3600));
                ActivityTimelineZoom.Out(start, end, place).Should().Be(CoreZoom.Out(start, end, place));
                ActivityTimelineZoom.ToRecording(start, end, place).Should().Be(CoreZoom.ToRecording(start, end, place));
                ActivityTimelineZoom.ToShare(start, end, place).Should().Be(CoreZoom.ToShare(start, end, place));
                foreach (var previous in places)
                {
                    ActivityTimelineZoom.Follow(start, end, previous, place).Should().Be(CoreZoom.Follow(start, end, previous, place));
                }
            }

            foreach (var tracks in Recordings)
            {
                var web = ActivityTimelineZoom.Bars([.. tracks.Select(t => Bucket(t * 7, t))], start, end, 400);
                var core = CoreZoom.Bars([.. tracks.Select(t => new CoreBucket(t * 7, t, IsAnalysed: true))], start, end, 400);
                web.Select(b => (b.Start, b.End, b.Level, b.IsAnalysed)).Should().Equal(core.Select(b => (b.Start, b.End, b.Level, b.IsAnalysed)));
            }

            ActivityTimelineEvent[] webEvents = [new(0.1, 0.2), new(0.45, 0.55, "Gate"), new(0.9, 1)];
            CoreEvent[] coreEvents = [new(0.1, 0.2), new(0.45, 0.55, "Gate"), new(0.9, 1)];
            ActivityTimelineZoom.Visible(webEvents, start, end).Select(e => (e.Start, e.End, e.Zone))
                .Should().Equal(CoreZoom.Visible(coreEvents, start, end).Select(e => (e.Start, e.End, e.Zone)));
        }

        ActivityTimelineZoom.ShortestSeconds.Should().Be(CoreZoom.ShortestSeconds);
        ActivityTimelineZoom.MostBuckets.Should().Be(CoreZoom.MostBuckets);
    }

    [Fact]
    public void Next_And_Previous_Land_A_Second_Before_The_Event()
    {
        // A 100 s recording with events at 10 s and 50 s.
        var events = new[] { new ActivityTimelineEvent(0.1, 0.2), new ActivityTimelineEvent(0.5, 0.6) };

        ActivityEventJumps.Next(events, 0, 100).Should().BeApproximately(9, 1e-9);
        ActivityEventJumps.Next(events, 9, 100).Should().BeApproximately(49, 1e-9, "right after a jump, next goes on to the following event");
        ActivityEventJumps.Next(events, 49, 100).Should().BeNull();
        ActivityEventJumps.Previous(events, 49, 100).Should().BeApproximately(9, 1e-9, "right after a jump, previous goes one further back");
        ActivityEventJumps.Previous(events, 80, 100).Should().BeApproximately(49, 1e-9);
        ActivityEventJumps.Previous(events, 9, 100).Should().BeNull();
        ActivityEventJumps.Next(events, 0, 0).Should().BeNull("the length is not known yet");
    }

    [Fact]
    public void An_Svg_Number_Has_A_Point_In_Every_Language()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("da-DK");

            SvgNumber.Format(12.5).Should().Be("12.5");
            SvgNumber.Format(1234.56789).Should().Be("1234.568");
            SvgNumber.Format(3).Should().Be("3");
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Theory]
    [InlineData("PT1M5S", 65)]
    [InlineData("00:00:30", 30)]
    [InlineData(null, 0)]
    [InlineData("nonsense", 0)]
    public void The_Length_Of_A_Recording_Is_Read_In_Seconds(
        string? duration,
        double seconds)
        => RecordingDisplay.DurationSeconds(duration).Should().Be(seconds);

    // The server's answer for a layout made by Core.
    private static RecordingPreviewSheet Sheet(
        AtcSoft.VideoSurveillance.Models.RecordingPreviews layout)
        => new(
            HasPreviews: true,
            IsBeingMade: false,
            ImagePath: "Cam/Cam.previews.jpg",
            IntervalSeconds: layout.IntervalSeconds,
            TileWidth: layout.TileWidth,
            TileHeight: layout.TileHeight,
            Columns: layout.Columns,
            GridCount: layout.GridCount,
            Events: [.. layout.Events.Select(e => new RecordingPreviewTile(Seconds: e.Seconds, Tile: e.Tile))]);

    private static RecordingActivityBucket Bucket(
        double level,
        int tracks)
        => new(Level: level, Tracks: tracks, IsAnalysed: true);

    [Fact]
    public void Play_Events_Only_Skips_The_Quiet_Stretches_In_Seconds_And_The_Playhead_Stays_In_The_Box()
    {
        // A 100 s recording with something moving from 20 to 30 s.
        ActivityTimelineEvent[] events = [new(0.2, 0.3)];

        ActivityEventJumps.EventsOnly(events, 5, 100).Should().BeApproximately(19, 1e-9, "a second before the event");
        ActivityEventJumps.EventsOnly(events, 31, 100).Should().BeNull("two seconds after it are shown");
        ActivityEventJumps.EventsOnly(events, 33, 100).Should().Be(100, "after the last event: the end");
        ActivityEventJumps.EventsOnly(events, 33, 0).Should().BeNull("the length is not known yet");

        ActivityTimelineShapes.Playhead(-1).Should().BeNull();
        ActivityTimelineShapes.Playhead(0.5)!.X.Should().Be(199);
        ActivityTimelineShapes.Playhead(1)!.X.Should().Be(398, "the line stays inside the box");
    }

    [Fact]
    public void A_Sparkline_Has_A_Bar_Per_Busy_Stretch_Coloured_As_On_The_Desktop()
    {
        var bars = ActivitySparklineShapes.Bars([0, 5, 0, 60]);

        bars.Select(b => b.Rect.X).Should().Equal(1.1, 3.1);
        bars[0].Fill.Should().NotBe("#FF8C00", "a flicker stays grey");
        bars[1].Fill.Should().Be("#FF8C00");
        bars[1].Rect.Height.Should().BeGreaterThan(bars[0].Rect.Height);
        ActivitySparklineShapes.BusyLevel.Should().Be(AtcSoft.VideoSurveillance.Helpers.Activity.ActivitySparkline.BusyLevel);
    }
}