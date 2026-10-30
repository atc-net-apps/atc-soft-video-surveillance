namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Boxes are smoothed against the box they were a moment ago, found by overlap or nearness,
/// never by their place in the list: the detector sorts its boxes by size, so the car's box
/// and the person's swap places when one grows past the other.
/// </summary>
public sealed class BoxSmootherTests
{
    [Fact]
    public void A_Box_Is_Smoothed_Against_Itself_When_The_List_Order_Changes()
    {
        var sut = new BoxSmoother(smoothingFactor: 0.5);
        sut.Smooth([Box(0, 0, 100, 100), Box(500, 500, 50, 50)]);

        // The small box moved right and is now listed first.
        var smoothed = sut.Smooth([Box(520, 500, 50, 50), Box(0, 0, 100, 100)]);

        smoothed[0].X.Should().Be(510, "halfway from 500 to 520, not from 0");
        smoothed[1].X.Should().Be(0);
    }

    [Fact]
    public void A_New_Box_Starts_Where_It_Is()
    {
        var sut = new BoxSmoother(smoothingFactor: 0.5);
        sut.Smooth([Box(0, 0, 100, 100)]);

        var smoothed = sut.Smooth([Box(0, 0, 100, 100), Box(800, 400, 60, 60)]);

        smoothed[1].X.Should().Be(800);
        smoothed[1].Y.Should().Be(400);
    }

    [Fact]
    public void A_Box_That_Went_Away_Leaves_No_Trace()
    {
        var sut = new BoxSmoother(smoothingFactor: 0.5);
        sut.Smooth([Box(0, 0, 100, 100), Box(800, 400, 60, 60)]);
        sut.Smooth([Box(0, 0, 100, 100)]);

        var smoothed = sut.Smooth([Box(0, 0, 100, 100), Box(820, 400, 60, 60)]);

        smoothed[1].X.Should().Be(820, "the box at 800 was gone for a frame, so this one starts afresh");
    }

    [Fact]
    public void Two_Boxes_Never_Follow_The_Same_Previous_Box()
    {
        var sut = new BoxSmoother(smoothingFactor: 0.5);
        sut.Smooth([Box(100, 100, 50, 50)]);

        var smoothed = sut.Smooth([Box(110, 100, 50, 50), Box(130, 100, 50, 50)]);

        smoothed.Select(b => b.X).Should().Contain(105, "the closer one follows the previous box")
            .And.Contain(130, "the other starts where it is");
    }

    [Fact]
    public void A_Box_Follows_Its_Own_Track_Even_When_Another_Box_Is_Nearer()
    {
        // Two people cross: the nearest previous box is the other person's.
        var sut = new BoxSmoother(smoothingFactor: 0.5);
        sut.Smooth([Box(100, 100, 40, 100, track: 1), Box(160, 100, 40, 100, track: 2)]);

        var smoothed = sut.Smooth([Box(150, 100, 40, 100, track: 1), Box(110, 100, 40, 100, track: 2)]);

        smoothed[0].X.Should().Be(125, "track 1 moved from 100 to 150");
        smoothed[1].X.Should().Be(135, "track 2 moved from 160 to 110");
        smoothed[0].TrackId.Should().Be(1);
    }

    [Fact]
    public void Without_Smoothing_The_Boxes_Are_Shown_As_They_Are()
    {
        var sut = new BoxSmoother(smoothingFactor: 0);
        sut.Smooth([Box(0, 0, 100, 100)]);

        sut.Smooth([Box(40, 0, 100, 100)])[0].X.Should().Be(40);
    }

    private static BoundingBox Box(
        double x,
        double y,
        double width,
        double height,
        int? track = null)
        => new() { X = x, Y = y, Width = width, Height = height, TrackId = track };
}