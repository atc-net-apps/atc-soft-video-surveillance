namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The editing of a zone's corners, as both editors do it.
/// </summary>
public sealed class MotionZoneEditingTests
{
    private static readonly IReadOnlyList<ZonePoint> Square =
    [
        new(0.2, 0.2),
        new(0.6, 0.2),
        new(0.6, 0.6),
        new(0.2, 0.6),
    ];

    [Fact]
    public void A_New_Zone_Is_A_Square_In_The_Middle_Moved_For_Each_Zone_Already_There()
    {
        var first = MotionZoneEditing.NewZoneCorners(0);
        var second = MotionZoneEditing.NewZoneCorners(1);

        first.Should().HaveCount(4);
        first[0].X.Should().BeApproximately(0.25, 1e-9);
        first[2].X.Should().BeApproximately(0.55, 1e-9);
        second[0].X.Should().BeApproximately(0.3, 1e-9);
        MotionZoneEditing.NewZoneCorners(5).Should().Equal(first);
        Enumerable.Range(0, 20).SelectMany(MotionZoneEditing.NewZoneCorners)
            .Should().OnlyContain(p => p.X >= 0 && p.X <= 1 && p.Y >= 0 && p.Y <= 1);
    }

    [Fact]
    public void A_New_Zone_Takes_The_Lowest_Free_Number()
    {
        static string Name(int n) => $"Zone {n}";

        MotionZoneEditing.NextNumber([], Name).Should().Be(1);
        MotionZoneEditing.NextNumber(["Zone 1", "zone 3"], Name).Should().Be(2);
        MotionZoneEditing.NextNumber(["Zone 1", "Zone 2"], Name).Should().Be(3);
    }

    [Fact]
    public void The_Limits_Are_The_Specs()
    {
        MotionZoneEditing.CanAddZone(15).Should().BeTrue();
        MotionZoneEditing.CanAddZone(16).Should().BeFalse();
        MotionZoneEditing.CanAddCorner(31).Should().BeTrue();
        MotionZoneEditing.CanAddCorner(32).Should().BeFalse();
        MotionZoneEditing.CanRemoveCorner(4).Should().BeTrue();
        MotionZoneEditing.CanRemoveCorner(3).Should().BeFalse();
    }

    [Fact]
    public void A_Corner_Moved_Off_The_Picture_Stays_On_Its_Edge()
    {
        var moved = MotionZoneEditing.MoveCorner(Square, 1, 1.4, -0.2);

        moved[1].Should().Be(new ZonePoint(1, 0));
        moved.Skip(2).Should().Equal(Square.Skip(2));
        Square[1].Should().Be(new ZonePoint(0.6, 0.2), "the corners given are not changed");
    }

    [Fact]
    public void A_Zone_Moved_Against_The_Edge_Keeps_Its_Shape()
    {
        var moved = MotionZoneEditing.MoveZone(Square, 0.7, -0.1);

        moved.Select(p => Math.Round(p.X, 9)).Should().Equal(0.6, 1, 1, 0.6);
        moved.Select(p => Math.Round(p.Y, 9)).Should().Equal(0.1, 0.1, 0.5, 0.5);
    }

    [Fact]
    public void A_Corner_Is_Added_In_The_Middle_Of_An_Edge_Also_The_Closing_One()
    {
        MotionZoneEditing.AddCorner(Square, 0)[1].Should().Be(new ZonePoint(0.4, 0.2));

        var closing = MotionZoneEditing.AddCorner(Square, 3);
        closing.Should().HaveCount(5);
        closing[4].Should().Be(new ZonePoint(0.2, 0.4));
    }

    [Fact]
    public void Corners_Are_Neither_Added_Past_Nor_Removed_Below_The_Limits()
    {
        IReadOnlyList<ZonePoint> full = [.. Enumerable.Range(0, 32).Select(i => new ZonePoint(i / 32d, i % 2))];
        IReadOnlyList<ZonePoint> triangle = [.. Square.Take(3)];

        MotionZoneEditing.AddCorner(full, 0).Should().Equal(full);
        MotionZoneEditing.RemoveCorner(triangle, 0).Should().Equal(triangle);
        MotionZoneEditing.RemoveCorner(Square, 0).Should().Equal(Square.Skip(1));
    }

