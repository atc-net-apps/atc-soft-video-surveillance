namespace AtcSoft.VideoSurveillance.Resources;

public sealed partial class PlaceholderTests
{
    private static readonly IReadOnlyDictionary<string, string> Neutral = ResxFile.Read(string.Empty);

    [Fact]
    public void A_Key_Ends_With_Its_Number_Of_Parameters()
        => Neutral
            .Where(entry => Placeholders(entry.Value).Count != DigitSuffix(entry.Key))
            .Select(entry => $"{entry.Key}: {Placeholders(entry.Value).Count} parameter(s)")
            .Should().BeEmpty("a key's last digit is its number of parameters, and a key without parameters ends in no digit");

    [Theory]
    [InlineData("da-DK")]
    [InlineData("de-DE")]
    [InlineData("en-GB")]
    public void A_Translation_Has_The_Same_Parameters(string culture)
        => ResxFile.Read(culture)
            .Where(entry => Neutral.TryGetValue(entry.Key, out var original) &&
                            !Placeholders(entry.Value).SetEquals(Placeholders(original)))
            .Select(entry => entry.Key)
            .Should().BeEmpty();

    private static HashSet<int> Placeholders(string text)
        => [.. Placeholder().Matches(text).Select(match => int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture))];

    private static int DigitSuffix(string key)
    {
        var match = TrailingDigits().Match(key);
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : 0;
    }

    // {0}, {1:N0}, {2,5} - not an escaped {{0}}.
    [GeneratedRegex(@"(?<!\{)\{(?<index>\d+)(?:[,:][^}]*)?\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\d+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrailingDigits();
}