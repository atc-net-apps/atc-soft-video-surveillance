namespace AtcSoft.VideoSurveillance.Resources;

public sealed class ResourceFileTests
{
    private static readonly IReadOnlyDictionary<string, string> Neutral = ResxFile.Read(string.Empty);

    [Theory]
    [InlineData("da-DK")]
    [InlineData("de-DE")]
    [InlineData("en-GB")]
    public void Every_Text_Is_Translated(string culture)
    {
        var translated = ResxFile.Read(culture);

        Neutral.Keys.Except(translated.Keys, StringComparer.Ordinal).Should().BeEmpty($"every key needs a {culture} text");
        translated.Keys.Except(Neutral.Keys, StringComparer.Ordinal).Should().BeEmpty($"{culture} has no keys of its own");
    }

    [Theory]
    [InlineData("")]
    [InlineData("da-DK")]
    [InlineData("de-DE")]
    [InlineData("en-GB")]
    public void No_Text_Is_Empty(string culture)
        => ResxFile.Read(culture)
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => entry.Key)
            .Should().BeEmpty();

    [Fact]
    public void British_English_Has_Its_Own_Spellings()
        => ResxFile.Read("en-GB")
            .Count(entry => entry.Value != Neutral[entry.Key])
            .Should().BePositive("en-GB has every key, and its own text where British English differs");

    [Theory]
    [InlineData("")]
    [InlineData("da-DK")]
    [InlineData("de-DE")]
    [InlineData("en-GB")]
    public void No_Two_Keys_Differ_Only_In_Case(string culture)
        => ResxFile.Names(culture)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group))
            .Should().BeEmpty("the build keeps one of two keys that differ only in case and drops the other");
}