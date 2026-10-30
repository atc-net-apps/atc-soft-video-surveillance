namespace AtcSoft.VideoSurveillance.Helpers;

public sealed class VideoQualityHelperTests
{
    [Theory]
    [InlineData("Auto", "Auto")]
    [InlineData("auto", "Auto")]
    [InlineData("1080p", "1080p")]
    [InlineData("720P", "720p")]
    [InlineData(" 480p ", "480p")]
    [InlineData("360p", "360p")]
    public void Normalize_Returns_The_Canonical_Spelling(
        string value,
        string expected)
        => VideoQualityHelper.Normalize(value).Should().Be(expected);

    [Theory]
    [InlineData("Low", "480p")]
    [InlineData("medium", "720p")]
    [InlineData("High", "1080p")]
    [InlineData("Ultra", "Auto")]
    public void Normalize_Translates_The_Older_Names(
        string value,
        string expected)
        => VideoQualityHelper.Normalize(value).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("4k")]
    [InlineData("best")]
    public void Normalize_Returns_Null_For_Anything_Else(string? value)
        => VideoQualityHelper.Normalize(value).Should().BeNull();

    [Fact]
    public void All_Matches_What_The_Settings_Dialog_Offers()
        => VideoQualityHelper.All.Should().BeEquivalentTo(AtcSoft.VideoSurveillance.Factories.DropDownItemsFactory.VideoQualityItems.Keys);
}