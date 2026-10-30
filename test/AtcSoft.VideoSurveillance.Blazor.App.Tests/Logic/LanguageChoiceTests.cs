namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class LanguageChoiceTests
{
    [Theory]
    [InlineData("da-DK", "en-US", "da-DK")]
    [InlineData(null, "da", "da-DK")]
    [InlineData(null, "da-DK", "da-DK")]
    [InlineData(null, "de-AT", "de-DE")]
    [InlineData(null, "en-GB", "en-GB")]
    [InlineData(null, "en-AU", "en-US")]
    [InlineData(null, "fr-FR", "en-US")]
    [InlineData(null, "", "en-US")]
    [InlineData(null, null, "en-US")]
    [InlineData("fr-FR", "de-DE", "de-DE")]
    [InlineData("1030", "en-US", "en-US")]
    public void The_Language_Is_The_Stored_Choice_Else_The_Browsers(
        string? stored,
        string? browser,
        string expected)
        => LanguageChoice.Resolve(stored, browser).Should().Be(expected);

    [Fact]
    public void The_Four_Languages_Are_Offered()
        => LanguageChoice.Supported.Select(language => language.Culture)
            .Should().Equal("en-US", "en-GB", "da-DK", "de-DE");
}