    [Fact]
    public void The_Longest_Edge_Is_Measured_On_The_Picture_As_Shown()
    {
        // Square in fractions; on a wide picture the top edge is the longest, on a tall one the right edge.
        MotionZoneEditing.LongestEdge(Square, 1600, 900).Should().Be(0);
        MotionZoneEditing.LongestEdge(Square, 900, 1600).Should().Be(1);
        MotionZoneEditing.LongestEdge([], 1600, 900).Should().Be(-1);
    }

    [Fact]
    public void A_Corner_Is_Found_Within_The_Grab_Distance_In_Pixels()
    {
        // On a 1000 x 500 picture, 0.006 across is 6 pixels and 0.02 down is 10 pixels.
        MotionZoneEditing.FindCorner(Square, 0.606, 0.2, 1000, 500).Should().Be(1);
        MotionZoneEditing.FindCorner(Square, 0.6, 0.22, 1000, 500).Should().BeNull();
        MotionZoneEditing.FindCorner(Square, 0.4, 0.4, 1000, 500).Should().BeNull();
    }

    [Fact]
    public void An_Edge_Is_Found_Within_The_Grab_Distance_But_Not_Beyond_Its_Ends()
    {
        MotionZoneEditing.FindEdge(Square, 0.4, 0.21, 1000, 500).Should().Be(0, "5 pixels below the top edge");
        MotionZoneEditing.FindEdge(Square, 0.4, 0.4, 1000, 500).Should().BeNull("the middle is far from every edge");
        MotionZoneEditing.FindEdge(Square, 0.9, 0.2, 1000, 500).Should().BeNull("in line with the top edge, but past its end");
        MotionZoneEditing.FindEdge(Square, 0.2, 0.4, 1000, 500).Should().Be(3, "the closing edge, from the last corner to the first");
    }

    [Fact]
    public void Inside_Is_Even_Odd_So_A_Notch_Is_Outside()
    {
        // A U: the notch in the middle top is not part of the zone.
        IReadOnlyList<ZonePoint> u = [new(0, 0), new(0.3, 0), new(0.3, 0.6), new(0.7, 0.6), new(0.7, 0), new(1, 0), new(1, 1), new(0, 1)];

        MotionZoneEditing.Contains(u, 0.1, 0.1).Should().BeTrue();
        MotionZoneEditing.Contains(u, 0.5, 0.3).Should().BeFalse();
        MotionZoneEditing.Contains(u, 0.5, 0.8).Should().BeTrue();
    }

    [Fact]
    public void The_Zone_On_Top_Is_Found_First()
    {
        IReadOnlyList<IReadOnlyList<ZonePoint>> zones = [Square, MotionZoneEditing.MoveZone(Square, 0.2, 0.2)];

        MotionZoneEditing.FindZone(zones, 0.5, 0.5).Should().Be(1);
        MotionZoneEditing.FindZone(zones, 0.3, 0.3).Should().Be(0);
        MotionZoneEditing.FindZone(zones, 0.9, 0.1).Should().BeNull();
    }

    [Theory]
    [InlineData(1000, 1000, 2.0, 0, 250, 1000, 500)]
    [InlineData(1000, 400, 2.0, 100, 0, 800, 400)]
    [InlineData(1600, 900, 0, 0, 0, 1600, 900)]
    [InlineData(0, 900, 2.0, 0, 0, 0, 0)]
    public void A_Picture_Is_Fitted_In_The_Middle_Keeping_Its_Shape(
        double areaWidth,
        double areaHeight,
        double aspectRatio,
        double x,
        double y,
        double width,
        double height)
    {
        var fit = MotionZoneEditing.Fit(areaWidth, areaHeight, aspectRatio);

        fit.X.Should().BeApproximately(x, 1e-9);
        fit.Y.Should().BeApproximately(y, 1e-9);
        fit.Width.Should().BeApproximately(width, 1e-9);
        fit.Height.Should().BeApproximately(height, 1e-9);
    }
}