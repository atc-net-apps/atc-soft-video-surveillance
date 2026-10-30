namespace AtcSoft.VideoSurveillance.Helpers;

public class MotionBoxMapperTests
{
    [Theory]
    [InlineData(CameraRotation.None, 1920, 1080)]
    [InlineData(CameraRotation.Rotate180, 1920, 1080)]
    [InlineData(CameraRotation.Rotate90, 1080, 1920)]
    [InlineData(CameraRotation.Rotate270, 1080, 1920)]
    public void GetDisplaySize_Swaps_Dimensions_Only_For_Quarter_Turns(
        CameraRotation rotation,
        int expectedWidth,
        int expectedHeight)
    {
        var (width, height) = MotionBoxMapper.GetDisplaySize(1920, 1080, rotation);

        width.Should().Be(expectedWidth);
        height.Should().Be(expectedHeight);
    }

    [Fact]
    public void CalculateVideoArea_Same_Aspect_Fills_The_Container()
    {
        var area = MotionBoxMapper.CalculateVideoArea(1600, 900, 1920, 1080);

        area.X.Should().BeApproximately(0, 0.001);
        area.Y.Should().BeApproximately(0, 0.001);
        area.Width.Should().BeApproximately(1600, 0.001);
        area.Height.Should().BeApproximately(900, 0.001);
    }

    [Fact]
    public void CalculateVideoArea_Wider_Video_Gets_Bars_Top_And_Bottom()
    {
        // 16:9 video in a square container.
        var area = MotionBoxMapper.CalculateVideoArea(1600, 1600, 1920, 1080);

        area.X.Should().BeApproximately(0, 0.001);
        area.Y.Should().BeApproximately(350, 0.001);
        area.Width.Should().BeApproximately(1600, 0.001);
        area.Height.Should().BeApproximately(900, 0.001);
    }

    [Fact]
    public void CalculateVideoArea_Quarter_Turned_Video_Gets_Bars_Left_And_Right()
    {
        // A 1920x1080 camera rotated 90 degrees is displayed 1080x1920.
        var (displayWidth, displayHeight) = MotionBoxMapper.GetDisplaySize(1920, 1080, CameraRotation.Rotate90);

        var area = MotionBoxMapper.CalculateVideoArea(1600, 900, displayWidth, displayHeight);

        area.Width.Should().BeApproximately(506.25, 0.001);
        area.Height.Should().BeApproximately(900, 0.001);
        area.X.Should().BeApproximately(546.875, 0.001);
        area.Y.Should().BeApproximately(0, 0.001);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    public void CalculateVideoArea_Unknown_Video_Size_Uses_The_Full_Container(
        int displayWidth,
        int displayHeight)
    {
        var area = MotionBoxMapper.CalculateVideoArea(800, 600, displayWidth, displayHeight);

        area.X.Should().Be(0);
        area.Y.Should().Be(0);
        area.Width.Should().Be(800);
        area.Height.Should().Be(600);
    }

    [Fact]
    public void MapToVideoArea_Scales_And_Offsets_Into_The_Video_Area()
    {
        // Box covering the right half of an 800x600 analysis frame.
        var box = new BoundingBox { X = 400, Y = 0, Width = 400, Height = 600 };
        var area = new BoundingBox { X = 546.875, Y = 0, Width = 506.25, Height = 900 };

        var mapped = MotionBoxMapper.MapToVideoArea(box, 800, 600, area);

        mapped.X.Should().BeApproximately(546.875 + 253.125, 0.001);
        mapped.Y.Should().BeApproximately(0, 0.001);
        mapped.Width.Should().BeApproximately(253.125, 0.001);
        mapped.Height.Should().BeApproximately(900, 0.001);
    }

    [Theory]
    [InlineData(CameraRotation.None)]
    [InlineData(CameraRotation.Rotate90)]
    [InlineData(CameraRotation.Rotate180)]
    [InlineData(CameraRotation.Rotate270)]
    public void A_Box_Covering_The_Whole_Analysis_Frame_Covers_Exactly_The_Video_Area(
        CameraRotation rotation)
    {
        var (displayWidth, displayHeight) = MotionBoxMapper.GetDisplaySize(1920, 1080, rotation);
        var area = MotionBoxMapper.CalculateVideoArea(1000, 700, displayWidth, displayHeight);
        var box = new BoundingBox { X = 0, Y = 0, Width = 800, Height = 600 };

        var mapped = MotionBoxMapper.MapToVideoArea(box, 800, 600, area);

        mapped.X.Should().BeApproximately(area.X, 0.001);
        mapped.Y.Should().BeApproximately(area.Y, 0.001);
        mapped.Width.Should().BeApproximately(area.Width, 0.001);
        mapped.Height.Should().BeApproximately(area.Height, 0.001);

        // The displayed aspect ratio is kept, so the boxes stay off the black bars.
        (area.Width / area.Height).Should().BeApproximately((double)displayWidth / displayHeight, 0.001);
    }

    [Fact]
    public void MapToVideoArea_Keeps_The_Track_Id_So_The_Overlay_Can_Follow_The_Box()
        => MotionBoxMapper.MapToVideoArea(
                new BoundingBox { X = 10, Y = 10, Width = 10, Height = 10, TrackId = 7 },
                100,
                100,
                new BoundingBox { Width = 200, Height = 200 })
            .TrackId.Should().Be(7);

    [Fact]
    public void MapToVideoArea_Without_An_Analysis_Size_Returns_An_Empty_Box()
    {
        var mapped = MotionBoxMapper.MapToVideoArea(
            new BoundingBox { X = 10, Y = 10, Width = 10, Height = 10 },
            0,
            0,
            new BoundingBox { X = 5, Y = 6, Width = 100, Height = 100 });

        mapped.Width.Should().Be(0);
        mapped.Height.Should().Be(0);
    }
}