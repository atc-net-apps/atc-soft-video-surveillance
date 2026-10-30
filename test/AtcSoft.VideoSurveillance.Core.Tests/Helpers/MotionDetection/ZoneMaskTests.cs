namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The ignore zones filled in at the analysis size: by pixel centres, concave shapes kept, shapes
/// that are not ignore zones or not shapes at all left out.
/// </summary>
public sealed class ZoneMaskTests
{
    [Fact]
    public void A_Rectangle_Covers_The_Pixels_Whose_Centres_Are_Inside()
    {
        var mask = ZoneMask.Ignored([Zone(MotionZoneKind.Ignore, (0, 0), (0.5, 0), (0.5, 0.5), (0, 0.5))], 10, 10)!;

        Count(mask).Should().Be(25);
        mask[(4 * 10) + 4].Should().Be(1);
        mask[(5 * 10) + 5].Should().Be(0, "the edge at 5 is not past the centre at 5.5");
    }

    [Fact]
    public void A_Concave_Shape_Keeps_Its_Notch()
    {
        // A U: the middle of the top half is open.
        var mask = ZoneMask.Ignored(
            [Zone(MotionZoneKind.Ignore, (0, 0), (0.3, 0), (0.3, 0.5), (0.7, 0.5), (0.7, 0), (1, 0), (1, 1), (0, 1))],
            10,
            10)!;

        mask[(2 * 10) + 5].Should().Be(0, "inside the notch");
        mask[(2 * 10) + 1].Should().Be(1, "the left arm");
        mask[(8 * 10) + 5].Should().Be(1, "the bottom");
    }

    [Fact]
    public void Only_Ignore_Zones_With_Three_Corners_Or_More_Count()
    {
        ZoneMask.Ignored([], 10, 10).Should().BeNull();
        ZoneMask.Ignored(null, 10, 10).Should().BeNull();
        ZoneMask.Ignored([Zone(MotionZoneKind.Watch, (0, 0), (1, 0), (1, 1))], 10, 10).Should().BeNull("a watched zone is not ignored");
        ZoneMask.Ignored([Zone(MotionZoneKind.Ignore, (0, 0), (1, 1))], 10, 10).Should().BeNull("a line is not a shape");
    }

    [Fact]
    public void A_Zone_Reaching_Past_The_Picture_Is_Cut_To_It()
    {
        var mask = ZoneMask.Ignored([Zone(MotionZoneKind.Ignore, (-1, -1), (2, -1), (2, 2), (-1, 2))], 8, 6)!;

        Count(mask).Should().Be(48);
    }

    private static MotionZone Zone(
        MotionZoneKind kind,
        params (double X, double Y)[] points)
        => new() { Kind = kind, Points = [.. points.Select(p => new ZonePoint(p.X, p.Y))] };

    private static int Count(byte[] mask)
        => mask.Count(b => b == 1);
}