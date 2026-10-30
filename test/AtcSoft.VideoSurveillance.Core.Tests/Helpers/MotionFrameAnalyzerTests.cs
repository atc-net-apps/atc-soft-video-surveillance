namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// The comparison of two pictures that decides whether something moved, and
/// where. The desktop app and the server both use it.
/// </summary>
public sealed class MotionFrameAnalyzerTests
{
    private const int Width = 320;
    private const int Height = 240;

    private readonly MotionDetectionSettings settings = new();

    [Fact]
    public void Two_Equal_Pictures_Show_No_Motion()
    {
        var result = MotionFrameAnalyzer.Analyze(Picture(), Picture(), Width, Height, settings);

        result.ChangePercent.Should().Be(0);
        result.BoundingBoxes.Should().BeEmpty();
    }

    [Fact]
    public void Something_That_Moved_Is_Measured_And_Boxed()
    {
        // A bright block of 100 x 80 = about a tenth of the picture.
        var after = Picture();
        Paint(after, x: 100, y: 60, width: 100, height: 80);

        var result = MotionFrameAnalyzer.Analyze(Picture(), after, Width, Height, settings);

        result.ChangePercent.Should().BeApproximately(100.0 * 100 * 80 / (Width * Height), 0.01);
        var box = result.BoundingBoxes.Should().ContainSingle().Subject;
        box.X.Should().BeInRange(90, 100);
        box.Y.Should().BeInRange(50, 60);
        (box.X + box.Width).Should().BeInRange(200, 210);
        (box.Y + box.Height).Should().BeInRange(140, 150);
    }

    [Fact]
    public void Things_That_Moved_Apart_From_Each_Other_Get_A_Box_Each_Largest_First()
    {
        var after = Picture();
        Paint(after, x: 20, y: 20, width: 40, height: 40);
        Paint(after, x: 200, y: 120, width: 80, height: 80);

        var result = MotionFrameAnalyzer.Analyze(Picture(), after, Width, Height, settings);

        result.BoundingBoxes.Should().HaveCount(2);
        result.BoundingBoxes[0].X.Should().BeGreaterThan(150, "the larger one comes first");
        result.BoundingBoxes[1].X.Should().BeLessThan(100);
    }

    [Fact]
    public void Scattered_Noise_Counts_As_Change_But_Gets_No_Box()
    {
        // One changed pixel in every 20 x 20 cell: far below what a cell
        // needs to count as moving.
        var after = Picture();
        for (var y = 10; y < Height; y += 20)
        {
            for (var x = 10; x < Width; x += 20)
            {
                after[(y * Width) + x] = 255;
            }
        }

        var result = MotionFrameAnalyzer.Analyze(Picture(), after, Width, Height, settings);

        result.ChangePercent.Should().BeGreaterThan(0);
        result.BoundingBoxes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(100, 20, true)]
    [InlineData(0, 20, false)]
    [InlineData(0, 40, true)]
    public void A_Higher_Sensitivity_Notices_A_Smaller_Difference(
        int sensitivity,
        byte difference,
        bool noticed)
    {
        // Sensitivity 100 notices a difference above 15, sensitivity 0 only above 35.
        settings.Sensitivity = sensitivity;
        var after = Picture();
        Paint(after, x: 100, y: 60, width: 100, height: 80, level: (byte)(50 + difference));

        var result = MotionFrameAnalyzer.Analyze(Picture(), after, Width, Height, settings);

        (result.ChangePercent > 0).Should().Be(noticed);
    }

    [Fact]
    public void Pictures_Of_Different_Sizes_Are_Not_Compared()
    {
        var result = MotionFrameAnalyzer.Analyze(new byte[100], Picture(), Width, Height, settings);

        result.ChangePercent.Should().Be(0);
        result.BoundingBoxes.Should().BeEmpty();
    }

    [Fact]
    public void A_Box_Smaller_Than_The_Minimum_Area_Is_Left_Out()
    {
        settings.BoundingBox.MinArea = 5000;
        var after = Picture();
        Paint(after, x: 20, y: 20, width: 40, height: 40);

        var result = MotionFrameAnalyzer.Analyze(Picture(), after, Width, Height, settings);

        result.ChangePercent.Should().BeGreaterThan(0);
        result.BoundingBoxes.Should().BeEmpty();
    }

    private static byte[] Picture()
    {
        var picture = new byte[Width * Height];
        Array.Fill(picture, (byte)50);
        return picture;
    }

    private static void Paint(
        byte[] picture,
        int x,
        int y,
        int width,
        int height,
        byte level = 200)
    {
        for (var row = y; row < y + height; row++)
        {
            Array.Fill(picture, level, (row * Width) + x, width);
        }
    }
}