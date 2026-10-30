namespace AtcSoft.VideoSurveillance.Wpf.Core.Factories;

/// <summary>
/// The analysis size drop-down: the longer side of the analysed picture.
/// </summary>
public sealed class DropDownItemsFactoryAnalysisSizeTests
{
    [Fact]
    public void The_Default_Is_The_Settings_Default_And_One_Of_The_Choices()
    {
        DropDownItemsFactory.ParseAnalysisResolution(DropDownItemsFactory.DefaultMotionAnalysisResolution)
            .Should().Be(new MotionDetectionSettings().AnalysisWidth);
        DropDownItemsFactory.MotionAnalysisResolutionItems.Should().ContainKey(DropDownItemsFactory.DefaultMotionAnalysisResolution);
    }

    [Theory]
    [InlineData("640", 640)]
    [InlineData("800x600", 800)]
    [InlineData("", MotionAnalysisSize.DefaultLongerSide)]
    [InlineData("wide", MotionAnalysisSize.DefaultLongerSide)]
    [InlineData("0", MotionAnalysisSize.DefaultLongerSide)]
    public void A_Key_Is_Read_As_The_Longer_Side_And_An_Older_One_As_Its_Width(
        string key,
        int expected)
        => DropDownItemsFactory.ParseAnalysisResolution(key).Should().Be(expected);

    [Fact]
    public void A_Size_That_Is_Not_A_Choice_Stays_Shown()
    {
        DropDownItemsFactory.MotionAnalysisResolutionItemsWith("1024").Should().ContainKey("1024");
        DropDownItemsFactory.MotionAnalysisResolutionItemsWith("800").Should().HaveCount(DropDownItemsFactory.MotionAnalysisResolutionItems.Count);
        DropDownItemsFactory.FormatAnalysisResolution(0).Should().Be(DropDownItemsFactory.DefaultMotionAnalysisResolution);
    }
}