namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The size a picture is analysed at: the longer side as set, the other following the picture.
/// </summary>
public sealed class MotionAnalysisSizeTests
{
    [Theory]
    [InlineData(800, 1920, 1080, 800, 450)]
    [InlineData(800, 640, 480, 800, 600)]
    [InlineData(320, 2560, 1440, 320, 180)]
    [InlineData(800, 1280, 1024, 800, 640)]
    public void A_Camera_Lying_Down_Keeps_Its_Width_And_Its_Shape(
        int longerSide,
        int displayWidth,
        int displayHeight,
        int width,
        int height)
        => MotionAnalysisSize.For(longerSide, displayWidth, displayHeight).Should().Be((width, height));

    [Fact]
    public void A_Camera_Turned_Upright_Costs_No_More_Than_One_Lying_Down()
    {
        var (width, height) = MotionAnalysisSize.For(800, 1080, 1920);

        (width, height).Should().Be((450, 800));
        (width * height).Should().BeLessThanOrEqualTo(800 * 600, "today's 800 x 600 is the most a camera costs");
    }

    [Fact]
    public void Before_The_First_Picture_Or_Without_A_Size_It_Is_4_By_3_Lying_Down()
    {
        MotionAnalysisSize.For(800, 0, 0).Should().Be((800, 600));
        MotionAnalysisSize.For(0, 0, 0).Should().Be((MotionAnalysisSize.DefaultLongerSide, 600));
        new MotionDetectionSettings().AnalysisWidth.Should().Be(MotionAnalysisSize.DefaultLongerSide);
    }

    [Fact]
    public void A_Very_Narrow_Picture_Keeps_A_Usable_Side()
        => MotionAnalysisSize.For(320, 10_000, 10).Should().Be((320, MotionAnalysisSize.MinSide));
}