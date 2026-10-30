namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The parts of the built-in detector, each on its own: grouping pixels into blobs, cleaning
/// the mask, and the tracker that shows only what has travelled.
/// </summary>
public sealed class MotionDetectionPartsTests
{
    private const int Width = 40;
    private const int Height = 30;

    [Fact]
    public void Two_Separate_Squares_Are_Two_Blobs_With_Their_Boxes()
    {
        var mask = Mask((2, 2, 5, 5), (20, 10, 4, 6));

        var blobs = ConnectedComponents.Find(mask, Width, Height).OrderBy(b => b.MinX).ToList();

        blobs.Should().HaveCount(2);
        blobs[0].Should().Be(new Blob(2, 2, 6, 6, 25));
        blobs[1].Should().Be(new Blob(20, 10, 23, 15, 24));
    }

    [Fact]
    public void Pixels_Touching_At_A_Corner_Are_One_Blob()
    {
        var mask = Mask((2, 2, 2, 2), (4, 4, 2, 2));

        ConnectedComponents.Find(mask, Width, Height).Should().ContainSingle();
    }

    [Fact]
    public void A_U_Shape_Joins_Its_Two_Arms_Into_One_Blob()
    {
        var mask = Mask((2, 2, 2, 10), (10, 2, 2, 10), (2, 10, 10, 2));

        ConnectedComponents.Find(mask, Width, Height).Should().ContainSingle().Which.Pixels.Should().Be(20 + 20 + 20 - 8);
    }

    [Fact]
    public void Blobs_Within_The_Gap_Are_Merged_Into_One_Object()
    {
        var blobs = new List<Blob> { new(0, 0, 4, 4, 25), new(7, 0, 10, 4, 20), new(30, 20, 35, 25, 36) };

        var merged = ConnectedComponents.Merge(blobs, gap: 3);

        merged.Should().HaveCount(2);
        merged.Should().Contain(new Blob(0, 0, 10, 4, 45));
    }

    [Fact]
    public void Opening_Removes_A_Speck_And_Keeps_A_Body()
    {
        var mask = Mask((1, 1, 1, 1), (10, 10, 8, 8));

        MaskOperations.Open(mask, Width, Height, radius: 1);

        mask[(1 * Width) + 1].Should().Be(0);
        mask[(14 * Width) + 14].Should().Be(1);
    }

    [Fact]
    public void Closing_Fills_A_Hole()
    {
        var mask = Mask((10, 10, 9, 9));
        mask[(14 * Width) + 14] = 0;

        MaskOperations.Close(mask, Width, Height, radius: 1);

        mask[(14 * Width) + 14].Should().Be(1);
    }

    [Fact]
    public void Something_Swaying_On_The_Spot_Never_Shows()
    {
        var tracker = new MotionTracker();
        var options = new MotionDetectorOptions();
        var shown = new List<(Blob Box, int Id, bool Certain)>();

        for (var i = 0; i < 40; i++)
        {
            var dx = i % 2 == 0 ? 0 : 3;
            shown.AddRange(tracker.Update([new Blob(100 + dx, 100, 120 + dx, 140, 400)], i * 0.25, options, width: 800));
        }

        shown.Should().BeEmpty();
    }

    [Fact]
    public void Something_That_Travels_Shows_With_One_Id()
    {
        var tracker = new MotionTracker();
        var options = new MotionDetectorOptions();
        var shown = new List<(Blob Box, int Id, bool Certain)>();

        for (var i = 0; i < 20; i++)
        {
            var x = 100 + (i * 10);
            shown.AddRange(tracker.Update([new Blob(x, 100, x + 20, 140, 400)], i * 0.25, options, width: 800));
        }

        shown.Should().NotBeEmpty();
        shown.Select(s => s.Id).Distinct().Should().ContainSingle();
        shown[0].Certain.Should().BeFalse("it has only just travelled far enough to show");
        shown[^1].Certain.Should().BeTrue("by now it has gone three times as far: an object for certain");
    }

    private static byte[] Mask(params (int X, int Y, int W, int H)[] rects)
    {
        var mask = new byte[Width * Height];
        foreach (var (x, y, w, h) in rects)
        {
            for (var row = y; row < y + h; row++)
            {
                for (var column = x; column < x + w; column++)
                {
                    mask[(row * Width) + column] = 1;
                }
            }
        }

        return mask;
    }
}